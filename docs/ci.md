# CI/CD Notes

## Root CI

`.github/workflows/ci.yml` validates:

- repository checkout with submodules
- basic environment steps
- module presence checks

## Suggested per-module CI

### anity-lib-core
- build shared packages
- run API compatibility tests

### anity-editor
- editor compile & unit tests
- headless smoke run
- artifact generation for mac/win/linux

### anity-hub
- launcher build
- update channel simulation
- package resolver dry-run

## Release

Tag on root repo triggers release workflow.

```bash
git tag v0.1.0
git push --tags
```

Root release job publishes a lightweight GitHub release. You can replace this with changelog-driven artifact publish.

