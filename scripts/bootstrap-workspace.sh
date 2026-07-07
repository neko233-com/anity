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

GIT_PROTOCOL="$(gh config get git_protocol || echo https)"

if [ ! -d .git ]; then
  git init -b main
  if [ "$GIT_PROTOCOL" = "ssh" ]; then
    git remote add origin "git@github.com:${OWNER}/${MAIN_REPO}.git"
  else
    git remote add origin "https://github.com/${OWNER}/${MAIN_REPO}.git"
  fi
fi

mkdir -p modules

for mod in "${MODULES[@]}"; do
  path="modules/${mod}"
  if [ -d "$path" ]; then
    continue
  fi
  if [ "$GIT_PROTOCOL" = "ssh" ]; then
    git submodule add "git@github.com:${OWNER}/${mod}.git" "$path"
  else
    git submodule add "https://github.com/${OWNER}/${mod}.git" "$path"
  fi
done

git add . || true
git commit -m "chore: initialize anity workspace with module submodules" || true
echo "workspace ready"
