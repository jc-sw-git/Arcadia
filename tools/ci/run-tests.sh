#!/usr/bin/env bash
# Runs all tests of the built solution (Release, --no-build) in parallel processes and fails if any of them fails.
# Arcadia.App.Tests runs its window tests on Avalonia's one headless UI thread, so in one process they run one after
# another and take minutes; split into shards they run side by side, each in a process with its own UI thread. The
# other test projects run before them. Shards take every n-th test method in name order: test methods,
# not classes, so the long window tests of one class spread over all shards.
# Usage: run-tests.sh <shards> [extra dotnet test arguments, e.g. coverage]
set -uo pipefail

shards="${1:?number of App test shards}"
shift
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
app_bin=tests/Arcadia.App.Tests/bin/Release/net10.0
app_dll="$app_bin/Arcadia.App.Tests.dll"
# A test that hangs ends its run after this instead of holding the runner until the job's timeout.
common=(--blame-hang-timeout 5m --blame-hang-dump-type none "$@")

# xunit v3 test projects are programs; this lists the test methods without running them.
# Sorted bytewise: the same shards on every OS and in every language.
mapfile -t methods < <(dotnet "$app_dll" -list methods -noColor | grep -E '^Arcadia\.' | tr -d '\r' | LC_ALL=C sort)
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
statuses=()
start() {
  local name="$1"
  shift
  dotnet test "$@" "${common[@]}" --logger "trx;LogFileName=$work/$name.trx" > "$work/$name.log" 2>&1 &
  pids+=($!)
  names+=("$name")
}

# Waits for the runs started so far and keeps each one's exit code.
collect() {
  local i
  for (( i = ${#statuses[@]}; i < ${#pids[@]}; i++ )); do
    wait "${pids[i]}"
    statuses+=($?)
  done
}

# Core and Pan first, on their own: some of their tests wait on real time (a reply timeout, a pipe), and beside four
# busy shards a Windows runner's thread pool answered too late for them.
start core tests/Arcadia.Core.Tests/Arcadia.Core.Tests.csproj --configuration Release --no-build
start pan tests/Arcadia.Pan.Tests/Arcadia.Pan.Tests.csproj --configuration Release --no-build
collect


# Each shard runs from its own copy of the build: coverage instruments the assemblies in their folder, and on Windows
# shards sharing one folder lock each other's files. Run from a copy, the test needs the coverage collector named.
packages=$(dotnet nuget locals global-packages -l | sed -E 's/^global-packages: *//' | tr -d '\r')
coverlet=$(grep -oE 'coverlet\.collector" Version="[^"]+' "$app_bin/../../../Arcadia.App.Tests.csproj" | sed -E 's/.*Version="//')
collector="$packages/coverlet.collector/$coverlet/build/netstandard2.0"
for (( shard = 0; shard < shards; shard++ )); do
  cp -R "$app_bin" "$work/app-shard-$shard"
  start "app-shard-$shard" "$work/app-shard-$shard/Arcadia.App.Tests.dll" --settings "$work/shard-$shard.runsettings" \
    --test-adapter-path "$collector"
done

collect
failed=0
for i in "${!names[@]}"; do
  log="$work/${names[i]}.log"
  if (( statuses[i] == 0 )); then
    echo "::group::${names[i]} passed"
    cat "$log"
    echo "::endgroup::"
  else
    # Shown in full, not folded: the failures are in it, worded in the runner's language.
    failed=1
    echo "::error::${names[i]} failed"
    cat "$log"
  fi
done

# Coverage that was asked for but not collected would leave the report without a word.
if grep -l "Could not find data collector" "$work"/*.log; then
  echo "::error::A test run above found no data collector for the coverage asked for."
  failed=1
fi

# A name the filter doesn't match would drop its test without a word: every listed method must have run. Read from the
# result files, which are the same in every language (the console output is German on Linux, see ci.yml).
mapfile -t ran < <(cat "$work"/app-shard-*.trx 2>/dev/null | grep -oE '<TestMethod [^>]*className="[^"]+" name="[^"]+"' \
  | sed -E 's/.*className="([^"]+)" name="([^"]+)"/\1.\2/' | LC_ALL=C sort -u)
missing=$(LC_ALL=C comm -23 <(printf '%s\n' "${methods[@]}") <(printf '%s\n' "${ran[@]}"))
echo "App test methods run: ${#ran[@]} of ${#methods[@]}."
if (( failed == 0 )) && [[ -n "$missing" ]]; then
  echo "::error::App test methods that ran in no shard:"
  echo "$missing"
  failed=1
fi

exit "$failed"
