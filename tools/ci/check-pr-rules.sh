#!/usr/bin/env bash
# Fails one of the owner's pull requests that breaks the PR rules of our way of working: branch name (CLAUDE.md → Git),
# section Handoff with the risk lines when a risk path is touched (docs/personas/architect.md), section Review once the
# PR adds test cases, every box of the Review's checklist ticked or "n/a: <reason>" (docs/personas/quality-engineer.md).
# Usage: check-pr-rules.sh <base-sha> <head-sha>, PR description in $PR_BODY, branch in $HEAD_REF.
set -euo pipefail

base="$1"
head="$2"
branch="${HEAD_REF:-}"
errors=()

if ! [[ "$branch" =~ ^(feat|fix|ci|test|doc|td)/[a-z0-9]+(-[a-z0-9]+)*$ ]]; then
  errors+=("Branch '$branch' is not '{feat|fix|ci|test|doc|td}/{issue-title}-{issue-number}' in kebab case (CLAUDE.md → Git).")
fi

# Hints left as HTML comments don't count as written.
body=$(printf '%s' "${PR_BODY:-}" | tr -d '\r' | perl -0pe 's/<!--.*?-->//gs')
section() { awk -v name="$1" '/^## / { inside = ($0 ~ "^## +" name " *$"); next } inside' <<< "$body"; }
has_section() { grep -qE "^## +$1 *$" <<< "$body"; }

has_section Handoff || errors+=("The description has no section '## Handoff' (docs/personas/README.md → Handoffs).")

# Risk paths are files or folders ending in /**.
mapfile -t changed < <(git diff --name-only "$base...$head")
risk_file=""
while IFS= read -r pattern; do
  [[ -z "$pattern" || "$pattern" == \#* ]] && continue
  for file in "${changed[@]}"; do
    if [[ "$file" == "$pattern" ]] || [[ "$pattern" == *'/**' && "$file" == "${pattern%\*\*}"* ]]; then
      risk_file="$file"
    fi
  done
done < tools/ci/risk-paths.txt

if [[ -n "$risk_file" ]]; then
  handoff=$(section Handoff)
  for line in "Risk, on the player's PC:" "Risk, logs and undo:"; do
    if ! grep -F "**$line**" <<< "$handoff" | grep -qE '\*\*[[:space:]]*[^[:space:]*]'; then
      errors+=("The PR touches the risk path $risk_file; its Handoff needs the line '**$line** …' filled in (docs/personas/architect.md).")
    fi
  done
fi

adds_cases=$(git diff --unified=0 "$base...$head" -- 'docs/testplans/iteration-*' \
  | grep -cE '^\+- \[[ xX]\] *(~~)?\*\*T[0-9]+-[0-9]+\*\*' || true)
if (( adds_cases > 0 )) && ! has_section Review; then
  errors+=("The PR adds test cases but has no section '## Review' yet: the quality engineer reviews before the owner tests (docs/personas/quality-engineer.md).")
fi

if has_section Review; then
  while IFS= read -r box; do
    errors+=("Review checklist: neither ticked nor 'n/a: <reason>': ${box#- \[ \] }")
  done < <(section Review | grep -E '^- \[ \]' | grep -vE 'n/a: *[^[:space:]]' || true)
fi

if (( ${#errors[@]} )); then
  printf '::error::%s\n' "${errors[@]}"
  exit 1
fi
echo "PR rules hold."
