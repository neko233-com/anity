#!/usr/bin/env bash
# Publish a directly runnable, self-contained Anity CLI for the native host.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONFIG="${1:-Release}"
REQUESTED_RID="${2:-}"

case "$CONFIG" in
  Debug|Release) ;;
  *) echo "ERROR: configuration must be Debug or Release" >&2; exit 1 ;;
esac

OS="$(uname -s)"
ARCH="$(uname -m)"
case "$OS/$ARCH" in
  Darwin/arm64) HOST_RID="osx-arm64"; NATIVE_NAME="libanity_native.dylib" ;;
  Darwin/x86_64) HOST_RID="osx-x64"; NATIVE_NAME="libanity_native.dylib" ;;
  Linux/aarch64|Linux/arm64) HOST_RID="linux-arm64"; NATIVE_NAME="libanity_native.so" ;;
  Linux/x86_64) HOST_RID="linux-x64"; NATIVE_NAME="libanity_native.so" ;;
  *) echo "ERROR: unsupported CLI publish host: $OS/$ARCH" >&2; exit 1 ;;
esac

RID="${REQUESTED_RID:-$HOST_RID}"
if [[ "$RID" != "$HOST_RID" ]]; then
  echo "ERROR: $RID cannot use the native runtime built for $HOST_RID" >&2
  exit 1
fi

OUTPUT_DIR="$ROOT/build/cli/$RID"
if [[ -n "${ANITY_CLI_PUBLISH_DIR:-}" && "$ANITY_CLI_PUBLISH_DIR" != "$OUTPUT_DIR" ]]; then
  echo "ERROR: ANITY_CLI_PUBLISH_DIR must equal $OUTPUT_DIR" >&2
  exit 1
fi

remove_directory() {
  local target="$1"
  [[ -e "$target" || -L "$target" ]] || return 0
  [[ -d "$target" && ! -L "$target" ]] || {
    echo "ERROR: refusing to remove non-directory artifact: $target" >&2
    exit 1
  }
  find "$target" -mindepth 1 -depth -delete
  rmdir "$target"
}

require() {
  command -v "$1" >/dev/null 2>&1 || {
    echo "ERROR: $1 is required" >&2
    exit 1
  }
}

require dotnet
require cmake
require file
[[ "$OS" != "Darwin" ]] || require codesign

mkdir -p "$ROOT/build/cli"
STAGING_DIR="$(mktemp -d "$ROOT/build/cli/anity-cli-$RID.XXXXXX")"
SMOKE_DIR=""
cleanup() {
  [[ -z "$SMOKE_DIR" ]] || remove_directory "$SMOKE_DIR"
  remove_directory "$STAGING_DIR"
}
trap cleanup EXIT
mkdir -p "$STAGING_DIR/locks"
LOCK_FILE_PATTERN="$STAGING_DIR/locks/\$(MSBuildProjectName).packages.lock.json"

bash "$ROOT/_scripts/build-native.sh" "$CONFIG"
dotnet build "$ROOT/_scripts/Anity.MetadataFixups/Anity.MetadataFixups.csproj" \
  --configuration "$CONFIG" --nologo --verbosity quiet --disable-build-servers \
  -m:1 -clp:ErrorsOnly

dotnet publish "$ROOT/anity-cli/src/Anity.Cli/Anity.Cli.csproj" \
  --configuration "$CONFIG" --runtime "$RID" --self-contained true \
  --nologo --disable-build-servers -m:1 -clp:ErrorsOnly \
  -p:PublishSingleFile=false -p:UseAppHost=true \
  -p:DebugSymbols=false -p:DebugType=None \
  -p:SkipUnityMetadataFixups=true \
  -p:RestorePackagesWithLockFile=true \
  -p:NuGetLockFilePath="$LOCK_FILE_PATTERN" \
  -o "$STAGING_DIR"

CLI="$STAGING_DIR/anity"
[[ -x "$CLI" ]] || { echo "ERROR: self-contained publish did not create $CLI" >&2; exit 1; }

if [[ "$OS" == "Darwin" ]]; then
  NATIVE_SOURCE="$ROOT/anity-native/build/libanity_native.dylib"
else
  NATIVE_SOURCE="$ROOT/anity-native/build/libanity_native.so"
fi
[[ -f "$NATIVE_SOURCE" ]] || { echo "ERROR: native runtime is missing: $NATIVE_SOURCE" >&2; exit 1; }
cp "$NATIVE_SOURCE" "$STAGING_DIR/$NATIVE_NAME"

case "$RID" in
  *-arm64) FILE_ARCH_PATTERN='arm64|aarch64' ;;
  *-x64) FILE_ARCH_PATTERN='x86_64|x86-64' ;;
  *) echo "ERROR: unsupported architecture in RID: $RID" >&2; exit 1 ;;
esac
file "$CLI" | grep -Eq "$FILE_ARCH_PATTERN"
file "$STAGING_DIR/$NATIVE_NAME" | grep -Eq "$FILE_ARCH_PATTERN"

if [[ "$OS" == "Darwin" ]]; then
  codesign --force --sign - "$STAGING_DIR/$NATIVE_NAME"
  codesign --force --sign - "$CLI"
  codesign --verify --strict --verbose=2 "$STAGING_DIR/$NATIVE_NAME"
  codesign --verify --strict --verbose=2 "$CLI"
fi

SMOKE_DIR="$(mktemp -d "$ROOT/build/cli/anity-cli-smoke.XXXXXX")"
(
  cd "$SMOKE_DIR"
  env -u DOTNET_ROOT_X64 -u DOTNET_ROOT_ARM64 \
    DOTNET_ROOT="/nonexistent/anity-dotnet-root" DOTNET_MULTILEVEL_LOOKUP=0 \
    "$CLI" -batchmode -quit -nographics -logFile - >stdout.log 2>stderr.log
  grep -q '^batchmode=1$' stdout.log
  grep -q '^nographics=1$' stdout.log
  grep -q '^quit=1$' stdout.log
  [[ ! -e ./- ]]
)
remove_directory "$SMOKE_DIR"
SMOKE_DIR=""

remove_directory "$OUTPUT_DIR"
mv "$STAGING_DIR" "$OUTPUT_DIR"
trap - EXIT

echo "Anity self-contained CLI published and verified: $OUTPUT_DIR"
