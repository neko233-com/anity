# Versioning

## Product version

- The monorepo follows `vX.Y.Z` for product release bundles.
- Independently published packages such as `anity-agent` may keep their own semver.

## Dependency pin policy

- External dependencies use locked package manifests or checksummed vendored sources.
- In-repository modules are versioned atomically with the commit that changes their ABI or contract.

## Compatibility levels

- `MAJOR`: protocol/package ABI incompatible changes
- `MINOR`: backward-compatible features
- `PATCH`: bugfixes and docs

## Tag naming

- Release tags: `v0.1.0`, `v0.2.0`...
- RC tags: `v0.2.0-rc.1`
- Hotfix tags: `v0.2.1-hf.1`
