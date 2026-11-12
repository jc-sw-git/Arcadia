# #510 Steam tab lists owned games twice when the account also owns their Mac-only app

**Status:** Automated: T510-1, T510-2 · QE: T510-1, T510-2 · Open for the owner: – (T510-3 passed)

- [x] **T510-1** Given an account that owns a game for Windows and its Mac port under another id with the same name (in
  `appinfo.vdf`: `oslist` `macos`, `VisibleOnlyOnAvailablePlatforms` 1), when the Steam tab is scanned on Windows, then the
  game shows once, as the Windows app; on Linux a Windows-only app stays (Proton), a Mac-only one is left out there too.
  · E2E: no (scanner test with a recreated Steam folder and a fake web API; the UI adds nothing)
- [x] **T510-2** `appinfo.vdf` v27 and v29: the platforms are read only with the flag set; an app without the flag, or
  with the flag but no `oslist`, is shown everywhere; case of the platform name doesn't matter. · E2E: no (unit test)
- [x] **T510-3** **Owner:** with your Steam account signed in, the Steam tab shows "Grand Theft Auto III", "Grand Theft
  Auto: Vice City" and "Grand Theft Auto: San Andreas" once each, and A on each opens Steam's install dialog of the
  Windows game. · E2E: no (real Steam account)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T510-1, T510-2 | 2026-11-12 | Claude | PR branch, Linux container, `dotnet test` | ✅ (T510-1 red before the fix) | – |
| T510-1 | 2026-11-12 | Claude | PR branch, Linux container: `SteamAppInfo` over a copy of the owner's `appinfo.vdf` and owned-games list | ✅ hidden on Windows and Linux: exactly 12230, 12240, 12250 of all owned apps | – |
| T510-1, T510-2 | 2026-11-12 | Claude (QE) | PR #513 at 2d57dab, Linux container, Release, `run-tests.sh 4` | ✅ red once each: filter removed in `SteamScanner`, Proton rule removed in `SteamAppEntry.IsHiddenOn` | – |
| T510-3 | 2026-11-12 | owner | PR #513, real PC, real Steam account | ✅ | – |
