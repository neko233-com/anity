#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SHADER_DIR="$ROOT/anity-native/src/graphics/shaders"
GLSLC_BIN="${GLSLC:-}"
if [[ -z "$GLSLC_BIN" ]] && command -v glslc >/dev/null 2>&1; then
  GLSLC_BIN="$(command -v glslc)"
fi
if [[ -z "$GLSLC_BIN" && -n "${ANDROID_NDK_HOME:-}" ]]; then
  for candidate in "$ANDROID_NDK_HOME"/shader-tools/*/glslc; do
    if [[ -x "$candidate" ]]; then GLSLC_BIN="$candidate"; break; fi
  done
fi
if [[ -z "$GLSLC_BIN" || ! -x "$GLSLC_BIN" ]]; then
  echo "glslc not found; set GLSLC or ANDROID_NDK_HOME" >&2
  exit 1
fi

VERT_SPV="$(mktemp)"
FRAG_SPV="$(mktemp)"
DEPTH_COPY_SPV="$(mktemp)"
DEPTH_COPY_MSAA_SPV="$(mktemp)"
CAMERA_MESH_VERT_SPV="$(mktemp)"
CAMERA_MESH_FRAG_SPV="$(mktemp)"
OUTPUT_TMP="$(mktemp)"
DEPTH_OUTPUT_TMP="$(mktemp)"
DEPTH_MSAA_OUTPUT_TMP="$(mktemp)"
CAMERA_MESH_OUTPUT_TMP="$(mktemp)"
trap 'rm -f "$VERT_SPV" "$FRAG_SPV" "$DEPTH_COPY_SPV" "$DEPTH_COPY_MSAA_SPV" "$CAMERA_MESH_VERT_SPV" "$CAMERA_MESH_FRAG_SPV" "$OUTPUT_TMP" "$DEPTH_OUTPUT_TMP" "$DEPTH_MSAA_OUTPUT_TMP" "$CAMERA_MESH_OUTPUT_TMP"' EXIT
"$GLSLC_BIN" -O "$SHADER_DIR/anity_ui.vert" -o "$VERT_SPV"
"$GLSLC_BIN" -O "$SHADER_DIR/anity_ui.frag" -o "$FRAG_SPV"
"$GLSLC_BIN" -O "$SHADER_DIR/anity_depth_copy.comp" -o "$DEPTH_COPY_SPV"
"$GLSLC_BIN" -O "$SHADER_DIR/anity_depth_copy_msaa.comp" -o "$DEPTH_COPY_MSAA_SPV"
"$GLSLC_BIN" -O "$SHADER_DIR/anity_camera_mesh.vert" -o "$CAMERA_MESH_VERT_SPV"
"$GLSLC_BIN" -O "$SHADER_DIR/anity_camera_mesh.frag" -o "$CAMERA_MESH_FRAG_SPV"

emit_array() {
  local name="$1"
  local file="$2"
  echo "static constexpr uint32_t $name[] = {"
  od -An -tu4 -v "$file" | awk '
    BEGIN { column = 0 }
    {
      for (i = 1; i <= NF; i++) {
        printf "  %s,", $i
        column++
        if (column == 12) { printf "\n"; column = 0 }
      }
    }
    END { if (column != 0) printf "\n" }
  '
  echo "};"
}

{
  echo "#pragma once"
  echo "#include <cstdint>"
  echo
  echo "/* Generated from anity_ui.vert/frag with glslc -O. */"
  emit_array kAnityUIVertexSpirv "$VERT_SPV"
  echo
  emit_array kAnityUIFragmentSpirv "$FRAG_SPV"
} > "$OUTPUT_TMP"
mv "$OUTPUT_TMP" "$SHADER_DIR/anity_ui_spirv.h"

{
  echo "#pragma once"
  echo "#include <cstdint>"
  echo
  echo "/* Generated from anity_depth_copy.comp with glslc -O. */"
  emit_array kAnityDepthCopyComputeSpirv "$DEPTH_COPY_SPV"
} > "$DEPTH_OUTPUT_TMP"
mv "$DEPTH_OUTPUT_TMP" "$SHADER_DIR/anity_depth_copy_spirv.h"

{
  echo "#pragma once"
  echo "#include <cstdint>"
  echo
  echo "/* Generated from anity_depth_copy_msaa.comp with glslc -O. */"
  emit_array kAnityDepthCopyMsaaComputeSpirv "$DEPTH_COPY_MSAA_SPV"
} > "$DEPTH_MSAA_OUTPUT_TMP"
mv "$DEPTH_MSAA_OUTPUT_TMP" "$SHADER_DIR/anity_depth_copy_msaa_spirv.h"

{
  echo "#pragma once"
  echo "#include <cstdint>"
  echo
  echo "/* Generated from anity_camera_mesh.vert/frag with glslc -O. */"
  emit_array kAnityCameraMeshVertexSpirv "$CAMERA_MESH_VERT_SPV"
  echo
  emit_array kAnityCameraMeshFragmentSpirv "$CAMERA_MESH_FRAG_SPV"
} > "$CAMERA_MESH_OUTPUT_TMP"
mv "$CAMERA_MESH_OUTPUT_TMP" "$SHADER_DIR/anity_camera_mesh_spirv.h"
