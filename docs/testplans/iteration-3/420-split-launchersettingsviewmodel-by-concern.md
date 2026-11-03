# #420 Split LauncherSettingsViewModel by concern

No new test cases: refactoring. The category "Launcher" behaves as before; every App, Core and Pan test stays green
with its assertions unchanged. It touches the cases of the launcher list and page: T83-1 to T83-7 (account),
T136-2 to T136-16 (list, order, rescan), T137-1 to T137-8 (page), T141-1 to T141-13 (library switches), T169-1,
T169-3, T178-1 to T178-10 (controller mode and reset), T180-2, T208-1 to T208-3, T209-3, T228-4, T315-1 to T315-7
(Steam's desktop layout).

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| All App, Core and Pan tests | 2026-11-03 | Claude | PR branch, Release, Linux cloud container, Avalonia headless | ✅ 3,251 passed, 24 skipped | – |
| Focus latency, view model level: 400 presses each, before (`main`) / after | 2026-11-03 | Claude | Release, Linux cloud container, Avalonia headless, no rendering | ✅ list ▼ p99 0.08 / 0.01 ms, page ▼ p99 0.10 / 0.01 ms, LT/RT p99 0.55 / 0.20 ms, page open and close p99 1.2 / 0.5 ms; all far below 50 ms, the difference is noise | – |
