#!/usr/bin/env bash
set -euo pipefail

SOURCE_ROOT="${1:-.}"
FAIL=${2:-1}

declare -A UNSUPPORTED=(
  ["AssetDatabase"]="Not in shim yet"
  ["UnityEditor"]="Editor-only API"
  ["EditorWindow"]="Editor-only API"
  ["EditorGUI"]="Editor-only API"
  ["EditorGUILayout"]="Editor-only API"
  ["SerializedObject"]="Editor-only API"
  ["SerializedProperty"]="Editor-only API"
  ["Undo"]="Editor-only API"
  ["Handles"]="Editor-only API"
  ["MeshRenderer"]="Rendering component not shimmed"
  ["Animator"]="Animation component not shimmed"
  ["RenderTexture"]="Rendering API not shimmed"
  ["Shader"]="Shader API not shimmed"
  ["Texture2D"]="Texture API not shimmed"
)

status=0
tmp=$(mktemp)
trap 'rm -f "$tmp"' EXIT

for pattern in "${!UNSUPPORTED[@]}"; do
  while IFS= read -r line; do
    [ -z "$line" ] && continue
    status=1
    printf "%s\\n" "$line" >> "$tmp"
  done < <(rg -n -S --glob "*.cs" "\\b${pattern}\\b" "$SOURCE_ROOT" || true)
done

if [ -f "$tmp" ] && [ -s "$tmp" ]; then
  echo "compat audit: hits found"
  echo "--------------------------------"
  cat "$tmp"
  if [ "$FAIL" = "1" ] || [ "${FAIL,,}" = "true" ]; then
    exit 1
  fi
fi

if [ "$status" -eq 0 ]; then
  echo "compat audit: no unsupported API hits"
fi
exit 0
