#!/usr/bin/env bash
set -euo pipefail

MODULES=("${@:-anity-hub anity-editor anity-lib-core}")

for mod in "${MODULES[@]}"; do
  if [ ! -d "modules/$mod" ]; then
    echo "missing: $mod"
    continue
  fi
  echo "==> pull $mod"
  git -C "modules/$mod" pull --rebase
done
