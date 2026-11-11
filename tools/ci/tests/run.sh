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
texts=../check-ui-texts.sh
expect 0 "allowed spellings, keys, comments and the header pass" $texts ui-texts/good.resx
for fixture in ui-texts/bad-*.resx; do
  expect 1 "forbidden spelling in $fixture fails" $texts "$fixture"
done
# Without arguments the check finds every resource file under src/, a launcher folder's too.
launcher_tree=$(mktemp -d)
mkdir -p "$launcher_tree/src/Arcadia.Launchers/Fake"
cp ui-texts/bad-emuliert.resx "$launcher_tree/src/Arcadia.Launchers/Fake/FakeTexts.resx"
expect 1 "a launcher folder's resource file is checked" bash -c "cd '$launcher_tree' && GIT_DIR=/nonexistent '$PWD/$texts'"
rm -rf "$launcher_tree"
(( failed == 0 )) && echo "CI check self-tests passed."
exit "$failed"
