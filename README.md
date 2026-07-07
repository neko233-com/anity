# anity

Anity is a Unity-inspired, cross-platform open game/editor platform split into three functional workspaces:

- `anity-hub`: desktop launcher, account/login shell, project workspace management, and update bootstrap.
- `anity-editor`: editor core application (project/session/assets graph, play loop orchestration, extension host).
- `anity-lib-core`: shared runtime/runtime-less shared libs, serialization, package manifests, utility services.

This repo is a **management repository** that coordinates versions, scripts, docs, and CI for the three code repositories.

## Repository Layout

- `docs/` architecture and ops documents
- `scripts/` bootstrap/bootstrap CI scripts
- `.github/workflows/` cross-platform pipeline
- `modules/` local mount point for checked-out sub-repositories

> Root repo is not the editor source itself. The real runtime code should live in `anity-hub`, `anity-editor`, and `anity-lib-core`.

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

### 2) Run bootstrap script (bash/macOS/Linux)

```bash
bash ./scripts/create-anity-org-repos.sh YOUR_ORG_OR_USER private
bash ./scripts/bootstrap-workspace.sh YOUR_ORG_OR_USER
```

## What this setup includes

- Submodule wiring in one command
- Cross-platform CI (`ubuntu`, `windows`, `macos`)
- Release workflow scaffold with version tag + changelog hook points
- Repo strategy, dependency policy, and versioning rules docs
- GitHub project conventions for 3-repo architecture

## Branch model

- `main`: stable release line
- `release/x.y.z`: pre-release stabilization
- `feat/*`, `fix/*`, `chore/*`: task branches

## Submodule map

Add these repositories as submodules under `modules/`:

- `modules/anity-hub`
- `modules/anity-editor`
- `modules/anity-lib-core`

Then keep all three at compatible tags / commit pins.

