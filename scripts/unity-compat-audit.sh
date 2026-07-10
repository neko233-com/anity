#!/usr/bin/env bash
set -euo pipefail

SOURCE_ROOT="${1:-.}"
FAIL="${2:-1}"

# Patterns considered "not yet shimmed". The associated reasons are kept
# inline for documentation only; the audit prints the matching source lines.
# Uses a plain indexed array (not declare -A) so the script stays compatible
# with the bash 3.2 shipped on macOS runners.
PATTERNS=(
  AssetDatabase
  UnityEditor
  EditorWindow
  EditorGUI
  EditorGUILayout
  SerializedObject
  SerializedProperty
  Undo
  Handles
  MeshRenderer
  Animator
  RenderTexture
  Shader
  Texture2D
)

status=0
tmp=$(mktemp)
trap 'rm -f "$tmp"' EXIT

for pattern in "${PATTERNS[@]}"; do
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
  fail_lc=$(printf "%s" "$FAIL" | tr '[:upper:]' '[:lower:]')
  if [ "$FAIL" = "1" ] || [ "$fail_lc" = "true" ]; then
    exit 1
  fi
fi

if [ "$status" -eq 0 ]; then
  echo "compat audit: no unsupported API hits"
fi
exit 0
