# Operations Guide

## Initial repository creation (one-time)

1. `pwsh`: `.\scripts\create-anity-org-repos.ps1 -Owner <OWNER>`
2. `pwsh`: `.\scripts\bootstrap-workspace.ps1 -Owner <OWNER>`
3. Fill each module repo in GitHub (or keep empty until code arrives).

## Daily module development

- Development is done inside each module repo.
- Root repo only updates submodule pointers.
- Before merging PR in root, ensure submodule hashes are compatible.

## Required checks before merging module changes

- Feature branches pass module CI.
- ABI/schema compatibility check with `anity-lib-core` version bounds.
- Release notes included for API-breaking changes.

## Common commands

### Pull latest all
```bash
git submodule update --init --recursive
git submodule update --remote
```

### Switch to specific module branch
```bash
git -C modules/anity-editor checkout feat/xxx
```

### Update submodule pointer
```bash
git add modules/anity-editor
git commit -m "chore: bump anity-editor module"
```

