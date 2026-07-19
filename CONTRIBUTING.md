# Contributing

## Commit style

Use Conventional Commits:

- `feat: ...`
- `fix: ...`
- `chore: ...`
- `refactor: ...`
- `docs: ...`

## PR checklist

- [ ] Native ABI, managed bindings, and affected modules are updated atomically.
- [ ] Every completed feature has at least 10 focused cases and the full native-backed gate passes.
- [ ] Relevant Unity 2022.3 Pro probes or API parity evidence are recorded.
- [ ] `PLAN.md` and `Checklist.md` include the result and remaining gaps.
- [ ] CHANGELOG entry added for user-visible behavior changes.

## Build env assumptions

- Git 2.40+
- .NET SDK 10
- CMake and a supported native C++ toolchain
- Platform SDKs reported by `_scripts/verify-env.*`
