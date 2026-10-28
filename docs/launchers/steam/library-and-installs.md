# Steam — library and installs

**Steam library** (`SteamScanner`, verified 2026-10-12 against the owner's 3 libraries): libraries from
`steamapps/libraryfolders.vdf` (`path`), one `appmanifest_<appid>.acf` per installed app. `StateFlags` bit 2 =
update pending (4 = installed, 6 = with update); a first install without `buildid` is not shown.
An app nobody owns any more keeps its manifest and files, but Steam writes `LastOwner` `0` and hides it from the
library (2026-10-20: the Half-Life 2 episodes and Lost Coast after Valve merged them into Half-Life 2, and Plan B:
Terraform, which the owner never bought); Arcadia hides it too. Owned apps carry the account's SteamID64 (family
sharing not checked yet, presumably the lender's). These four were the only manifests with `StateFlags` bit 64 (68/70); what the bit means is unknown,
so only `LastOwner` decides. Playtime (minutes) and `LastPlayed` come from `userdata/<user>/config/localconfig.vdf` →
`UserLocalConfigStore/Software/Valve/Steam/apps/<appid>`; that file also holds app tickets — never log it.
Steam stores no install date (the game folder's creation time is used). Owned but uninstalled games are **not**
readable locally: `appcache/packageinfo.vdf` keeps packages after a refund (owner's 007 First Light), and the real
license list (`userdata/<id>/config/licensecache`) is encrypted — they come with a Steam login (#83, Account below). Libraries are re-read on every scan,
so added or missing drives need no handling of their own.
**Types** (#141, checked 2026-10-27 against the owner's files): app manifests hold no type; it is
`appinfo/common/type` in the binary `appcache/appinfo.vdf` (`SteamAppInfo`; versions 0x07564427/28/29, v29 with a
string table for the keys). Only the installed app ids are parsed, every other entry is skipped by its size; cached by
the file's length and write time. The owner's file: v29, 3.9 MB, all 81 installed apps typed in 13 ms; the type's case
varies (`game`, `Game`, `Demo`, `Music`, `Tool`). `Game`/`Demo` → Spiel,
`Application` → Software, `Tool` → Werkzeug, `Music` → Soundtrack, anything else is left out, as are runtimes and
redistributables by id and name (Proton, Steam Linux Runtime, Steamworks Common). Without a readable file the types
come from ids and names (tools as before, a name ending in "Soundtrack"), software counts as a game, and one warning
goes to arcadia.log. A soundtrack has its own manifest and lives in `steamapps/music/<installdir>`, with the same folder
name as its game in `common` (RuneScape: Dragonwilds' soundtrack: `music/RSDragonwilds`), so `music` wins for a
soundtrack. Steam types SDKs (Source SDK Base 2013) and the merged Half-Life 2 episodes as `Tool`, the redistributables
(228980) too. `steam://rungameid/<id>` plays a soundtrack in Steam's player (owner's PC, 2026-10-27: `console_log.txt` shows the
launch ending in `ExecuteSteamURL: "steam://musicplayer/playappalbum/<id>"`); Arcadia then also opens its page
(`steam://nav/games/details/<id>`, owner's wish), 2 s later: each link starts a `steam.exe` that hands it to the running
Steam, and of two links opened at once Steam got only the second (its log showed no `rungameid`, owner's PC 2026-10-27).
`SteamInstallMonitor` (verified 2026-10-12 with real installs): a new install's manifest appears with `StateFlags` 2
and `buildid` 0, then 1026 (+1024 started) while downloading *or* waiting, 1538 (+512) when paused, 4 when done;
an update of an installed game is 1030. Steam downloads one app at a time but flags all waiting ones alike, so the
app whose `BytesDownloaded`/`BytesStaged` moved last is the downloading one. Cancelling a new install and
uninstalling (38 for a moment) both delete the manifest. Steam updates runtimes/redistributables on its own —
they are filtered like in the scanner.
The manifest's byte counters are written only on a state change (an update of Schedule I: 0 at the start, complete at
the end), and the paused flag came late or not at all (#278), so the monitor takes what a download does from
`logs/content_log.txt` (`SteamContentLog`), which Steam writes the moment it happens (owner's PC, 2026-10-27):

| Line | Meaning |
|---|---|
| `AppID <id> state changed : …,Update Running,…` | Steam works on the app (a waiting or paused one lacks it) |
| `App update changed : Running Update,Preallocating,` (also `Verifying Installed`, `Reconfiguring`) | started, not downloading yet |
| `update started : download 1485691824/5899237456, store …, stage …` | start or resume; the exact download bytes (the number Steam's download page shows) |
| `App update changed : Running Update,Downloading,Staging,` | downloading |
| `update canceled : Disabled (Suspended)` | paused by the user, also on a logoff |
| `update canceled : Priority (Suspended)` | another download took over: queued |
| `update canceled : Uninstall (Canceled)` | dropped for good |
| `App update changed : …Verifying Staged` / `…Committing` | data complete, files verified or moved |
| `finished update` | done |
| `Client version: …` | Steam started anew (restart, account switch); queued apps follow as `state changed : …Update Queued,Update Started,` |

There is **no per-app progress** while a download runs: only a global `Current download rate: X Mbps` about every 60 s.
An account switch writes no login line; the running download gets `Disabled (Suspended)` in the same second, then a new
session starts that doesn't mention the other account's downloads, so Arcadia shows them as paused. Arcadia shows a
percentage only when it is exact (owner's decision): paused or queued, the larger of the manifest's and the last
`update started` download bytes; running, none.
**`steamapps/downloading/<id>` is no live source** (check script on the owner's PC, 2026-10-27, a 6.5 GB new install):
Steam preallocates every file there before downloading (`preallocated 493 files (7510 MB)`), so the folder holds the
whole staging size (7,875,211,774 of 7,888,336,863 bytes) from the first second, and the size on disk is the same; in
4 minutes it grew by 1.6 MB while Steam went from 0 to 4 %. A pause rewrote the manifest at once
(`BytesDownloaded` 139,259,776 = the next `update started`; Steam showed 2 %); while running it stayed unchanged.
Steam has no update-only URI: `steam://install/<id>` is ignored for installed games, `steam://validate/<id>` updates
but first verifies every file (and resets modified ones).

`steam://controllerconfig/<id>` is unreliable (owner's PC, 2026-10-18): Steam goes to the game in its library, but
often, mostly on the first call, the controller configurator doesn't open; a second call mostly works. Cause unknown,
so Arcadia offers no "Controller-Layout" action for now (owner's decision).

