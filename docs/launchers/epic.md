# Epic — findings

Read before touching `src/Arcadia.Launchers/Epic`. Verified on the owner's PC.

## Starting a game

All locally installed games have `bRequiresAuth` → they need the launcher. If it isn't running, `EpicLauncherClient`
starts it with `-silent`, waits for `EpicWebHelper` + 4 s, starts the game via `com.epicgames.launcher://…` and
re-sends once if no process appears from the install directory within 30 s. If the web helper doesn't appear within
60 s, it reports `LauncherNotReady` (toast) and still sends the request once. Install: `…?action=install`.

## Catalog: owned games, engines, add-ons

- Owned but uninstalled games come from `Data\Catalog\catcache.bin` (Base64 JSON, refreshed by the launcher, no login
  needed). Games = category `games` without `addons`/`digitaldownloads` and with an empty `mainGameItem.id` (DLC and
  season passes name their game there). Epic's own library list (`webcache_*/IndexedDB/…indexeddb.blob`, V8-serialized)
  also counts Unreal Engine and Unreal Editor for Fortnite, which are no games.
- The cache is **ASCII-only**: every other character is `?` ("Batman? Arkham", "K?ln"); `EpicCatalog.RepairTitle` drops
  trademark `?`s, umlauts stay broken until metadata providers (#43) supply titles.
- With the launcher's switch on (#141) both come as tools: engines by category `engines`, namespace `ue` with a title
  starting "Unreal Engine", or an app name `UE_<version>`; every installed version is one entry "Unreal Engine" whose A
  starts the newest (by `UE_5.4`, else the leading numbers of `AppVersionString`), not installed the newest catalog
  version. Launchable add-ons (UEFN: `Fortnite_Studio`) count by `addons/launchable` even though their
  `mainGameItem`/`MainGameAppName` names a game. Installing an engine queues its plugins (`QuixelBridge_<v>`,
  `FabPlugin_<v>`, categories `plugins/engine`) as followers with their own manifests in the engine's folder.
- `…?action=install` for an engine logs "dispatching install" but shows no dialog, so Arcadia opens
  `com.epicgames.launcher://ue/library` instead; no dialog opens there either: the user starts the installation
  (`Game.InstallStartsInLauncher`), and only then does it show in Arcadia with its progress.

## Installs (`EpicInstallMonitor`, verified with real installs)

- Queue order from `Data\DownloadManager\DownloadState_<account>.json` (incl. paused, `[]` when idle); per-app events
  from `EpicGamesLauncher.log` (`Queued install for the`, `StartTask: BinaryId [`, `Resume of active installation`,
  `Pause is requested`, `Cancel of active installation`, `HandleTaskComplete … AlertCode=[ok]`,
  `FCommunityPortalAppManagerImpl: Uninstall` — an uninstall then runs through the same queue with the same
  `Queued`/`StartTask`/`HandleTaskComplete` lines, so only that first line tells it apart).
- Install size from `Manifests\Pending\*.item`, which **stays behind after a cancel** (never use it as state).
- The queue file can lag behind (a running install vanished after cancelling a paused one), so for apps the log reported
  on while Arcadia ran, the log decides; the file only orders them.
- Followers: an add-on installed before its game (UEFN before Fortnite) or an engine's plugins are queued as `Queued
  follower install for the <app>`; once the main app is done Epic logs `Not persisting follower [<app>]` and drops them
  from the queue file while it still downloads them, in one task whose `StartTask` lines name the main app first,
  followed by `Skipping OnInstallBegin broadcast for unchanged action <main app>`. So a follower is open by the log
  alone, and the main app is not. An add-on (`MainGameAppName` set) installs into its game's folder, so its progress
  has no fraction.
- Reordering logs `New active position is requested for the <app>` followed by a pause of the old one. Resuming a paused
  app while another downloads logs only `Resume of active installation` for the new one; the running one is cancelled
  without a pause line, so only the latest `StartTask` counts as downloading.
- Progress = size of the whole install dir / `InstallSize` (download goes to `.egstore` inside it, moved at the end).
  Epic writes no percentages to the log. An update has no progress measure (the install dir is full from the start), so
  the monitor reports it without a fraction.

## Updates, playtime, rescans

- On each launcher start the log has `Sending response: {…"get-installed-apps-response"…}` with `installedVersion` and
  `hasUpdate` per app; a report whose version differs from the manifest's `AppVersionString` is stale.
- Epic keeps total playtime only on its servers (login: #82). Locally the logs (`EpicGamesLauncher*.log`, current plus
  a few rotated backups, UTC) have `LogPortalPublishTime: Started/Stopped tracking playtime for <ns>:<item>:<app>` per
  session; Arcadia keeps the latest start per app in `%APPDATA%\Arcadia\epic-last-played.json` because the backups
  rotate away.
- The library is also rescanned when the window is activated and when a selected installed game's directory is
  missing.

## Running games (#95)

`EpicRunningGames` reads the current `EpicGamesLauncher.log`: an app with a `Started tracking playtime` line and no
`Stopped` line after it runs, since the time of that line. Each poll reads only what was added since the last one (up
to the last complete line); a log whose first bytes differ from those read before, or that got shorter, is a new one
(Epic starts a new log with every launcher start) and is read from its start. If the launcher crashes while a game
runs, the game shows "Läuft" until the launcher's next start writes a new log. The process search over the install
folder doesn't run for Epic games (only a fallback for launchers without a signal).
