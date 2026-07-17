#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SRC="$ROOT/anity-native"
BUILD="$SRC/build"
CONFIG="${1:-Release}"

VULKAN_ARG=""
if [[ "$(uname -s)" == "Darwin" ]]; then
  # macOS/iOS/tvOS/visionOS product builds use Metal. Pass this explicitly so
  # an older CMake cache cannot retain a host Android Emulator Vulkan library.
  VULKAN_ARG="-DANITY_ENABLE_VULKAN=OFF"
fi

command -v cmake >/dev/null || { echo "cmake required"; exit 1; }
mkdir -p "$BUILD"
cmake -S "$SRC" -B "$BUILD" -DCMAKE_BUILD_TYPE="$CONFIG" -DANITY_NATIVE_SHARED=ON -DANITY_ENABLE_HDR=ON ${VULKAN_ARG:+"$VULKAN_ARG"}
cmake --build "$BUILD" --config "$CONFIG" -j"$(nproc 2>/dev/null || sysctl -n hw.ncpu 2>/dev/null || echo 4)"
echo "anity-native build OK → $BUILD"
find "$BUILD" -name 'libanity_native*' -o -name 'anity_native.*' 2>/dev/null | head -20
