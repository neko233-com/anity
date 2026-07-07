# Dependency Policy

## Design rules

- `anity-lib-core` has zero direct dependency on Hub/Editor implementation repos.
- `anity-editor` and `anity-hub` may only depend on released package interface from `anity-lib-core`.
- All third-party deps must be pinned with checksum lock files.

## Security & supply-chain

- Prefer official package sources.
- Track external SDK licenses in each module repo.
- Disable network in unit tests unless explicitly required.

## Update protocol

1. Propose update in module PR.
2. Run dependency scan and license check in module CI.
3. Sync API docs and bump lockfile in the same PR.

## Forbidden coupling

- No module code should compile by importing internal/private editor implementation from another module.
- No static global cross-module singleton for business logic.

