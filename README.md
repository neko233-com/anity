# Anity

Anity is a cross-platform engine and editor whose target is API, behavior,
asset, build, and editor compatibility with Unity 2022.3.61f1 Pro. The parity
work is ongoing; `PLAN.md` and `Checklist.md` are the acceptance ledgers.

## Repository Layout

- `anity-native/` — native C++ engine, rendering, physics, media, import, and jobs
- `anity-lib-core/` — C# Unity API compatibility and managed/native bridge
- `anity-editor/` — editor host and Unity-compatible editor workflows
- `anity-cli/` — Unity-compatible `anity` command line
- `anity-agent/` — separately versioned official Agent extension
- `anity-webgl/` — WebGL runtime and platform integration
- `anity-shader-graph/` / `anity-vfx-graph/` — Unity package compatibility
- `anity-hub/` — launcher and project management
- `_scripts/` — the only supported environment, build, test, and audit entrypoints
- `samples/` — integration samples

All modules are tracked directly in this monorepo.

## Build and test

macOS/Linux:

```bash
bash _scripts/verify-env.sh
bash _scripts/build-all.sh Release
bash _scripts/run-tests.sh Release
```

Windows:

```powershell
.\_scripts\verify-env.ps1
.\_scripts\build-all.ps1 Release
.\_scripts\run-tests.ps1 Release
```

Run `_scripts/install-env.*` first on a new machine. Native-backed tests require a
fresh native build and intentionally fail when the runtime cannot be loaded.

## Branch model

- `main`: stable release line
- `release/x.y.z`: pre-release stabilization
- `feat/*`, `fix/*`, `chore/*`: task branches
