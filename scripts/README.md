# Scripts

## Bootstrap

- `create-anity-org-repos.ps1` / `create-anity-org-repos.sh`
  - Create 4 GitHub repositories:
    - `anity`
    - `anity-hub`
    - `anity-editor`
    - `anity-lib-core`
- `bootstrap-workspace.ps1` / `bootstrap-workspace.sh`
  - Initialize current folder as root workspace repo.
  - Add module submodules under `modules/`.

## Execution notes

- PowerShell:
  - `Set-ExecutionPolicy -Scope Process Bypass`
  - `.\scripts\bootstrap-workspace.ps1 -Owner your-org`
- Bash:
  - `chmod +x scripts/*.sh`
  - `bash ./scripts/create-anity-org-repos.sh your-org private`

## Cross-platform daily sync

- PowerShell:
  - `.\scripts\sync-modules.ps1`
- Bash:
  - `bash ./scripts/sync-modules.sh`

## Unity API migration audit

- PowerShell:
  - `.\scripts\unity-compat-audit.ps1 -SourceRoot "." -FailOnUnsupported`
- Bash:
  - `bash ./scripts/unity-compat-audit.sh "."`
