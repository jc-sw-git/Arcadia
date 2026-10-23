#!/usr/bin/env bash
# Keeps the docs every session may load small (CLAUDE.md → Context economy): CLAUDE.md is loaded on every
# step of every session, the other docs whenever their area is touched. A file over its limit is cut into
# topics, not raised. Test plan files are exempt: each is one issue's history, read only for that issue.
set -euo pipefail

core_limit=15000
doc_limit=10000
failed=0

check() {
  local file="$1" limit="$2" size
  size=$(wc -c < "$file")
  if (( size > limit )); then
    echo "::error file=$file::$file has $size bytes, more than $limit. Cut it into topics (CLAUDE.md → Context economy)."
    failed=1
  fi
}

check CLAUDE.md "$core_limit"
while IFS= read -r file; do
  check "$file" "$doc_limit"
done < <(find docs .claude/skills -name '*.md' -not -path 'docs/testplans/*' | sort)

(( failed == 0 )) && echo "All docs within their size limits."
exit "$failed"
