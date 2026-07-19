#!/usr/bin/env bash
# Build and install the native Apple Silicon Anity Editor bundle.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONFIG="${1:-Release}"
INSTALL_ROOT="${ANITY_MACOS_INSTALL_DIR:-/Applications}"
PUBLISH_DIR="$ROOT/build/macos-arm64/publish"
RESTORE_LOCK_DIR="$ROOT/build/macos-arm64/restore-locks"
BUNDLE_DIR="$INSTALL_ROOT/Anity.app"
TEMPLATE="$ROOT/assets/macos/Info.plist"
ICON="$ROOT/assets/macos/AnityIcon.icns"
NATIVE_LIBRARY="$ROOT/anity-native/build/libanity_native.dylib"
NATIVE_BUILD_DIR="$ROOT/anity-native/build"
METADATA_FIXUPS_PROJECT="$ROOT/_scripts/Anity.MetadataFixups/Anity.MetadataFixups.csproj"
STAGING_ROOT=""

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

cleanup_build_artifacts() {
  [[ -n "$STAGING_ROOT" ]] && remove_directory "$STAGING_ROOT"
  remove_directory "$PUBLISH_DIR"
  remove_directory "$RESTORE_LOCK_DIR"
  remove_directory "$NATIVE_BUILD_DIR"
}

require() {
  command -v "$1" >/dev/null 2>&1 || {
    echo "ERROR: $1 is required" >&2
    exit 1
  }
}

[[ "$(uname -s)" == "Darwin" ]] || { echo "ERROR: macOS is required" >&2; exit 1; }
[[ "$(uname -m)" == "arm64" ]] || { echo "ERROR: native arm64 host is required; Rosetta installation is rejected" >&2; exit 1; }
[[ -w "$INSTALL_ROOT" ]] || { echo "ERROR: installation directory is not writable: $INSTALL_ROOT" >&2; exit 1; }

require dotnet
require codesign
require file
require plutil
[[ -f "$TEMPLATE" ]] || { echo "ERROR: missing bundle template: $TEMPLATE" >&2; exit 1; }
[[ -f "$ICON" ]] || { echo "ERROR: missing app icon: $ICON" >&2; exit 1; }

bash "$ROOT/_scripts/build-native.sh" "$CONFIG"
[[ -f "$NATIVE_LIBRARY" ]] || { echo "ERROR: missing arm64 native library: $NATIVE_LIBRARY" >&2; exit 1; }

trap cleanup_build_artifacts EXIT
remove_directory "$PUBLISH_DIR"
remove_directory "$RESTORE_LOCK_DIR"
mkdir -p "$RESTORE_LOCK_DIR"
LOCK_FILE_PATTERN="$RESTORE_LOCK_DIR/\$(MSBuildProjectName).packages.lock.json"
dotnet build "$METADATA_FIXUPS_PROJECT" --configuration "$CONFIG" --nologo --verbosity quiet --disable-build-servers -m:1 -clp:ErrorsOnly
dotnet publish "$ROOT/anity-editor/src/Anity.Editor.Host/Anity.Editor.Host.csproj" \
  -c "$CONFIG" -r osx-arm64 --self-contained true --nologo --disable-build-servers -m:1 -clp:ErrorsOnly \
  -p:PublishSingleFile=false -p:SkipUnityMetadataFixups=true \
  -p:RestorePackagesWithLockFile=true -p:NuGetLockFilePath="$LOCK_FILE_PATTERN" \
  -o "$PUBLISH_DIR"

EDITOR_BINARY="$PUBLISH_DIR/Anity.Editor.Host"
[[ -x "$EDITOR_BINARY" ]] || { echo "ERROR: publish did not create executable: $EDITOR_BINARY" >&2; exit 1; }

STAGING_ROOT="$(mktemp -d "$ROOT/build/anity-macos-arm64.XXXXXX")"
STAGING_DIR="$STAGING_ROOT/Anity.app"
mkdir -p "$STAGING_DIR/Contents/MacOS" "$STAGING_DIR/Contents/Resources"
# APFS clone copies retain the atomic staging transaction while avoiding a full
# second self-contained runtime allocation on constrained developer machines.
cp -c "$TEMPLATE" "$STAGING_DIR/Contents/Info.plist"
cp -c "$ICON" "$STAGING_DIR/Contents/Resources/AnityIcon.icns"
cp -cR "$PUBLISH_DIR/." "$STAGING_DIR/Contents/MacOS/"
cp -c "$NATIVE_LIBRARY" "$STAGING_DIR/Contents/MacOS/libanity_native.dylib"

plutil -lint "$STAGING_DIR/Contents/Info.plist"
[[ "$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$STAGING_DIR/Contents/Info.plist")" == "com.anity.engine.editor" ]]
[[ "$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIconFile' "$STAGING_DIR/Contents/Info.plist")" == "AnityIcon" ]]
file "$STAGING_DIR/Contents/MacOS/Anity.Editor.Host" | grep -q 'arm64'
file "$STAGING_DIR/Contents/MacOS/libanity_native.dylib" | grep -q 'arm64'

codesign --force --deep --sign - "$STAGING_DIR"
codesign --verify --deep --strict --verbose=2 "$STAGING_DIR"
"$STAGING_DIR/Contents/MacOS/Anity.Editor.Host" --help | grep -q 'Anity Editor Host'
"$STAGING_DIR/Contents/MacOS/Anity.Editor.Host" menu list | grep -q 'File/'

remove_directory "$BUNDLE_DIR"
mv "$STAGING_DIR" "$BUNDLE_DIR"
rmdir "$STAGING_ROOT"

codesign --verify --deep --strict --verbose=2 "$BUNDLE_DIR"
"$BUNDLE_DIR/Contents/MacOS/Anity.Editor.Host" --help | grep -q 'Anity Editor Host'
"$BUNDLE_DIR/Contents/MacOS/Anity.Editor.Host" menu list | grep -q 'File/'

echo "Anity arm64 app installed and verified: $BUNDLE_DIR"
