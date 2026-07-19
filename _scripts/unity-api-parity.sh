#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY_DIR="${1:-}"
UNITY_LABEL="${UNITY_LABEL:-}"
UNITY_VERSION="${UNITY_EDITOR_VERSION:-2022.3.61f1}"
REPORT="${2:-$ROOT/parity-evidence/unity-api-parity-current.json}"
BASELINE="${3:-$ROOT/parity-evidence/unity-api-parity-baseline.json}"

if [[ -z "$UNITY_DIR" && -d "/Applications/Unity/Hub/Editor" ]]; then
  UNITY_DIR="/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/Managed/UnityEngine"
fi

if [[ -z "$UNITY_DIR" || ! -d "$UNITY_DIR" ]]; then
  echo "Unity $UNITY_VERSION managed directory not found. Install that exact Editor version or pass its Managed/UnityEngine directory as the first argument." >&2
  exit 2
fi

if [[ "${ANITY_ALLOW_NON_TARGET_UNITY:-0}" != "1" && "$UNITY_DIR" != *"/Editor/$UNITY_VERSION/"* ]]; then
  echo "Refusing non-target Unity evidence: expected $UNITY_VERSION, got $UNITY_DIR. Set ANITY_ALLOW_NON_TARGET_UNITY=1 only for non-final exploratory comparisons." >&2
  exit 2
fi

if [[ -z "$UNITY_LABEL" ]]; then
  UNITY_LABEL="Unity $UNITY_VERSION"
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
