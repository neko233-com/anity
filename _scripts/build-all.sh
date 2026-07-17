#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONFIG="${1:-Debug}"
cd "$ROOT"
echo "=== build-all ($CONFIG) ==="
command -v cmake >/dev/null || {
  echo "ERROR: cmake is required for the production build-all gate; run _scripts/install-env.sh" >&2
  exit 1
}
bash "$ROOT/_scripts/build-native.sh" "$CONFIG"
for p in \
  anity-lib-core/src/Anity.Core/Anity.Core.csproj \
  anity-agent/src/Anity.Agent/Anity.Agent.csproj \
  anity-shader-graph/src/Unity.ShaderGraph.Editor/Unity.ShaderGraph.Editor.csproj \
  anity-vfx-graph/src/Unity.VisualEffectGraph.Editor/Unity.VisualEffectGraph.Editor.csproj \
  anity-cli/src/Anity.Cli/Anity.Cli.csproj \
  _scripts/UnityApiParity/UnityApiParity.csproj \
  anity-webgl/src/Anity.WebGL/Anity.WebGL.csproj \
  anity-hub/src/Anity.Hub/Anity.Hub.csproj \
  anity-editor/src/Anity.Editor.Host/Anity.Editor.Host.csproj \
  samples/URP3DDemo/URP3DDemo.csproj
do
  [[ -f "$p" ]] || continue
  echo "dotnet build $p"
  dotnet build "$p" -c "$CONFIG" --nologo
done
echo "=== build-all OK ==="
