# Epic — findings

Read before touching `src/Arcadia.Launchers/Epic`. Verified on the owner's PC.

**Epic:** all locally installed games have `bRequiresAuth` → they need the launcher. If it isn't
running, `EpicLauncherClient` starts it with `-silent`, waits for `EpicWebHelper` + 4 s, starts
the game via `com.epicgames.launcher://…` and re-sends once if no process appears from the
install directory within 30 s. If the web helper doesn't appear within 60 s, it reports `LauncherNotReady`
(toast) and still sends the request once.
Owned but uninstalled games come from `Data\Catalog\catcache.bin` (Base64 JSON, refreshed by the
launcher, no login needed). Games = category `games` without `addons`/`digitaldownloads` and with
an empty `mainGameItem.id` (DLC/season passes name their game there): 331 on the owner's PC vs.
333 in the Epic library — the difference is Unreal Engine (one tile for all `engines` versions) and Unreal Editor for Fortnite (`addons/launchable`), which Epic shows but which are no games. Epic's own library list is cached in `webcache_*/IndexedDB/https_launcher.store.epicgames.com_0.indexeddb.blob` (V8-serialized, 431 apps incl. DLC). The cache is **ASCII-only** — every other
character is `?` ("Batman? Arkham", "K?ln"); `EpicCatalog.RepairTitle` drops trademark `?`s,
umlauts stay broken until metadata providers (#43) supply titles. Install: `…?action=install`.
`EpicInstallMonitor` (verified 2026-10-10 with real installs): queue order from
`Data\DownloadManager\DownloadState_<account>.json` (incl. paused, `[]` when idle); per-app events
from `EpicGamesLauncher.log` (`Queued install for the`, `StartTask: BinaryId [`, `Resume of active
installation`, `Pause is requested`, `Cancel of active installation`, `HandleTaskComplete … AlertCode=[ok]`,
`FCommunityPortalAppManagerImpl: Uninstall` — an uninstall then runs through the same queue with
the same `Queued`/`StartTask`/`HandleTaskComplete` lines, so only that first line tells it apart); install size from
`Manifests\Pending\*.item`, which **stays behind after a cancel** (never use it as state). The
queue file can lag behind (owner saw a running install vanish after cancelling a paused one), so
for apps the log reported on while Arcadia ran, the log decides; the file only orders them.
Reordering logs `New active position is requested for the <app>` followed by a pause of the old one. Resuming a paused app while
another downloads logs only `Resume of active installation` for the new one; the running one is cancelled
without a pause line (verified 2026-10-12), so only the latest `StartTask` counts as downloading. Progress =
size of the whole install dir / `InstallSize` (download goes to `.egstore` inside it, moved at the end).
Epic writes no percentages to the log.
Epic keeps total playtime only on its servers (login: #82). Locally the logs (`EpicGamesLauncher*.log`, current plus a few
rotated backups, UTC times) have `LogPortalPublishTime: Started/Stopped tracking playtime for <ns>:<item>:<app>` per
session — Arcadia keeps the latest start per app in `%APPDATA%\Arcadia\epic-last-played.json` because the backups
rotate away — and, on each launcher start, `Sending response: {…"get-installed-apps-response"…}` with
`installedVersion` and `hasUpdate` per app; a report whose version differs from the manifest's `AppVersionString` is stale. An update has no progress measure (the install dir
is full from the start), so `EpicInstallMonitor` reports it without a fraction. The library is also rescanned when the window is activated
and when a selected installed game's directory is missing.
