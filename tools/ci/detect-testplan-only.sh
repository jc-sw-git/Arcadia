#!/usr/bin/env bash
# Decides whether a pull request push may skip build and tests: it changed only docs/testplans/** (the owner
# marking manual tests as passed) and the commit before it was green. Writes skip_build=true|false to $GITHUB_OUTPUT.
# Any doubt means false: a full run is only slower, a wrongly skipped one hides a red build.
# Usage: detect-testplan-only.sh <before-sha> <head-sha>; needs $GH_TOKEN and $GITHUB_REPOSITORY.
set -uo pipefail

before="${1:-}"
head="${2:-}"
required_jobs=("build (ubuntu-latest)" "build (windows-latest)")

emit() {
  echo "$1"
  echo "skip_build=$2" >> "${GITHUB_OUTPUT:-/dev/stdout}"
  exit 0
}

# Only a push to an open PR has a previous head; opened, reopened and push events always build.
[[ -n "$before" && "$before" != "0000000000000000000000000000000000000000" && -n "$head" ]] \
  || emit "No previous head: full build." false

# After a force-push the previous head is gone from the clone.
git cat-file -e "$before^{commit}" 2>/dev/null || emit "Previous head $before is not available: full build." false

changed=$(git diff --name-only "$before" "$head") || emit "Diff failed: full build." false
[[ -n "$changed" ]] || emit "No file changed: full build." false
if grep -qvE '^docs/testplans/' <<< "$changed"; then
  emit "Files outside docs/testplans/ changed: full build." false
fi

# A skipped build must not turn a red commit green: the previous head has to have passed (a testplan-only
# head that was skipped itself counts, its own predecessor passed by the same rule).
runs=$(gh api "repos/${GITHUB_REPOSITORY}/commits/${before}/check-runs?per_page=100") \
  || emit "Could not read the checks of $before: full build." false
for job in "${required_jobs[@]}"; do
  conclusion=$(jq -r --arg name "$job" \
    '[.check_runs[] | select(.name == $name)] | max_by(.id) | .conclusion // "none"' <<< "$runs")
  [[ "$conclusion" == "success" ]] || emit "'$job' on $before is '$conclusion', not success: full build." false
done

emit "Only docs/testplans/ changed and $before was green: build and tests skipped." true
