# Contributing

## Commit style

Use Conventional Commits:

- `feat: ...`
- `fix: ...`
- `chore: ...`
- `refactor: ...`
- `docs: ...`

## PR checklist

- [ ] Module change only lives in its corresponding repo unless orchestration change.
- [ ] Submodule pin updated in root if module SHA changed.
- [ ] CI passes on all supported OS.
- [ ] CHANGELOG entry added for user-visible behavior changes.

## Build env assumptions

- Git 2.40+
- GitHub CLI 2.40+
- Node/.NET depending on module choice (module repos define exact versions)

