# Architecture: `Arcadia.Launchers`

The built-in launchers; they use only `Arcadia.Launchers.Abstractions` ([core.md](core.md)), as a plugin would. Facts
about the launchers themselves: `docs/launchers/`. The other projects: [README.md](README.md).

**Shared**
- `BuiltInLaunchers`: the one place a launcher is registered; `KnownLaunchers` its launchers in tab order (Steam and
  Epic build their own `LauncherInfo`, the others are demo tabs). Integrations: `SteamIntegration` with
  `SteamFolderChoice` and `SteamProgram`, `EpicIntegration`, `DemoIntegration` (`DemoScanner`: a fictional library per
  launcher).
- `Demo/DemoArtwork`: made-up covers and backgrounds for `--demo-games`, so the budgets are measured with images (#487);
  every fourth in Epic's formats, 3 : 4 and 16 : 9, to look at the crop (#488).
- `DirectLauncherClient`, `GameLaunchService` (every game start, `TryStartAsync` never throws).
- Arcadia's data folder comes in from the App (`BuiltInLaunchers.Create`), as a plugin would get it.

**Epic** (`Epic/`)
- `EpicScanner` (installed games from the install manifests), `EpicCatalog`, `EpicLauncherLog` (the launcher's log:
  play sessions), `EpicLastPlayedStore` (keeps "last played" after Epic's logs rotated it out), `EpicLauncherClient`,
  `EpicInstallMonitor`, `EpicRunningGames`.
- `EpicArtwork`: the `keyImages` of Epic's catalog cache, only on Epic's image hosts (#488, `docs/launchers/epic.md`).

**Steam** (`Steam/`)
- `SteamArtwork`: the account's own images, Steam's library cache, Steam's image server (#487,
  `docs/launchers/steam/artwork.md`).
- `SteamScanner`, `SteamInstallation`, `SteamInstallMonitor`, `SteamRunningGames`, `SteamProgram` (Steam the
  program: running, ready, closed and started again, its running game, `shortcuts.vdf` entries).
- Logs: `SteamContentLog`, `SteamConsoleLog` (starts Steam aborted), `SteamLoginLog`.
- Files: `TextVdf`, `BinaryVdf`, `SteamAppInfo` (app types from `appinfo.vdf`), `SteamPackageInfo` (the apps of each
  license from `packageinfo.vdf`), `SteamInputSetting` (a game's Steam Input from `localconfig.vdf`), `SteamShortcuts`.
- `Steam/Account/`: `SteamOnlineAccount` (the signed-in account, owned games, cache), `SteamStorePage` (the sign-in on
  Steam's own store and login page in Arcadia's browser, which then holds the account and a web API token),
  `SteamWebApi`, `SteamAccountStore` (DPAPI), `SteamToken`, `SteamHiddenGames`.
