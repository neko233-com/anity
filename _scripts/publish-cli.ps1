# Publish a directly runnable, self-contained Anity CLI for the native Windows host.
param(
  [ValidateSet("Debug", "Release")]
  [string]$Config = "Release",
  [ValidateSet("", "win-x64", "win-arm64")]
  [string]$RuntimeIdentifier = ""
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
$HostRid = switch ($Arch) {
  ([System.Runtime.InteropServices.Architecture]::X64) { "win-x64" }
  ([System.Runtime.InteropServices.Architecture]::Arm64) { "win-arm64" }
  default { throw "unsupported Windows CLI publish architecture: $Arch" }
}
$Rid = if ($RuntimeIdentifier) { $RuntimeIdentifier } else { $HostRid }
if ($Rid -ne $HostRid) {
  throw "$Rid cannot use the native runtime built for $HostRid"
}

$BuildRoot = [System.IO.Path]::GetFullPath((Join-Path $Root "build\cli"))
$OutputDir = [System.IO.Path]::GetFullPath((Join-Path $BuildRoot $Rid))
if (-not $OutputDir.StartsWith($BuildRoot + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
  throw "CLI output must stay under $BuildRoot"
}

function Remove-OutputDirectory([string]$Path) {
  if (-not (Test-Path -LiteralPath $Path)) { return }
  $Item = Get-Item -LiteralPath $Path -Force
  if (-not $Item.PSIsContainer -or $Item.LinkType) {
    throw "refusing to remove non-directory artifact: $Path"
  }
  Remove-Item -LiteralPath $Path -Recurse -Force
}

foreach ($Command in @("dotnet", "cmake")) {
  if (-not (Get-Command $Command -ErrorAction SilentlyContinue)) {
    throw "$Command is required"
  }
}

New-Item -ItemType Directory -Force -Path $BuildRoot | Out-Null
$StagingDir = Join-Path $BuildRoot ("anity-cli-{0}-{1}" -f $Rid, [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $StagingDir | Out-Null
$LockRoot = Join-Path $StagingDir "locks"
New-Item -ItemType Directory -Path $LockRoot | Out-Null
$LockFilePattern = Join-Path $LockRoot '$(MSBuildProjectName).packages.lock.json'

try {
  & "$PSScriptRoot\build-native.ps1" -Config $Config
  if ($LASTEXITCODE -ne 0) { throw "native build failed" }

  dotnet build "$PSScriptRoot\Anity.MetadataFixups\Anity.MetadataFixups.csproj" `
    --configuration $Config --nologo --verbosity quiet --disable-build-servers `
    -m:1 -clp:ErrorsOnly
  if ($LASTEXITCODE -ne 0) { throw "metadata fixups build failed" }

  dotnet publish "$Root\anity-cli\src\Anity.Cli\Anity.Cli.csproj" `
    --configuration $Config --runtime $Rid --self-contained true `
    --nologo --disable-build-servers -m:1 -clp:ErrorsOnly `
    -p:PublishSingleFile=false -p:UseAppHost=true `
    -p:DebugSymbols=false -p:DebugType=None `
    -p:SkipUnityMetadataFixups=true `
    -p:RestorePackagesWithLockFile=true `
    "-p:NuGetLockFilePath=$LockFilePattern" `
    -o $StagingDir
  if ($LASTEXITCODE -ne 0) { throw "CLI publish failed" }

  $Cli = Join-Path $StagingDir "anity.exe"
  if (-not (Test-Path -LiteralPath $Cli -PathType Leaf)) {
    throw "self-contained publish did not create $Cli"
  }

  $Native = Get-ChildItem "$Root\anity-native\build" -Recurse -Filter "anity_native.dll" |
    Select-Object -First 1
  if (-not $Native) { throw "native runtime anity_native.dll is missing" }
  Copy-Item -LiteralPath $Native.FullName -Destination (Join-Path $StagingDir "anity_native.dll") -Force

  $SmokeDir = Join-Path $BuildRoot ("anity-cli-smoke-" + [Guid]::NewGuid().ToString("N"))
  New-Item -ItemType Directory -Path $SmokeDir | Out-Null
  try {
    $Start = [Diagnostics.ProcessStartInfo]::new($Cli)
    $Start.WorkingDirectory = $SmokeDir
    $Start.ArgumentList.Add("-batchmode")
    $Start.ArgumentList.Add("-quit")
    $Start.ArgumentList.Add("-nographics")
    $Start.ArgumentList.Add("-logFile")
    $Start.ArgumentList.Add("-")
    $Start.UseShellExecute = $false
    $Start.RedirectStandardOutput = $true
    $Start.RedirectStandardError = $true
    $Start.Environment.Remove("DOTNET_ROOT")
    $Start.Environment.Remove("DOTNET_ROOT_X64")
    $Start.Environment.Remove("DOTNET_ROOT_ARM64")
    $Start.Environment["DOTNET_ROOT"] = "C:\nonexistent\anity-dotnet-root"
    $Start.Environment["DOTNET_MULTILEVEL_LOOKUP"] = "0"
    $Process = [Diagnostics.Process]::Start($Start)
    $Stdout = $Process.StandardOutput.ReadToEnd()
    $Stderr = $Process.StandardError.ReadToEnd()
    $Process.WaitForExit()
    if ($Process.ExitCode -ne 0) { throw "CLI smoke failed ($($Process.ExitCode)): $Stderr" }
    foreach ($Expected in @("batchmode=1", "nographics=1", "quit=1")) {
      if ($Stdout -notmatch "(?m)^$([Regex]::Escape($Expected))$") {
        throw "CLI smoke output is missing $Expected"
      }
    }
    if (Test-Path -LiteralPath (Join-Path $SmokeDir "-")) {
      throw "-logFile - created a dash file"
    }
  }
  finally {
    Remove-OutputDirectory $SmokeDir
  }

  Remove-OutputDirectory $OutputDir
  Move-Item -LiteralPath $StagingDir -Destination $OutputDir
  Write-Host "Anity self-contained CLI published and verified: $OutputDir" -ForegroundColor Green
}
finally {
  Remove-OutputDirectory $StagingDir
}
