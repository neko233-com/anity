#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY_DIR="${1:-}"
UNITY_LABEL="${UNITY_LABEL:-}"
REPORT="${2:-$ROOT/parity-evidence/unity-api-parity-current.json}"
BASELINE="${3:-$ROOT/parity-evidence/unity-api-parity-baseline.json}"

if [[ -z "$UNITY_DIR" && -d "/Applications/Unity/Hub/Editor" ]]; then
  EDITOR_ROOT="$(find /Applications/Unity/Hub/Editor -maxdepth 1 -type d -name '2022.3.*' | sort -V | tail -n 1)"
  if [[ -n "$EDITOR_ROOT" ]]; then
    UNITY_DIR="$EDITOR_ROOT/Unity.app/Contents/Managed/UnityEngine"
  fi
fi

if [[ -z "$UNITY_DIR" || ! -d "$UNITY_DIR" ]]; then
  echo "Unity 2022.3 managed directory not found. Pass it as the first argument." >&2
  exit 2
fi

if [[ -z "$UNITY_LABEL" ]]; then
  if [[ "$UNITY_DIR" =~ /Editor/(2022\.3\.[^/]+)/ ]]; then
    UNITY_LABEL="Unity ${BASH_REMATCH[1]}"
  else
    UNITY_LABEL="Unity 2022.3"
  fi
fi

dotnet build "$ROOT/anity-lib-core/src/Anity.Core/Anity.Core.csproj" -c Release --nologo
PARITY_ARGS=(
  --unity-dir "$UNITY_DIR"
  --anity "$ROOT/anity-lib-core/src/Anity.Core/bin/Release/netstandard2.1/Anity.Core.dll"
  --unity-label "$UNITY_LABEL"
  --json "$REPORT"
)
if [[ -f "$BASELINE" && "${RESET_API_PARITY_BASELINE:-0}" != "1" ]]; then
  PARITY_ARGS+=(--baseline "$BASELINE" --fail-on-regression)
else
  PARITY_ARGS+=(--write-baseline "$BASELINE")
fi

dotnet run --project "$ROOT/_scripts/UnityApiParity/UnityApiParity.csproj" -c Release -- "${PARITY_ARGS[@]}"
