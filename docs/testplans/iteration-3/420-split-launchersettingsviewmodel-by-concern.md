# #420 Split LauncherSettingsViewModel by concern

No new test cases: refactoring. The category "Launcher" behaves as before; every App, Core and Pan test stays green
with its assertions unchanged. It touches the cases of the launcher list and page: T83-1 to T83-7 (account),
T136-2 to T136-16 (list, order, rescan), T137-1 to T137-8 (page), T141-1 to T141-13 (library switches), T169-1,
T169-3, T178-1 to T178-10 (controller mode and reset), T180-2, T208-1 to T208-3, T209-3, T228-4, T315-1 to T315-7
(Steam's desktop layout).

Uploading and restoring a logo and picking a folder (T138-2, T138-3) had no automated test, so moving them was not
covered. `LauncherLogoAndFolderTests` now holds them through a fake system dialog: upload and restore with the copy
deleted and the focus back on "Logo", a file that is no picture and a cancelled dialog, a wrong folder, a folder that
switches an inactive launcher on at the end, an active one read again, and no second dialog while one is open, across
"Logo" and "Ordner". Green on `main` and on this branch; each red once with the code broken on purpose.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| All App, Core and Pan tests | 2026-11-03 | Claude | PR branch, Release, Linux cloud container, Avalonia headless | ✅ 3,256 passed, 24 skipped (with `LauncherLogoAndFolderTests`) | – |
| `LauncherLogoAndFolderTests` against `main` | 2026-11-03 | Claude | `main` with the new tests copied in, same environment | ✅ 6 passed: they hold the old behaviour | – |
| Focus latency, view model level: 400 presses each, before (`main`) / after | 2026-11-03 | Claude | Release, Linux cloud container, Avalonia headless, no rendering | ✅ list ▼ p99 0.08 / 0.01 ms, page ▼ p99 0.10 / 0.01 ms, LT/RT p99 0.55 / 0.20 ms, page open and close p99 1.2 / 0.5 ms; all far below 50 ms, the difference is noise | – |
| QE run of the app: launcher list, lift and move, page, LT/RT | 2026-11-03 | Claude | b37f361, Linux, Xvfb, `--demo`, keys for the pad | ✅ as before, no error in `arcadia.log` | – |
| Owner cases: Steam account sign-in and sign-out; Steam Controller mode and its reset with Pan on; logo upload in the Windows file dialog and restore | 2026-11-03 | owner | PR branch, Windows, controller | ✅ passed | – |
