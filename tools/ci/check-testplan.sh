#!/usr/bin/env bash
# Fails a pull request that closes an issue without adding that issue's tests to the test plan
# (Definition of Done in CLAUDE.md, format in docs/testplans/README.md).
# Usage: check-testplan.sh <base-sha> <head-sha>, PR description in $PR_BODY.
set -euo pipefail

base="$1"
head="$2"

# GitHub's closing keywords, e.g. "Closes #23", "fixes #8".
issues=$(printf '%s' "${PR_BODY:-}" \
  | grep -oiE '\b(close[sd]?|fix(e[sd])?|resolve[sd]?)[[:space:]]+#[0-9]+' \
  | grep -oE '[0-9]+$' | sort -un || true)

if [[ -z "$issues" ]]; then
  echo "The PR closes no issue; no test plan entry needed."
  exit 0
fi

added=$(git diff --unified=0 "$base...$head" -- 'docs/testplans/iteration-*' | grep -E '^\+[^+]' || true)

missing=()
for issue in $issues; do
  # Matches the issue heading "# #23 …" (its own file) or "## #23 …" or a test case "**T23-1**" / history row "| T23-1 |".
  # A here-string, not a pipe: with pipefail, grep -q closing the pipe early would fail printf on a large diff.
  if ! grep -qE "(^\+##? #${issue}\b|\bT${issue}-[0-9]+)" <<< "$added"; then
    missing+=("#$issue")
  fi
done

if (( ${#missing[@]} )); then
  echo "::error::The PR closes ${missing[*]} but adds no tests for it to docs/testplans/iteration-<n>/ (see docs/testplans/README.md)."
  exit 1
fi
echo "Test plan updated for: $(echo $issues | sed 's/[0-9]\+/#&/g')."
