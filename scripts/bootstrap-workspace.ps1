param(
  [Parameter(Mandatory=$true)]
  [string]$Owner,

  [string]$MainRepo = "anity",
  [string[]]$Modules = @("anity-hub", "anity-editor", "anity-lib-core")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$gitProtocol = gh config get git_protocol
if (-not $gitProtocol) { $gitProtocol = "https" }

if (!(Test-Path ".git")) {
  git init -b main | Out-Null
  if ($gitProtocol -eq "ssh") {
    git remote add origin "git@github.com:$Owner/$MainRepo.git"
  } else {
    git remote add origin "https://github.com/$Owner/$MainRepo.git"
  }
}

if (!(Test-Path "modules")) { New-Item -ItemType Directory -Path "modules" | Out-Null }

foreach ($m in $Modules) {
  $path = "modules/$m"
  if (Test-Path $path) { continue }

  if ($gitProtocol -eq "ssh") {
    $url = "git@github.com:$Owner/$m.git"
  } else {
    $url = "https://github.com/$Owner/$m.git"
  }
  git submodule add $url $path
}

git add .
git commit -m "chore: initialize anity workspace with module submodules" | Out-Null
Write-Host "Workspace bootstrapped: modules => $($Modules -join ', ')"
