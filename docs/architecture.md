# Anity Architecture

## 1) Monorepo modules

| Module | Responsibility |
| --- | --- |
| `anity-native` | C++ engine core and platform graphics backends exposed through C ABI. |
| `anity-lib-core` | Unity-compatible managed API, serialization, and native lifetime bridge. |
| `anity-hub` | Launcher shell, project listing, install/update orchestration, first-run initialization. |
| `anity-editor` | Core editor app, scene/state orchestration, inspector/editor tooling surface, runtime integration. |
| `anity-cli` | Unity Editor/Player-compatible command-line surface. |
| `anity-agent` | Independent official Agent extension package. |
| `anity-webgl` | WebGL player runtime and browser platform integration. |
| `anity-shader-graph` / `anity-vfx-graph` | Unity package asset, editor, compiler, and runtime compatibility. |

## 2) Data & API Boundaries

- UI layer only depends on interfaces exported by `anity-lib-core`.
- All serialization formats are versioned via schema IDs in `anity-lib-core`.
- Editor runtime must not directly reach network layer internals of Hub; share contracts only.
- Native engine responsibilities stay in `anity-native`; managed code owns the public API and object lifetime bridge.

## 3) Recommended Tech (Cross-platform)

- C++ for engine, rendering, physics, media, import, and jobs
- C#/.NET for Unity-compatible APIs, editor, CLI, and tools
- URP 14.x as the product rendering path
- Build matrix led by WebGL, Windows, Android/Vulkan, and iOS/Metal
- Native packaging with deterministic dependency lock files

## 4) Verification

- `_scripts/build-all.*` builds the native runtime and managed modules.
- `_scripts/run-tests.*` runs all native-required test projects.
- `_scripts/unity-api-parity.*` compares the managed surface with the installed target Unity editor.
- `PLAN.md`, `Checklist.md`, and tracked `parity-evidence/` record accepted evidence and remaining gaps.

## 5) Source strategy

- All product modules are ordinary directories in this repository.
- Cross-module changes are committed atomically so native ABI, managed bridge, tests, and docs stay aligned.
