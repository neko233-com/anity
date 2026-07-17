param(
  [string]$UnityManagedPath = "",
  [string]$ReportPath = "",
  [string]$BaselinePath = "",
  [switch]$ResetBaseline
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$UnityLabel = ""
if (-not $ReportPath) { $ReportPath = Join-Path $Root "parity-evidence\unity-api-parity-current.json" }
if (-not $BaselinePath) { $BaselinePath = Join-Path $Root "parity-evidence\unity-api-parity-baseline.json" }

if (-not $UnityManagedPath) {
  $hub = Join-Path $env:ProgramFiles "Unity\Hub\Editor"
  if (Test-Path $hub) {
    $editor = Get-ChildItem $hub -Directory |
      Where-Object { $_.Name -like "2022.3.*" } |
      Sort-Object Name -Descending |
      Select-Object -First 1
    if ($editor) {
      $UnityManagedPath = Join-Path $editor.FullName "Editor\Data\Managed\UnityEngine"
      $UnityLabel = "Unity $($editor.Name)"
    }
  }
}

if (-not $UnityManagedPath -or -not (Test-Path $UnityManagedPath)) {
  throw "Unity 2022.3 managed directory not found. Pass -UnityManagedPath."
}
if (-not $UnityLabel) {
  if ($UnityManagedPath -match '[\\/]Editor[\\/](2022\.3\.[^\\/]+)[\\/]') {
    $UnityLabel = "Unity $($Matches[1])"
  } else {
    $UnityLabel = "Unity 2022.3"
  }
}

$coreProject = Join-Path $Root "anity-lib-core\src\Anity.Core\Anity.Core.csproj"
$anityAssembly = Join-Path $Root "anity-lib-core\src\Anity.Core\bin\Release\netstandard2.1\Anity.Core.dll"
$toolProject = Join-Path $Root "_scripts\UnityApiParity\UnityApiParity.csproj"

dotnet build $coreProject -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$parityArgs = @(
  "--unity-dir", $UnityManagedPath,
  "--anity", $anityAssembly,
  "--unity-label", $UnityLabel,
  "--json", $ReportPath
)
if ((Test-Path $BaselinePath) -and -not $ResetBaseline) {
  $parityArgs += @("--baseline", $BaselinePath, "--fail-on-regression")
} else {
  $parityArgs += @("--write-baseline", $BaselinePath)
}
dotnet run --project $toolProject -c Release -- @parityArgs
exit $LASTEXITCODE
