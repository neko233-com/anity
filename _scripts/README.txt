Anity _scripts/ — environment, build, audit (canonical)

Windows (PowerShell):
  .\ _scripts\install-env.ps1
  .\ _scripts\verify-env.ps1
  .\ _scripts\build-native.ps1
  .\ _scripts\build-all.ps1
  .\ _scripts\gap-audit.ps1
  .\ _scripts\install-vulkan-sdk.ps1
  .\ _scripts\install-android-sdk.ps1

Unix:
  bash _scripts/install-env.sh
  bash _scripts/verify-env.sh
  bash _scripts/build-native.sh
  bash _scripts/build-all.sh

Legacy scripts/ folder only forwards to _scripts where applicable.
All new install/build scripts MUST live here (see AGENTS.md §六).
