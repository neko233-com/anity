# Operations Guide

## First checkout

1. Run `_scripts/install-env.sh` or `_scripts/install-env.ps1` on a new machine.
2. Run `_scripts/verify-env.sh` or `_scripts/verify-env.ps1`.
3. Build native and managed modules with `_scripts/build-all.*`.

## Daily development

- Work from the repository root; all modules are tracked directly.
- Keep native ABI, managed bindings, feature tests, `PLAN.md`, and `Checklist.md` in the same change.
- Use `main` as the stable checkpoint unless a task explicitly requires a branch.

## Required checks before merging module changes

- `_scripts/build-all.*` succeeds.
- `_scripts/run-tests.*` succeeds with the freshly built native runtime.
- Relevant Unity 2022.3 probes and parity audits succeed.
- `PLAN.md` and `Checklist.md` state both completed evidence and remaining gaps.

## Common local gate

```bash
bash _scripts/verify-env.sh
bash _scripts/build-all.sh Release
bash _scripts/run-tests.sh Release
git status --short --branch
```
