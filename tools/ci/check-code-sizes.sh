#!/usr/bin/env bash
# Points at large source files a PR changes (CLAUDE.md → Definition of Done, Readable). It warns and never fails:
# file length is only a hint. A long cohesive file is fine; a file that mixes concerns is not, and only a reviewer can
# tell which. The warning prompts the author and the reviewer to ask whether the change belongs in its own class.
# Usage: check-code-sizes.sh <base-sha> <head-sha>
set -euo pipefail

base="$1"
head="$2"
hint=600
large=0

while IFS= read -r file; do
  [[ -f "$file" ]] || continue
  lines=$(wc -l < "$file")
  if (( lines > hint )); then
    echo "::warning file=$file::$file has $lines lines. Does this change belong in its own class? (CLAUDE.md → Definition of Done, Readable)"
    large=1
  fi
done < <(git diff --name-only --diff-filter=AMR "$base...$head" -- 'src/*.cs')

(( large == 0 )) && echo "No changed source file above $hint lines."
exit 0
