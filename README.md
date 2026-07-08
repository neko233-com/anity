# anity

Anity is a Unity-inspired, cross-platform open game/editor platform split into four functional workspaces:

- `anity-hub`: desktop launcher, account/login shell, project workspace management, and update bootstrap.
- `anity-editor`: editor core application (project/session/assets graph, play loop orchestration, extension host).
- `anity-lib-core`: shared runtime/runtime-less shared libs, serialization, package manifests, utility services.
- `anity-webgl`: WebGL platform support (Unity WebGL compatibility layer).

## Repository Layout

- `anity-hub/` - desktop launcher and project management
- `anity-editor/` - editor core application
- `anity-lib-core/` - shared runtime and Unity API compatibility layer
- `anity-webgl/` - WebGL platform support
- `docs/` - architecture and ops documents
- `scripts/` - bootstrap and CI scripts
- `.github/workflows/` - cross-platform pipeline

> All modules are at the root level for easy access and maintenance.

## Quick start (create GitHub repos + workspace)

### 1) Prepare GH CLI

```bash
gh auth status
```

### 2) Run bootstrap script (PowerShell)

```powershell
.\scripts\create-anity-org-repos.ps1 -Owner "YOUR_ORG_OR_USER" -Visibility private
.\scripts\bootstrap-workspace.ps1 -Owner "YOUR_ORG_OR_USER"
```

### 3) Run bootstrap script (bash/macOS/Linux)

```bash
bash ./scripts/create-anity-org-repos.sh YOUR_ORG_OR_USER private
bash ./scripts/bootstrap-workspace.sh YOUR_ORG_OR_USER
```

## What this setup includes

- Submodule wiring in one command
- Cross-platform CI (`ubuntu`, `windows`, `macos`)
- Release workflow scaffold with version tag + changelog hook points
- Repo strategy, dependency policy, and versioning rules docs
- GitHub project conventions for 4-repo architecture
- Unity compatibility shim in `anity-lib-core` (`UnityCompat`) for API-parity migration
- WebGL platform support in `anity-webgl`

Note: "Unity API一样" is achieved via staged compatibility, not vendor-equivalent source.

## Branch model

- `main`: stable release line
- `release/x.y.z`: pre-release stabilization
- `feat/*`, `fix/*`, `chore/*`: task branches

## Submodule map

Add these repositories as submodules:

- `anity-hub`
- `anity-editor`
- `anity-lib-core`
- `anity-webgl`

Then keep all four at compatible tags / commit pins.
