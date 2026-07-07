#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -lt 2 ]; then
  echo "Usage: ./bootstrap-repo.sh <repo> <owner> [branch]"
  exit 1
fi

REPO_NAME="$1"
OWNER="$2"
BRANCH="${3:-main}"

mkdir -p modules
DIR="modules/$REPO_NAME"
if [ -d "$DIR" ]; then
  echo "skip: $DIR exists"
  exit 0
fi

GIT_PROTOCOL="$(gh config get git_protocol || echo https)"
if [ "$GIT_PROTOCOL" = "ssh" ]; then
  URL="git@github.com:${OWNER}/${REPO_NAME}.git"
else
  URL="https://github.com/${OWNER}/${REPO_NAME}.git"
fi

git submodule add -b "$BRANCH" "$URL" "$DIR"

