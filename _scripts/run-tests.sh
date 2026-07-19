#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONFIG="${1:-Release}"
cd "$ROOT"

# Native-backed suites must never silently fall back to managed-only skips.
# The project property copies the freshly built runtime beside the test host;
# the environment variable makes a missing/invalid runtime a hard assertion.
export ANITY_REQUIRE_NATIVE=1

# The CLI gate validates a real, host-matching self-contained distribution.
bash "$ROOT/_scripts/publish-cli.sh" "$CONFIG"
case "$(uname -s)/$(uname -m)" in
  Darwin/arm64) ANITY_CLI_RID="osx-arm64" ;;
  Darwin/x86_64) ANITY_CLI_RID="osx-x64" ;;
  Linux/aarch64|Linux/arm64) ANITY_CLI_RID="linux-arm64" ;;
  Linux/x86_64) ANITY_CLI_RID="linux-x64" ;;
  *) echo "ERROR: unsupported CLI test host" >&2; exit 1 ;;
esac
export ANITY_CLI_DISTRIBUTION_DIR="$ROOT/build/cli/$ANITY_CLI_RID"

projects=(
  "anity-lib-core/tests/Anity.Core.Tests/Anity.Core.Tests.csproj"
  "anity-agent/tests/Anity.Agent.Tests/Anity.Agent.Tests.csproj"
  "anity-editor/tests/Anity.Editor.Host.Tests/Anity.Editor.Host.Tests.csproj"
  "anity-shader-graph/tests/Unity.ShaderGraph.Editor.Tests/Unity.ShaderGraph.Editor.Tests.csproj"
  "anity-vfx-graph/tests/Unity.VisualEffectGraph.Editor.Tests/Unity.VisualEffectGraph.Editor.Tests.csproj"
  "anity-cli/tests/Anity.Cli.Tests/Anity.Cli.Tests.csproj"
  "_scripts/UnityApiParity.Tests/UnityApiParity.Tests.csproj"
  "anity-lib-core/tests/Anity.AB.Compare.Tests/Anity.AB.Compare.Tests.csproj"
)

for project in "${projects[@]}"; do
  if [[ ! -f "$project" ]]; then
    echo "ERROR: missing test project $project" >&2
    exit 1
  fi
  echo "dotnet test $project ($CONFIG)"
  dotnet test "$project" -c "$CONFIG" --nologo --verbosity minimal \
    -p:AnityRequireNative=true
done

echo "=== run-tests OK ==="
