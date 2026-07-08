# anity-hub

Cross-platform launcher module for Anity:

- list local/editor installations
- verify manifests
- bootstrap editor download/update workflow

## Development

```bash
dotnet restore
dotnet build
dotnet run --project src/Anity.Hub/Anity.Hub.csproj -- --help
```

## Inputs

The hub reads `manifest.json` and prints version checks / install intents.
