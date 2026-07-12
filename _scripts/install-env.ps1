# Anity full dev environment installer (Windows)
# Usage: powershell -ExecutionPolicy Bypass -File _scripts/install-env.ps1
param(
  [switch]$SkipDotNet,
  [switch]$SkipCMake,
  [switch]$SkipVulkan,
  [switch]$SkipAndroid,
  [switch]$BuildNative
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
if (-not $Root) { $Root = (Get-Location).Path }
Set-Location $Root

Write-Host "=== Anity env install (Unity 2022.3 Pro parity toolchain) ===" -ForegroundColor Cyan
Write-Host "Root: $Root"

function Test-Cmd($name) {
  return [bool](Get-Command $name -ErrorAction SilentlyContinue)
}

# --- .NET 10 / 8 SDK ---
if (-not $SkipDotNet) {
  if (Test-Cmd "dotnet") {
    Write-Host "[OK] dotnet: $(dotnet --version)"
  } else {
    Write-Host "[..] Installing .NET SDK via winget..."
    if (Test-Cmd "winget") {
      winget install --id Microsoft.DotNet.SDK.10 -e --accept-source-agreements --accept-package-agreements
    } else {
      Write-Warning "winget not found. Install .NET 10 SDK from https://dotnet.microsoft.com/download"
    }
  }
}

# --- CMake ---
if (-not $SkipCMake) {
  if (Test-Cmd "cmake") {
    Write-Host "[OK] cmake: $(cmake --version | Select-Object -First 1)"
  } else {
    Write-Host "[..] Installing CMake via winget..."
    if (Test-Cmd "winget") {
      winget install --id Kitware.CMake -e --accept-source-agreements --accept-package-agreements
    } else {
      Write-Warning "Install CMake from https://cmake.org/download/"
    }
  }
}

# --- Visual Studio Build Tools (C++) ---
if (-not (Test-Cmd "cl")) {
  Write-Host "[..] MSVC not on PATH. Prefer Visual Studio 2022 Build Tools with C++ workload."
  Write-Host "     winget install Microsoft.VisualStudio.2022.BuildTools --override `"--add Microsoft.VisualStudio.Workload.VCTools --includeRecommended`""
}

# --- Vulkan SDK ---
if (-not $SkipVulkan) {
  & "$PSScriptRoot\install-vulkan-sdk.ps1" -CheckOnly
}

# --- Android ---
if (-not $SkipAndroid) {
  & "$PSScriptRoot\install-android-sdk.ps1" -CheckOnly
}

# --- Python (optional tools) ---
if (Test-Cmd "python") {
  Write-Host "[OK] python: $(python --version 2>&1)"
} else {
  Write-Host "[--] python optional (gap-audit helpers)"
}

# --- Git ---
if (Test-Cmd "git") {
  Write-Host "[OK] git: $(git --version)"
} else {
  Write-Warning "git missing"
}

Write-Host ""
Write-Host "Running verify-env..."
& "$PSScriptRoot\verify-env.ps1"

if ($BuildNative) {
  & "$PSScriptRoot\build-native.ps1"
}

Write-Host "=== install-env done ===" -ForegroundColor Green
Write-Host "Next: _scripts/build-all.ps1"
