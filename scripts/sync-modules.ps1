param(
  [string[]]$Modules = @("anity-hub","anity-editor","anity-lib-core")
)

foreach ($m in $Modules) {
  $path = Join-Path "modules" $m
  if (-not (Test-Path $path)) {
    Write-Warning "$m missing"
    continue
  }
  Write-Host "==> pull $m"
  Push-Location $path
  git pull --rebase
  Pop-Location
}
