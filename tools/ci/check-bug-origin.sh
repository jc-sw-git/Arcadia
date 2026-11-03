#!/usr/bin/env bash
# Fails a pull request that closes a `bug` issue whose body doesn't say where the bug came from. The release notes
# need that line to leave out bugs that never shipped (docs/releases.md); without it, every bug fixed in the same
# release as the change that caused it shows up as a fix.
# Accepted lines: "Introduced by #N" (the PR or issue that brought the bug) or "Introduced by vX.Y.Z" (it shipped in
# that release). Usage: check-bug-origin.sh, PR description in $PR_BODY, repository in $GITHUB_REPOSITORY, `gh` logged in.
set -euo pipefail

# The same references Write-ReleaseNotes.ps1 lists: "Closes #23", "fixes: #8", "Part of #5". A bug named only as
# "Part of" still shows in the notes, so it needs its origin too.
issues=$(printf '%s' "${PR_BODY:-}" \
  | grep -oiE '\b(close[sd]?|fix(e[sd])?|resolve[sd]?|part of)[[:space:]]*:?[[:space:]]+#[0-9]+' \
  | grep -oE '[0-9]+$' | sort -un || true)

# A call now and then comes back empty under load; retry before giving up.
read_issue() {
  local try json
  for try in 1 2 3; do
    json=$(gh api "repos/$GITHUB_REPOSITORY/issues/$1") && [[ -n "$json" ]] && { printf '%s' "$json"; return 0; }
    sleep $((5 * try))
  done
  return 1
}

missing=()
for issue in $issues; do
  json=$(read_issue "$issue") || { echo "::error::Could not read #$issue"; exit 1; }
  # "#N" may name a PR; only bug issues need an origin.
  jq -e '.pull_request == null and any(.labels[]; .name == "bug")' <<< "$json" > /dev/null || continue
  if ! jq -r '.body // ""' <<< "$json" | grep -qiE 'introduced (by|in):? (PR )?(#[0-9]+|v[0-9]+\.[0-9]+\.[0-9]+)'; then
    missing+=("#$issue")
  fi
done

if (( ${#missing[@]} )); then
  echo "::error::Bug ${missing[*]} has no line 'Introduced by #N' or 'Introduced by vX.Y.Z' in its description (docs/releases.md). Add it to the issue, then re-run this check."
  exit 1
fi
echo "Every closed bug names its origin."
