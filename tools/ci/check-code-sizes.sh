#!/usr/bin/env bash
# Keeps source files small enough to read whole (CLAUDE.md → Definition of Done, Readable): a large file costs context
# in every thread that touches it and draws merge conflicts between parallel PRs. A new file stays at or below the
# limit; a file already above it is listed in code-size-ceilings.txt with a ceiling it may not grow past. When a split
# shrinks a listed file, lower its ceiling (or drop the line) in the same PR, so the ceiling only goes down.
set -euo pipefail

limit=600
ceilings=tools/ci/code-size-ceilings.txt
failed=0

declare -A ceiling
while read -r file lines rest; do
  [[ -z "$file" || "$file" == \#* ]] && continue
  lines=${lines%$'\r'}
  # A malformed line must fail, not slip through as an arithmetic error that leaves the check green.
  if [[ ! "$lines" =~ ^[0-9]+$ || -n "$rest" ]]; then
    echo "::error file=$ceilings::Malformed line for $file; expected '<path> <lines>' and nothing else."
    failed=1
    continue
  fi
  ceiling["$file"]=$lines
done < "$ceilings"

while IFS= read -r file; do
  lines=$(wc -l < "$file")
  allowed=${ceiling["$file"]:-$limit}
  if (( lines > allowed )); then
    if [[ -n "${ceiling["$file"]:-}" ]]; then
      echo "::error file=$file::$file has $lines lines, its ceiling is $allowed. Move a concern into its own class instead of raising the ceiling."
    else
      echo "::error file=$file::$file has $lines lines, more than $limit. Split it by concern (CLAUDE.md → Definition of Done, Readable). If it is a listed file that was renamed, move its line in $ceilings."
    fi
    failed=1
  elif [[ -n "${ceiling["$file"]:-}" ]] && (( lines < allowed )); then
    echo "::warning file=$file::$file shrank to $lines lines; lower its ceiling in $ceilings (drop the line at $limit or below)."
  fi
done < <(find src -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' | sort)

for file in "${!ceiling[@]}"; do
  [[ -f "$file" ]] || echo "::warning::$file is listed in $ceilings but no longer exists; drop the line."
done

(( failed == 0 )) && echo "All source files within their size limits."
exit "$failed"
