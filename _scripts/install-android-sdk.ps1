param([switch]$CheckOnly)
$ErrorActionPreference = "Continue"

$sdk = $env:ANDROID_HOME
if (-not $sdk) { $sdk = $env:ANDROID_SDK_ROOT }
$ndk = $env:ANDROID_NDK_HOME
if (-not $ndk -and $sdk) {
  $ndkDir = Join-Path $sdk "ndk"
  if (Test-Path $ndkDir) {
    $ndk = Get-ChildItem $ndkDir -Directory | Sort-Object Name -Descending | Select-Object -First 1 -ExpandProperty FullName
  }
}

if ($sdk -and (Test-Path $sdk)) {
  Write-Host "[OK] ANDROID_HOME/SDK: $sdk"
} else {
  Write-Host "[--] Android SDK not configured"
  Write-Host "     Install Android Studio or command-line tools"
  Write-Host "     Set ANDROID_HOME / ANDROID_SDK_ROOT"
  if (-not $CheckOnly) {
    Write-Host "     https://developer.android.com/studio"
  }
}

if ($ndk -and (Test-Path $ndk)) {
  Write-Host "[OK] NDK: $ndk"
} else {
  Write-Host "[--] Android NDK not found (required for native Android/Vulkan builds)"
}

if ($CheckOnly) { exit 0 }
