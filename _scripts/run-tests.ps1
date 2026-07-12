# Run Anity deep test suites (≥10 cases per feature modules)
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

$projects = @(
  "anity-lib-core\tests\Anity.Core.Tests\Anity.Core.Tests.csproj",
  "anity-agent\tests\Anity.Agent.Tests\Anity.Agent.Tests.csproj",
  "anity-cli\tests\Anity.Cli.Tests\Anity.Cli.Tests.csproj",
  "anity-lib-core\tests\Anity.AB.Compare.Tests\Anity.AB.Compare.Tests.csproj"
)

$failed = 0
foreach ($p in $projects) {
  $full = Join-Path $Root $p
  if (-not (Test-Path $full)) {
    Write-Warning "missing $p"
    continue
  }
  Write-Host "dotnet test $p" -ForegroundColor Cyan
  dotnet test $full -c Debug --nologo --verbosity minimal
  if ($LASTEXITCODE -ne 0) { $failed++ }
}

if ($failed -gt 0) {
  Write-Host "run-tests: $failed project(s) failed" -ForegroundColor Red
  exit 1
}
Write-Host "run-tests: all passed" -ForegroundColor Green
