# Build anity-native C++ engine
param(
  [ValidateSet("Debug", "Release")]
  [string]$Config = "Release",
  [string]$Generator = ""
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Src = Join-Path $Root "anity-native"
$Build = Join-Path $Src "build"

if (-not (Get-Command cmake -EA SilentlyContinue)) {
  throw "cmake not found. Run _scripts/install-env.ps1 first."
}

New-Item -ItemType Directory -Force -Path $Build | Out-Null

$cmakeArgs = @("-S", $Src, "-B", $Build, "-DANITY_NATIVE_SHARED=ON", "-DANITY_ENABLE_HDR=ON")
if ($Generator) { $cmakeArgs += @("-G", $Generator) }

Write-Host "cmake configure..." -ForegroundColor Cyan
& cmake @cmakeArgs
if ($LASTEXITCODE -ne 0) { throw "cmake configure failed" }

Write-Host "cmake build ($Config)..." -ForegroundColor Cyan
& cmake --build $Build --config $Config
if ($LASTEXITCODE -ne 0) { throw "cmake build failed" }

Write-Host "anity-native build OK → $Build" -ForegroundColor Green
Get-ChildItem $Build -Recurse -Include "anity_native.dll","anity_native.lib","libanity_native.*" -ErrorAction SilentlyContinue |
  ForEach-Object { Write-Host "  $($_.FullName)" }

# Copy next to managed outputs for P/Invoke
$dll = Get-ChildItem $Build -Recurse -Filter "anity_native.dll" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($dll) {
  $targets = @(
    "$Root\anity-lib-core\src\Anity.Core\bin\Debug",
    "$Root\anity-lib-core\src\Anity.Core\bin\Release",
    "$Root\anity-editor\src\Anity.Editor.Host\bin\Debug\net10.0",
    "$Root\anity-editor\src\Anity.Editor.Host\bin\Release\net10.0",
    "$Root\samples\URP3DDemo\bin\Debug\net10.0"
  )
  foreach ($t in $targets) {
    if (Test-Path $t) {
      Copy-Item -Force $dll.FullName $t
      Write-Host "  copied → $t"
    }
  }
}
