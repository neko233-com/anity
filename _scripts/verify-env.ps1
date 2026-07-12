# Verify Anity toolchain
$ErrorActionPreference = "Continue"
$Root = Split-Path -Parent $PSScriptRoot
$fail = 0

function Check($name, $ok, $hint) {
  if ($ok) { Write-Host "[OK] $name" -ForegroundColor Green }
  else { Write-Host "[FAIL] $name — $hint" -ForegroundColor Red; $script:fail++ }
}

Write-Host "=== verify-env ===" -ForegroundColor Cyan
Check "dotnet" ([bool](Get-Command dotnet -EA SilentlyContinue)) "Install .NET 10 SDK"
if (Get-Command dotnet -EA SilentlyContinue) {
  $v = dotnet --version
  Write-Host "     version $v"
}
Check "cmake" ([bool](Get-Command cmake -EA SilentlyContinue)) "Install CMake 3.20+"
Check "git" ([bool](Get-Command git -EA SilentlyContinue)) "Install git"
Check "anity-native sources" (Test-Path "$Root\anity-native\CMakeLists.txt") "Missing anity-native"
Check "anity-lib-core" (Test-Path "$Root\anity-lib-core\src\Anity.Core\Anity.Core.csproj") "Missing core"
Check "AGENTS.md" (Test-Path "$Root\AGENTS.md") "Missing AGENTS.md"

$nativeLib = @(
  "$Root\anity-native\build\Release\anity_native.dll",
  "$Root\anity-native\build\Debug\anity_native.dll",
  "$Root\anity-native\build\anity_native.dll"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if ($nativeLib) { Write-Host "[OK] native built: $nativeLib" -ForegroundColor Green }
else { Write-Host "[--] native not built yet (run _scripts/build-native.ps1)" -ForegroundColor Yellow }

if ($fail -gt 0) {
  Write-Host "verify-env: $fail failure(s)" -ForegroundColor Red
  exit 1
}
Write-Host "verify-env: all required checks passed" -ForegroundColor Green
exit 0
