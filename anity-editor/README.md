# anity-editor

Editor core service for the Anity stack.

Current scaffold provides:

- session lifecycle model
- project open/close API
- Unity-like editor API shell (`UnityEditor` namespace): menu registry, window lifecycle, selection/asset/handles/serialized-object facades
- host-driven window registry and menu command execution
- attribute-driven `MenuItem` discovery from editor assembly
- editor preferences/menu/undo/scene management shell APIs
- mock runtime commands for local development

## Run

```bash
dotnet restore
dotnet build
dotnet run --project src/Anity.Editor.Host/Anity.Editor.Host.csproj -- start --project-path ./DemoProject
dotnet run --project src/Anity.Editor.Host/Anity.Editor.Host.csproj -- menu list
dotnet run --project src/Anity.Editor.Host/Anity.Editor.Host.csproj -- window list
dotnet run --project src/Anity.Editor.Host/Anity.Editor.Host.csproj -- status
```

## Planned evolution

- host-level graph editor modules
- package cache service
- extension host hooks
- runtime/scene serialization and import pipeline
