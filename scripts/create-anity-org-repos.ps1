param(
  [Parameter(Mandatory=$true)]
  [string]$Owner,

  [ValidateSet("public","private")]
  [string]$Visibility = "private",

  [switch]$SeedInitialCommit = $true
)

$repos = @("anity", "anity-hub", "anity-editor", "anity-lib-core")
$gitProtocol = gh config get git_protocol
if (-not $gitProtocol) { $gitProtocol = "https" }

function New-ReadmeContent {
  param([string]$RepoName, [string]$Description)
  return @"
# $RepoName

$Description

This repository was initialized by automation and is intentionally minimal until module content is added.
"@
}

function Seed-WithGitHubApi {
  param(
    [Parameter(Mandatory=$true)][string]$FullName,
    [Parameter(Mandatory=$true)][string]$Content,
    [string]$Branch = "main"
  )

  $encoded = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($Content))
  gh api -X PUT "repos/$FullName/contents/README.md" `
    -f message="chore: initial scaffold" `
    -f branch=$Branch `
    -f content=$encoded `
    -f committer[name]="Anity Bot" `
    -f committer[email]="bot@users.noreply.github.com" | Out-Null
}

function Ensure-GhAuth {
  try {
    gh auth status | Out-Null
  } catch {
    Write-Error "gh auth failed. Run: gh auth login"
    exit 1
  }
}

function New-RepoIfNotExist([string]$fullName, [string]$description) {
  $repo = gh repo view $fullName 2>$null
  if ($LASTEXITCODE -eq 0) {
    Write-Host "exists: $fullName"
    if ($SeedInitialCommit) { Ensure-InitialCommit -FullName $fullName }
    return
  }

  gh repo create $fullName --$Visibility --confirm --description "$description"
  if ($LASTEXITCODE -ne 0) {
    Write-Error "create failed: $fullName"
    exit 1
  }
  Write-Host "created: $fullName"
  if ($SeedInitialCommit) { Ensure-InitialCommit -FullName $fullName -Description $description }
}

function Ensure-InitialCommit {
  param(
    [Parameter(Mandatory=$true)]
    [string]$FullName,

    [string]$Description = ""
  )

  if ($gitProtocol -eq "ssh") {
  $remoteUrl = "git@github.com:$FullName.git"
  } else {
    $remoteUrl = "https://github.com/$FullName.git"
  }
  $tmpRoot = Join-Path $env:TEMP ("anity_seed_" + [Guid]::NewGuid().ToString("N"))
  $repoName = Split-Path $FullName -Leaf

  $heads = & git ls-remote --heads $remoteUrl "main" 2>$null
  if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($heads)) {
    return
  }

  New-Item -ItemType Directory -Path $tmpRoot | Out-Null
  Push-Location $tmpRoot

  try {
    git init -b main | Out-Null
    $template = New-ReadmeContent -RepoName $repoName -Description $Description
    Set-Content -NoNewline -Path README.md -Value $template
    git add README.md
    git commit -m "chore: initial scaffold"
    git remote add origin $remoteUrl
    git push -u origin main | Out-Null
  }
  catch {
    try {
      Seed-WithGitHubApi -FullName $FullName -Content (New-ReadmeContent -RepoName $repoName -Description $Description)
    }
    catch {
      Write-Warning "seed failed for $FullName, may require manual initialization"
    }
  }
  finally {
    Pop-Location
    Remove-Item -Recurse -Force $tmpRoot
  }
}

Ensure-GhAuth

New-RepoIfNotExist "$Owner/anity" "Anity workspace root for multi-repo orchestration."
New-RepoIfNotExist "$Owner/anity-hub" "Anity Hub - launch / package / launcher experience."
New-RepoIfNotExist "$Owner/anity-editor" "Anity Editor - editor app platform."
New-RepoIfNotExist "$Owner/anity-lib-core" "Anity Lib Core - shared runtime/library modules."

Write-Host "All repositories are ready."
