# #634 Start the demo libraries without Steam and Epic and in a data folder of their own

**Status:** Automated: T634-1, T634-2, T634-3 · QE: T634-4 · Open for the owner: none

- [x] **T634-1** `--data-folder` moves every path Arcadia writes (settings, logs; logos, artwork, metadata and sign-ins derive from the same folder) · E2E: yes
- [x] **T634-2** The option reads the folder after it and a restart keeps it · E2E: yes
- [x] **T634-3** `--demo` loads no Steam and no Epic: all launchers are demo libraries, also with `--demo-games 5000` · E2E: yes
- [x] **T634-4** `--demo --demo-games 5000 --data-folder <temp>` shows 5,000 games and touches no file outside the temp folders · E2E: no (window)

Automated tests:

| Test cases | Tests |
|---|---|
| T634-1 | `LogLocationTests.An_own_data_folder_moves_the_settings_and_the_logs_below_it` |
| T634-2 | `CommandLineTests` (data folder, restart) |
| T634-3 | `LauncherRegistryTests` (built-in launchers, large demo library) |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T634-1 to T634-3 | 2026-11-18 | Claude | PR branch, Debug, Linux cloud container | ✅ `dotnet test`: all passed | – |
| T634-4 | 2026-11-18 | Claude | PR branch, Debug, Linux cloud container, xvfb | ✅ `--demo --demo-games 5000 --data-folder <temp>`: log "5039 Spiele aus 8 Launchern"; settings, logs and metadata cache only below the folder; the only other new files were the graphics driver's shader cache (not Arcadia's). | – |
| T634-1 to T634-4 (QE) | 2026-11-18 | Claude (QE) | PR branch 4125828, Release, Linux cloud container, xvfb, separate HOME | ✅ build without warnings, `dotnet test` all passed (App, Core, Pan); `--demo --demo-games 5000 --data-folder <temp>`: 5039 games from 8 launchers, new files only below the folder and in the OS temp folder (`arcadia-demo-artwork`), nothing in the profile's config or data folders. Windows-only parts (no Pan connection, no VIIPER, `ProgramData` untouched) checked in the code only; the measuring run on the owner's PC (#109, #379) sees them for real | – |
