Anity _scripts/ — environment, build, audit (canonical)

Windows (PowerShell):
  .\_scripts\install-env.ps1
  .\_scripts\verify-env.ps1
  .\_scripts\build-native.ps1
  .\_scripts\build-all.ps1
  .\_scripts\publish-cli.ps1
  .\_scripts\run-tests.ps1 Release
  .\_scripts\gap-audit.ps1
  .\_scripts\unity-api-parity.ps1
  .\_scripts\install-vulkan-sdk.ps1
  .\_scripts\install-android-sdk.ps1

Unix:
  bash _scripts/install-env.sh
  bash _scripts/verify-env.sh
  bash _scripts/build-native.sh
  bash _scripts/build-all.sh
  bash _scripts/publish-cli.sh Release
  bash _scripts/run-tests.sh Release
  bash _scripts/unity-api-parity.sh
  bash _scripts/capture-unity-vfx-spawner.sh

Official Unity API parity:
  - Compares UnityEngine/UnityEditor public + protected metadata against Anity.Core.
  - Writes the detailed local report to parity-evidence/*-current.json.
  - Tracks parity-evidence/unity-api-parity-baseline.json to reject regressions.
  - Set RESET_API_PARITY_BASELINE=1 only for an intentional, reviewed baseline update.

Deep test gate:
  - run-tests requires the freshly built native runtime and fails if it cannot be loaded.
  - Run build-all before run-tests so managed and native binaries are from the same revision.

Self-contained CLI:
  - publish-cli builds the host-matching native runtime and publishes a directly runnable
    `build/cli/<rid>/anity` (`anity.exe` on Windows) without requiring a system .NET runtime.
  - The publish is rejected for cross-architecture RIDs, redirects RID lock evaluation into staging,
    and runs batchmode with an intentionally invalid DOTNET_ROOT before atomically publishing.

Official Unity VFX Spawner Player fixture:
  - Builds the ignored Unity probe project with Unity 2022.3.51f1 + VFX/URP 14.0.11.
  - Runs a visible Metal Player (batchmode sleeps/culls VFX and is not valid evidence).
  - Captures loop timing, ordered Set SpawnEvent Attribute, and custom Spawner Callback semantics.
  - Validates 573 state snapshots, 100 Player Output Event records, and 24 callback lifecycle records in
    parity-evidence/unity-vfx-spawner-2022.3.51f1.json.

All new install/build scripts MUST live here (see AGENTS.md §六).
