#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONFIG="${1:-Release}"
cd "$ROOT"

# Native-backed suites must never silently fall back to managed-only skips.
# The project property copies the freshly built runtime beside the test host;
# the environment variable makes a missing/invalid runtime a hard assertion.
export ANITY_REQUIRE_NATIVE=1

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
