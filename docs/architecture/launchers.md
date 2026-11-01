# Architecture: `Arcadia.Launchers`

The built-in launchers; they use only `Arcadia.Launchers.Abstractions` ([core.md](core.md)), as a plugin would. Facts
about the launchers themselves: `docs/launchers/`. The other projects: [README.md](README.md).

**Shared**
- `BuiltInLaunchers`: the one place a launcher is registered. Integrations: `SteamIntegration` with
  `SteamFolderChoice`, `EpicIntegration`, `DemoIntegration` (`DemoScanner`: a fictional library per launcher).
- `DirectLauncherClient`, `GameLaunchService` (every game start, `TryStartAsync` never throws).
- `ProcessWatch` (also `PathOf(pid)`, the foreground process and the process ids), `SystemProcessTable`.

**Epic** (`Epic/`)
- `EpicScanner` (installed games from the install manifests), `EpicCatalog`, `EpicLauncherLog` (the launcher's log:
  play sessions), `EpicLastPlayedStore` (keeps "last played" after Epic's logs rotated it out), `EpicLauncherClient`,
  `EpicInstallMonitor`, `EpicRunningGames`.

**Steam** (`Steam/`)
- `SteamScanner`, `SteamInstallation`, `SteamInstallMonitor`, `SteamRunningGames`.
- Logs: `SteamContentLog`, `SteamConsoleLog` (starts Steam aborted), `SteamLoginLog`.
- Files: `TextVdf`, `BinaryVdf`, `SteamAppInfo` (app types from `appinfo.vdf`), `SteamPackageInfo` (the apps of each
  license from `packageinfo.vdf`), `SteamInputSetting` (a game's Steam Input from `localconfig.vdf`), `SteamShortcuts`.
- `Steam/Account/`: `SteamOnlineAccount` (the signed-in account, owned games, cache), `SteamStorePage` (the sign-in on
  Steam's own store and login page in Arcadia's browser, which then holds the account and a web API token),
  `SteamWebApi`, `SteamAccountStore` (DPAPI), `SteamToken`, `SteamHiddenGames`.
