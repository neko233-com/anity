# Versioning

## Root repo version

- root follows `vX.Y.Z` for orchestration metadata and release bundles.
- each module keeps independent semver (`MAJOR.MINOR.PATCH`) in its own repo.

## Dependency pin policy

- Modules consume each other only through:
  - `anity-lib-core` contract packages
  - explicit tags in module-specific manifests
- Root repo pins submodule SHAs to exact commit IDs.

## Compatibility levels

- `MAJOR`: protocol/package ABI incompatible changes
- `MINOR`: backward-compatible features
- `PATCH`: bugfixes and docs

## Tag naming

- Release tags: `v0.1.0`, `v0.2.0`...
- RC tags: `v0.2.0-rc.1`
- Hotfix tags: `v0.2.1-hf.1`

