#!/usr/bin/env bash
set -euo pipefail

OWNER="${1:?Usage: ./create-anity-org-repos.sh <owner> [public|private] [repo list...]}"
VISIBILITY="${2:-private}"
GIT_PROTOCOL="$(gh config get git_protocol || echo https)"
SEED_INITIAL_COMMIT="${3:-true}"
shift 3 || true
if [ "$#" -gt 0 ]; then
  REPOS=("$@")
else
  REPOS=(anity anity-hub anity-editor anity-lib-core)
fi

if ! command -v gh >/dev/null 2>&1; then
  echo "GitHub CLI (gh) not installed."
  exit 1
fi

if ! gh auth status >/dev/null 2>&1; then
  echo "gh auth missing. Run: gh auth login"
  exit 1
fi

if [ "$VISIBILITY" != "public" ] && [ "$VISIBILITY" != "private" ]; then
  echo "visibility must be public or private"
  exit 1
fi

seed_if_empty() {
  local name="$1"
  local description="$2"
  local url="https://github.com/${OWNER}/${name}.git"
  if [ "$GIT_PROTOCOL" = "ssh" ]; then
    url="git@github.com:${OWNER}/${name}.git"
  fi

  if git ls-remote --heads "$url" main >/dev/null 2>&1; then
    return
  fi

  local tmp_dir
  tmp_dir="$(mktemp -d)"
  if [ "${SEED_INITIAL_COMMIT,,}" != "true" ]; then
    return 0
  fi

  if ! git -C "$tmp_dir" init -b main >/dev/null; then
    rm -rf "$tmp_dir"
    return 1
  fi
  local readme_file="$tmp_dir/README.md"
  cat > "$readme_file" <<EOF
# ${name}

${description}

This repository was initialized by automation and is intentionally minimal until module content is added.
EOF
  git -C "$tmp_dir" add README.md
  if git -C "$tmp_dir" commit -m "chore: initial scaffold" >/dev/null && \
     git -C "$tmp_dir" remote add origin "$url" && \
     git -C "$tmp_dir" push -u origin main >/dev/null; then
    rm -rf "$tmp_dir"
    return 0
  fi

  content=$(base64 -w0 "$readme_file")
  if ! gh api repos/"${OWNER}"/"${name}"/contents/README.md \
       -X PUT \
       -f message="chore: initial scaffold" \
       -f branch="main" \
       -f content="$content" \
       -f committer[name]="Anity Bot" \
       -f committer[email]="bot@users.noreply.github.com" >/dev/null; then
    rm -rf "$tmp_dir"
    return 1
  fi

  rm -rf "$tmp_dir"
}

for name in "${REPOS[@]}"; do
  if gh repo view "$OWNER/$name" >/dev/null 2>&1; then
    echo "exists: $OWNER/$name"
    seed_if_empty "$name" ""
  else
    gh repo create "$OWNER/$name" --"$VISIBILITY" --confirm
    echo "created: $OWNER/$name"
    seed_if_empty "$name" ""
  fi
done

echo "done."
