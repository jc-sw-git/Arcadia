#!/usr/bin/env bash
# Fails one of the owner's pull requests that names him or his PC: real name, account ids, user folder (CLAUDE.md →
# Secure). The terms come from the secret PRIVATE_TERMS, one per line, so they stay out of the public repository; the
# log names only where a term was found, never the term or the line.
# Usage: check-privacy.sh <base-sha> <head-sha>, PR description in $PR_BODY, terms in $PRIVATE_TERMS.
set -euo pipefail

base="$1"
head="$2"
mapfile -t terms < <(printf '%s\n' "${PRIVATE_TERMS:-}" | tr -d '\r' | sed '/^[[:space:]]*$/d')
if (( ${#terms[@]} == 0 )); then
  echo "::error::The secret PRIVATE_TERMS is not set (Settings → Secrets and variables → Actions)."
  exit 1
fi

patterns=$(mktemp)
trap 'rm -f "$patterns"' EXIT
printf '%s\n' "${terms[@]}" > "$patterns"
found=()

# Added lines per file, so the log can name the file without its content.
while IFS= read -r file; do
  if git diff --unified=0 "$base...$head" -- "$file" | grep -E '^\+[^+]' | grep -qiFf "$patterns"; then
    found+=("$file")
  fi
done < <(git diff --name-only --diff-filter=AMR "$base...$head")

# Messages only: commits made in GitHub's web UI carry the account's display name as author, which no PR can change.
git log --format='%B' "$base..$head" | grep -qiFf "$patterns" && found+=("a commit message")
grep -qiFf "$patterns" <<< "${PR_BODY:-}" && found+=("the PR description")

if (( ${#found[@]} )); then
  echo "::error::A private term (secret PRIVATE_TERMS) appears in: ${found[*]}. Replace it with 'owner', jc-sw-git or a made-up value."
  exit 1
fi
echo "No private terms found."
