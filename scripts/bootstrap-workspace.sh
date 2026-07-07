#!/usr/bin/env bash
set -euo pipefail

OWNER="${1:?Usage: ./bootstrap-workspace.sh <owner> [main-repo] [module1 module2 ...]}"
MAIN_REPO="${2:-anity}"
shift 2
MODULES=("${@:-anity-hub anity-editor anity-lib-core}")

if ! command -v git >/dev/null 2>&1; then
  echo "git is required"
  exit 1
fi

if [ ! -d .git ]; then
  git init -b main
  git remote add origin "git@github.com:${OWNER}/${MAIN_REPO}.git"
fi

mkdir -p modules

for mod in "${MODULES[@]}"; do
  path="modules/${mod}"
  if [ -d "$path" ]; then
    continue
  fi
  git submodule add "git@github.com:${OWNER}/${mod}.git" "$path"
done

git add . || true
git commit -m "chore: initialize anity workspace with module submodules" || true
echo "workspace ready"

