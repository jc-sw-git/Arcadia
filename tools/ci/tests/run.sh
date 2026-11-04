#!/usr/bin/env bash
# Self-test of the CI check scripts on made-up fixtures: a check that never goes red protects nothing.
set -euo pipefail
cd "$(dirname "$0")"
check=../check-testplan-file.sh
failed=0
expect() { # <expected exit> <description> <command…>
  local want="$1" what="$2"; shift 2
  local got=0; "$@" > /dev/null || got=$?
  if (( (want == 0) != (got == 0) )); then echo "::error::Self-test failed: $what (exit $got)"; failed=1; fi
}
expect 0 "a complete file passes" $check testplan-file/good.md yes
expect 0 "a file without cases passes" $check testplan-file/none.md yes
expect 1 "missing E2E, owner case and History fail before the review" $check testplan-file/bad.md no
expect 1 "an open case that is not the owner's fails after the review" $check testplan-file/bad.md yes
(( failed == 0 )) && echo "CI check self-tests passed."
exit "$failed"
