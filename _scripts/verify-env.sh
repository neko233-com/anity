#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
fail=0
ok() { echo "[OK] $1"; }
bad() { echo "[FAIL] $1 — $2"; fail=$((fail+1)); }

echo "=== verify-env ==="
command -v dotnet >/dev/null && ok "dotnet $(dotnet --version)" || bad "dotnet" "install SDK 10"
command -v cmake >/dev/null && ok "cmake" || bad "cmake" "install cmake"
command -v git >/dev/null && ok "git" || bad "git" "install git"
[[ -f "$ROOT/anity-native/CMakeLists.txt" ]] && ok "anity-native" || bad "anity-native" "missing"
[[ -f "$ROOT/anity-lib-core/src/Anity.Core/Anity.Core.csproj" ]] && ok "anity-lib-core" || bad "core" "missing"

if ls "$ROOT"/anity-native/build/**/libanity_native.* 2>/dev/null | head -1 | grep -q .; then
  ok "native library present"
else
  echo "[--] native not built (run _scripts/build-native.sh)"
fi

if [[ $fail -gt 0 ]]; then echo "verify-env: $fail failure(s)"; exit 1; fi
echo "verify-env: all required checks passed"
