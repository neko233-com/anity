param([switch]$CheckOnly)
$ErrorActionPreference = "Continue"

function HasVulkan {
  if ($env:VULKAN_SDK -and (Test-Path $env:VULKAN_SDK)) { return $true }
  if (Get-Command vulkaninfo -EA SilentlyContinue) { return $true }
  $paths = @(
    "C:\VulkanSDK",
    "${env:ProgramFiles}\VulkanSDK"
  )
  foreach ($p in $paths) {
    if (Test-Path $p) { return $true }
  }
  return $false
}

if (HasVulkan) {
  Write-Host "[OK] Vulkan SDK present (VULKAN_SDK=$env:VULKAN_SDK)"
  exit 0
}

if ($CheckOnly) {
  Write-Host "[--] Vulkan SDK not found (optional for Android/Linux Vulkan backend)"
  Write-Host "     https://vulkan.lunarg.com/sdk/home"
  exit 0
}

Write-Host "Install Vulkan SDK from LunarG: https://vulkan.lunarg.com/sdk/home"
Write-Host "Or: winget search Vulkan"
if (Get-Command winget -EA SilentlyContinue) {
  winget search Vulkan
}
