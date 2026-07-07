param(
  [Parameter(Mandatory=$false)]
  [string]$SourceRoot = ".",

  [switch]$FailOnUnsupported
)

$unsupported = @{
  "AssetDatabase" = "Not in shim yet. Use runtime manifest/importer pipeline instead."
  "UnityEditor" = "Editor-only Unity API; schedule in external tooling layer."
  "EditorWindow" = "EditorWindow shim missing; keep in separate host tooling process."
  "EditorGUI" = "EditorGUI shim missing."
  "EditorGUILayout" = "EditorGUILayout shim missing."
  "SerializedObject" = "Editor serialization API missing; use core DTO in editor host."
  "SerializedProperty" = "Editor serialization API missing."
  "Undo" = "Undo system missing."
  "Handles" = "Handles API missing."
  "SceneManager" = "UnityEngine.SceneManagement shim currently limited (Load/Create scene only)."
  "MeshRenderer" = "Rendering components not shimmed yet."
  "Animator" = "Animator API not yet mapped."
  "RenderTexture" = "Rendering API not yet mapped."
  "Shader" = "Shader API not yet mapped."
  "Texture2D" = "Texture API not yet mapped."
}

$matches = @()

$allFiles = Get-ChildItem -Path $SourceRoot -Recurse -Filter *.cs -File

foreach ($kv in $unsupported.GetEnumerator()) {
  $pattern = $kv.Key
  $note = $kv.Value
  $results = $allFiles | Select-String -Pattern "\\b$pattern\\b" -ErrorAction SilentlyContinue
  foreach ($r in $results) {
    $record = [PSCustomObject]@{
      File = $r.Path
      Line = $r.LineNumber
      Text = $r.Line.Trim()
      Pattern = $pattern
      Suggestion = $note
    }
    $matches += $record
  }
}

if ($matches.Count -eq 0) {
  Write-Host "compat audit: no unsupported API hits found"
  exit 0
}

Write-Host "compat audit: found $($matches.Count) hits" -ForegroundColor Yellow
$matches | Sort-Object File,Line | Format-Table -AutoSize

if ($FailOnUnsupported) {
  exit 1
}

exit 0
