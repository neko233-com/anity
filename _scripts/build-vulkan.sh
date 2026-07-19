#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONFIG="${1:-Release}"
NATIVE_SOURCE="$ROOT/anity-native"
VULKAN_BUILD="$NATIVE_SOURCE/build-vulkan"
TEST_PROJECT="$ROOT/anity-lib-core/tests/Anity.Core.Tests/Anity.Core.Tests.csproj"
TEST_OUTPUT="$ROOT/anity-lib-core/tests/Anity.Core.Tests/bin/$CONFIG/net10.0"

command -v cmake >/dev/null || { echo "cmake required"; exit 1; }
command -v dotnet >/dev/null || { echo "dotnet required"; exit 1; }

cmake -S "$NATIVE_SOURCE" -B "$VULKAN_BUILD" \
  -DCMAKE_BUILD_TYPE="$CONFIG" \
  -DANITY_NATIVE_SHARED=ON \
  -DANITY_ENABLE_HDR=ON \
  -DANITY_ENABLE_VULKAN=ON
cmake --build "$VULKAN_BUILD" --config "$CONFIG" \
  -j"$(nproc 2>/dev/null || sysctl -n hw.ncpu 2>/dev/null || echo 4)"

dotnet build "$TEST_PROJECT" -c "$CONFIG" --no-restore -p:AnityRequireNative=true
for library_name in libanity_native.dylib libanity_native.0.dylib libanity_native.0.1.0.dylib; do
  if [[ -f "$VULKAN_BUILD/libanity_native.0.1.0.dylib" ]]; then
    cp "$VULKAN_BUILD/libanity_native.0.1.0.dylib" "$TEST_OUTPUT/$library_name"
  fi
done

ANITY_REQUIRE_VULKAN=1 dotnet test "$TEST_PROJECT" -c "$CONFIG" --no-build \
  -p:AnityRequireNative=true \
  --filter 'FullyQualifiedName~VulkanCamera' --verbosity quiet
