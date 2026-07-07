# anity Architecture Blueprint

## 1) 3-Repos Model

| Repo | Responsibility |
| --- | --- |
| `anity-hub` | Launcher shell, project listing, install/update orchestration, first-run initialization. |
| `anity-editor` | Core editor app, scene/state orchestration, inspector/editor tooling surface, runtime integration. |
| `anity-lib-core` | Shared libraries: serialization, package metadata, math/utilities, cross-module service contracts. |

## 2) Data & API Boundaries

- UI layer only depends on interfaces exported by `anity-lib-core`.
- All serialization formats are versioned via schema IDs in `anity-lib-core`.
- Editor runtime must not directly reach network layer internals of Hub; share contracts only.

## 3) Recommended Tech (Cross-platform)

- C# + .NET (for core libs/tools)
- C++/C# interop in editor runtime as needed
- Build matrix: `linux/x64`, `windows/x64`, `macos/universal`
- Native packaging with deterministic dependency lock files

## 4) CI Split

- Root workflow validates repository wiring and orchestrates submodule integrity.
- Per-repo workflows own compile/package for each module.
- Release artifacts from `anity-editor` are assembled and published in release flow.

## 5) Sync Strategy

- Pin submodule SHAs in root.
- Do not hot-fix directly in main repo; changes should go via module repos then update submodule pointers in root.

