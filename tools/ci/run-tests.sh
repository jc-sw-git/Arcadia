#!/usr/bin/env bash
# Runs all tests of the built solution (Release, --no-build) in parallel processes and fails if any of them fails.
# Arcadia.App.Tests runs its window tests on Avalonia's one headless UI thread, so in one process they run one after
# another and take minutes; split into shards they run side by side, each in a process with its own UI thread. The
# other test projects run beside them. Shards take every n-th test method in name order: test methods,
# not classes, so the long window tests of one class spread over all shards.
# Usage: run-tests.sh <shards> [extra dotnet test arguments, e.g. coverage]
set -uo pipefail

shards="${1:?number of App test shards}"
shift
work="$(mktemp -d)"
app_tests=tests/Arcadia.App.Tests
app_dll="$app_tests/bin/Release/net10.0/Arcadia.App.Tests.dll"
# A test that hangs ends its run after this instead of holding the runner until the job's timeout.
common=(--configuration Release --no-build --blame-hang-timeout 5m --blame-hang-dump-type none "$@")

# xunit v3 test projects are programs; this lists the test methods without running them.
mapfile -t methods < <(dotnet "$app_dll" -list methods -noColor | grep -E '^Arcadia\.' | tr -d '\r' | sort)
if (( ${#methods[@]} == 0 )); then
  echo "::error::No test methods found in $app_dll"
  exit 1
fi

# The filter goes in a run settings file: on Windows a command line holding hundreds of names is too long.
for (( shard = 0; shard < shards; shard++ )); do
  filter=""
  for (( i = shard; i < ${#methods[@]}; i += shards )); do
    filter+="${filter:+|}FullyQualifiedName=${methods[i]}"
  done
  cat > "$work/shard-$shard.runsettings" <<EOF
<RunSettings><RunConfiguration><TestCaseFilter>$filter</TestCaseFilter></RunConfiguration></RunSettings>
EOF
done
echo "${#methods[@]} App test methods in $shards shards."

pids=()
names=()
start() {
  local name="$1"
  shift
  dotnet test "$@" "${common[@]}" > "$work/$name.log" 2>&1 &
  pids+=($!)
  names+=("$name")
}

start core tests/Arcadia.Core.Tests/Arcadia.Core.Tests.csproj
start pan tests/Arcadia.Pan.Tests/Arcadia.Pan.Tests.csproj
for (( shard = 0; shard < shards; shard++ )); do
  start "app-shard-$shard" "$app_tests/Arcadia.App.Tests.csproj" --settings "$work/shard-$shard.runsettings"
done

failed=0
for i in "${!pids[@]}"; do
  if wait "${pids[i]}"; then
    result=passed
  else
    result=FAILED
    failed=1
  fi
  echo "::group::${names[i]} $result"
  cat "$work/${names[i]}.log"
  echo "::endgroup::"
  if [[ $result == FAILED ]]; then
    echo "::error::${names[i]} failed:"
    grep -E '^\s*(Failed|Error Message|The active test run was aborted)' "$work/${names[i]}.log" | head -40
  fi
done

# A name the filter doesn't match would drop its test without a word: every method runs at least once.
app_total=$(cat "$work"/app-shard-*.log | grep -oE 'Total: +[0-9]+' | awk '{ sum += $2 } END { print sum + 0 }')
echo "App tests run: $app_total cases of ${#methods[@]} methods."
if (( failed == 0 && app_total < ${#methods[@]} )); then
  echo "::error::The App test shards ran fewer tests than there are test methods."
  failed=1
fi

exit "$failed"
