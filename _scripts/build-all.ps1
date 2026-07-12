# Build native + all C# projects
param(
  [ValidateSet("Debug", "Release")]
  [string]$Config = "Debug"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

Write-Host "=== build-all ($Config) ===" -ForegroundColor Cyan

if (Get-Command cmake -EA SilentlyContinue) {
  try {
    & "$PSScriptRoot\build-native.ps1" -Config $(if ($Config -eq "Debug") { "Debug" } else { "Release" })
  } catch {
    Write-Warning "Native build failed (managed still builds): $_"
  }
} else {
  Write-Warning "cmake missing — skipping native"
}

$projects = @(
  "anity-lib-core\src\Anity.Core\Anity.Core.csproj",
  "anity-agent\src\Anity.Agent\Anity.Agent.csproj",
  "anity-cli\src\Anity.Cli\Anity.Cli.csproj",
  "anity-webgl\src\Anity.WebGL\Anity.WebGL.csproj",
  "anity-hub\src\Anity.Hub\Anity.Hub.csproj",
  "anity-editor\src\Anity.Editor.Host\Anity.Editor.Host.csproj",
  "samples\URP3DDemo\URP3DDemo.csproj"
)

foreach ($p in $projects) {
  $full = Join-Path $Root $p
  if (-not (Test-Path $full)) {
    Write-Warning "skip missing $p"
    continue
  }
  Write-Host "dotnet build $p" -ForegroundColor Cyan
  dotnet build $full -c $Config --nologo
  if ($LASTEXITCODE -ne 0) { throw "build failed: $p" }
}

Write-Host "=== build-all OK ===" -ForegroundColor Green
