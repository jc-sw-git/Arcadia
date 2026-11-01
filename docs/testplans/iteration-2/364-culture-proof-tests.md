# #364 Tests fail on a German Windows: culture-dependent formatting in tests and in Pan's log

No new manual test: nothing changes for the player, and the suite itself is the check. CI's Linux job now runs the tests
under `LANG=de_DE.UTF-8` (decimal comma), the Windows job under en-US, so a culture-dependent expectation fails in CI.

## Test runs

| Tests | Date | By | Environment | Result | Notes |
|---|---|---|---|---|---|
| suite under `LANG=de_DE.UTF-8` and under the container's default (en-US); before the fix 14 red under de-DE (13 in `MouseModeTests`/`ReturnWithViewTests`, 1 in `HidingTests`) | 2026-11-01 | Claude (thread) | Linux cloud container, .NET 10, Debug | pass | de-DE: Pan 330, Core 2276, App 491 green, 11 skipped (Windows only); en-US: none red |
| suite in CI on e2088a2 | 2026-11-01 | Claude (thread) | GitHub runners: ubuntu-latest with `LANG=de_DE.UTF-8` (output German, so the culture reached .NET), windows-latest en-US | pass | Linux: Pan 331, Core 2292, App 491 green |
