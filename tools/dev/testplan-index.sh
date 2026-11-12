#!/usr/bin/env bash
# Prints the overview of one iteration's test plan from its issue files: file, issue and title, the cases open for the
# owner. The files are the index; a hand-kept list in the iteration's README.md made every pair of parallel PRs
# conflict there, so there is none.
#
#   tools/dev/testplan-index.sh 3        # docs/testplans/iteration-3
set -euo pipefail

dir="$(dirname "$0")/../../docs/testplans/iteration-${1:?usage: testplan-index.sh <iteration>}"
echo "| File | Issue and title | Open for the owner |"
echo "|---|---|---|"
for file in "$dir"/*.md; do
  [[ "$(basename "$file")" == README.md ]] && continue
  title=$(head -n 1 "$file" | sed 's/^# *//')
  owner=$(grep -m 1 '^\*\*Status:\*\*' "$file" | sed -n 's/.*Open for the owner: *//p' || true)
  echo "| $(basename "$file" .md) | $title | ${owner:-–} |"
done
