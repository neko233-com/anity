param(
  [Parameter(Mandatory=$true)]
  [string]$RepoName,

  [Parameter(Mandatory=$true)]
  [string]$Owner,

  [string]$Branch = "main"
)

$repoDir = Join-Path "modules" $RepoName
if (-not (Test-Path "modules")) { New-Item -ItemType Directory -Path "modules" | Out-Null }

if (Test-Path $repoDir) {
  Write-Host "skip: $repoDir exists"
  return
}

$protocol = gh config get git_protocol
if (-not $protocol) {
  $protocol = gh config get git_protocol
}
if (-not $protocol) { $protocol = "https" }

$url = if ($protocol -eq "ssh") { "git@github.com:$Owner/$RepoName.git" } else { "https://github.com/$Owner/$RepoName.git" }
git submodule add -b $Branch $url $repoDir
