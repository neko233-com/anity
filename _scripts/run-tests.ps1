# Run Anity deep test suites (≥10 cases per feature modules)
param(
  [ValidateSet("Debug", "Release")]
  [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root
$env:ANITY_REQUIRE_NATIVE = "1"

# The CLI gate validates a real, host-matching self-contained distribution.
& "$PSScriptRoot\publish-cli.ps1" -Config $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$CliRid = switch ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture) {
  ([System.Runtime.InteropServices.Architecture]::X64) { "win-x64" }
  ([System.Runtime.InteropServices.Architecture]::Arm64) { "win-arm64" }
  default { throw "unsupported CLI test architecture" }
}
$env:ANITY_CLI_DISTRIBUTION_DIR = Join-Path $Root "build\cli\$CliRid"

$projects = @(
  "anity-lib-core\tests\Anity.Core.Tests\Anity.Core.Tests.csproj",
  "anity-agent\tests\Anity.Agent.Tests\Anity.Agent.Tests.csproj",
  "anity-editor\tests\Anity.Editor.Host.Tests\Anity.Editor.Host.Tests.csproj",
  "anity-shader-graph\tests\Unity.ShaderGraph.Editor.Tests\Unity.ShaderGraph.Editor.Tests.csproj",
  "anity-vfx-graph\tests\Unity.VisualEffectGraph.Editor.Tests\Unity.VisualEffectGraph.Editor.Tests.csproj",
  "anity-cli\tests\Anity.Cli.Tests\Anity.Cli.Tests.csproj",
  "_scripts\UnityApiParity.Tests\UnityApiParity.Tests.csproj",
  "anity-lib-core\tests\Anity.AB.Compare.Tests\Anity.AB.Compare.Tests.csproj"
)

foreach ($p in $projects) {
  $full = Join-Path $Root $p
  if (-not (Test-Path $full)) {
    throw "Missing test project: $p"
  }
  Write-Host "dotnet test $p ($Configuration)" -ForegroundColor Cyan
  dotnet test $full -c $Configuration --nologo --verbosity minimal `
    -p:AnityRequireNative=true
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "run-tests: all passed" -ForegroundColor Green
