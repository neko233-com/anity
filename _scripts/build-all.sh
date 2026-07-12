#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONFIG="${1:-Debug}"
cd "$ROOT"
echo "=== build-all ($CONFIG) ==="
if command -v cmake >/dev/null; then
  bash "$ROOT/_scripts/build-native.sh" "$CONFIG" || echo "WARN: native build failed"
fi
for p in \
  anity-lib-core/src/Anity.Core/Anity.Core.csproj \
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
