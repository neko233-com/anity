# Unity 2022.3 Pro gap audit against Anity.Core public surface + Checklist
param(
  [string]$SourceRoot = "",
  [switch]$FailOnStub
)

$ErrorActionPreference = "Continue"
$Root = if ($SourceRoot) { $SourceRoot } else { Split-Path -Parent $PSScriptRoot }
$Core = Join-Path $Root "anity-lib-core\src\Anity.Core"
$Checklist = Join-Path $Root "Checklist.md"

Write-Host "=== Unity 2022 Pro gap audit ===" -ForegroundColor Cyan
Write-Host "Core: $Core"

$csFiles = Get-ChildItem $Core -Recurse -Filter *.cs -ErrorAction SilentlyContinue
Write-Host "C# files: $($csFiles.Count)"

$stubHits = @()
foreach ($f in $csFiles) {
  $lines = Select-String -Path $f.FullName -Pattern "ANITY_STUB|NotImplementedException|throw new NotSupportedException" -ErrorAction SilentlyContinue
  foreach ($l in $lines) {
    $stubHits += "$($f.FullName):$($l.LineNumber): $($l.Line.Trim())"
  }
}

Write-Host "Potential stubs / not-implemented: $($stubHits.Count)" -ForegroundColor Yellow
$stubHits | Select-Object -First 40 | ForEach-Object { Write-Host "  $_" }
if ($stubHits.Count -gt 40) { Write-Host "  ... +$($stubHits.Count - 40) more" }

$nativeHdr = Join-Path $Root "anity-native\include\anity"
$modules = @("anity_core.h", "graphics\anity_graphics.h", "graphics\anity_hdr.h", "physics\anity_physics.h", "audio\anity_audio.h", "media\anity_media.h", "jobs\anity_jobs.h", "texture\anity_texture_compress.h")
Write-Host "Native headers:"
foreach ($m in $modules) {
  $p = Join-Path $nativeHdr $m
  if (Test-Path $p) { Write-Host "  [OK] $m" -ForegroundColor Green }
  else { Write-Host "  [MISS] $m" -ForegroundColor Red }
}

if (Test-Path $Checklist) {
  $text = Get-Content $Checklist -Raw
  $ok = ([regex]::Matches($text, "\| ✅ \|")).Count
  $bad = ([regex]::Matches($text, "\| ❌ \|")).Count
  Write-Host "Checklist: ✅=$ok  ❌=$bad"
} else {
  Write-Host "Checklist.md missing" -ForegroundColor Red
}

# High-value Unity modules expected for Pro parity
$expectedTypes = @(
  "HDROutputSettings",
  "UniversalRenderPipelineAsset",
  "VideoPlayer",
  "PrefabStage",
  "SearchService",
  "CompositeCollider2D",
  "MeshDataArray",
  "AvatarMask",
  "JobHandle",
  "TextureCompressionUtility"
)
Write-Host "Required type presence:"
foreach ($t in $expectedTypes) {
  $hit = $csFiles | Select-String -Pattern "class $t|struct $t|static class $t" -List -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($hit) { Write-Host "  [OK] $t" -ForegroundColor Green }
  else { Write-Host "  [MISS] $t" -ForegroundColor Red }
}

if ($FailOnStub -and $stubHits.Count -gt 0) {
  Write-Host "gap-audit FAILED (stubs present)" -ForegroundColor Red
  exit 1
}
Write-Host "gap-audit complete" -ForegroundColor Green
