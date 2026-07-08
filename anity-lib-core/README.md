# anity-lib-core

Shared contracts and runtime primitives for the anity platform.

- Editor/Hub protocol types
- Package descriptors and version metadata
- Cross-platform utility DTOs
- Plugin manifest and dependency descriptors

This repo is framework-agnostic and should not depend on Editor or Hub internals.

### Unity compatibility layer

`UnityCompat` provides a practical subset of Unity-like API types for engine-like development and migration ease:

- `UnityEngine.Vector2`, `UnityEngine.Vector3`, `UnityEngine.Quaternion`
- `UnityEngine.GameObject`, `UnityEngine.Component`, `UnityEngine.Transform`, `UnityEngine.MonoBehaviour`
- `UnityEngine.Time`, `UnityEngine.Mathf`, `UnityEngine.Debug`, `UnityEngine.Random`
- `UnityEngine.Application` and common attributes
- `UnityEngine.PlayerPrefs` shell persistence

This is a compatibility shim, not an official Unity implementation.

## Quick build

```bash
dotnet restore
dotnet build
```

## Version policy

- API compatibility is documented in `src/Anity.Core/Runtime/VersionInfo.cs`
- Breaking protocol changes require major version bump
- Non-breaking additive changes use minor/patch updates
