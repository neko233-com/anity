# CI/CD Notes

## Root CI

`.github/workflows/ci.yml` validates:

- tag/manual checkout of the monorepo
- .NET 10 and .NET 8 availability
- compile checks for Core, Agent, CLI, Editor, WebGL, and Hub

The workflow is intentionally compile-only. Product acceptance uses the local
native-backed gates below so a managed-only CI pass cannot be mistaken for
Unity parity evidence.

```bash
bash _scripts/build-all.sh Release
bash _scripts/run-tests.sh Release
```

## Release

Tag on root repo triggers release workflow.

```bash
git tag v0.1.0
git push --tags
```

The release workflow publishes a lightweight GitHub release. A GitHub release
does not by itself certify Unity 2022.3 Pro parity; the parity ledgers and local
runtime evidence remain authoritative.
