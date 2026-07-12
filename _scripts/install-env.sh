#!/usr/bin/env bash
# Anity full dev environment installer (Linux / macOS)
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
echo "=== Anity env install ==="
echo "Root: $ROOT"

have() { command -v "$1" >/dev/null 2>&1; }

if have dotnet; then
  echo "[OK] dotnet: $(dotnet --version)"
else
  echo "[..] Install .NET 10 SDK: https://dotnet.microsoft.com/download"
fi

if have cmake; then
  echo "[OK] cmake: $(cmake --version | head -1)"
else
  if have brew; then brew install cmake
  elif have apt-get; then sudo apt-get update && sudo apt-get install -y cmake build-essential
  elif have dnf; then sudo dnf install -y cmake gcc-c++ make
  else echo "Install cmake manually"; fi
fi

if [[ "$(uname)" == "Darwin" ]]; then
  if ! xcode-select -p >/dev/null 2>&1; then
    echo "[..] Installing Xcode CLT..."
    xcode-select --install || true
  else
    echo "[OK] Xcode CLT"
  fi
fi

if have vulkaninfo || [[ -n "${VULKAN_SDK:-}" ]]; then
  echo "[OK] Vulkan env"
else
  echo "[--] Vulkan SDK optional: https://vulkan.lunarg.com/"
fi

bash "$ROOT/_scripts/verify-env.sh"
echo "=== install-env done ==="
echo "Next: _scripts/build-all.sh"
