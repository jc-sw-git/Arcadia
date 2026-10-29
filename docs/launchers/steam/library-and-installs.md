# Steam — library and installs

## Library (`SteamScanner`)

Libraries from `steamapps/libraryfolders.vdf` (`path`), one `appmanifest_<appid>.acf` per installed app; libraries are
re-read on every scan, so added or missing drives need no handling of their own. `StateFlags` bit 2 = update pending
(4 = installed, 6 = with update); a first install without `buildid` is not shown. An app nobody owns any more keeps its
manifest and files, but Steam writes `LastOwner` `0` and hides it from the library (e.g. the Half-Life 2 episodes after
Valve merged them into Half-Life 2); Arcadia hides it too. Owned apps carry the account's SteamID64 (family sharing not
checked, presumably the lender's); such manifests also had `StateFlags` bit 64, meaning unknown, so only `LastOwner`
decides. Playtime (minutes) and `LastPlayed` come from `userdata/<user>/config/localconfig.vdf` →
`UserLocalConfigStore/Software/Valve/Steam/apps/<appid>`; that file also holds app tickets — never log it.
Steam stores no install date (the game folder's creation time is used). Owned but uninstalled games are **not**
readable locally: `appcache/packageinfo.vdf` keeps packages after a refund, and the real license list
(`userdata/<id>/config/licensecache`) is encrypted — they come with a Steam login (`account.md`).

## Types (#141)

App manifests hold no type; it is `appinfo/common/type` in the binary `appcache/appinfo.vdf` (`SteamAppInfo`; versions
0x07564427/28/29, v29 with a string table for the keys). Only the installed app ids are parsed, every other entry is
skipped by its size; cached by the file's length and write time (a 3.9 MB v29 file: 81 apps typed in 13 ms). The
type's case varies (`game`, `Game`, `Demo`, `Music`, `Tool`). `Game`/`Demo` → Spiel,
`Application` → Software, `Tool` → Werkzeug, `Music` → Soundtrack, anything else is left out, as are runtimes and
redistributables by id and name (Proton, Steam Linux Runtime, Steamworks Common). Without a readable file the types
come from ids and names (tools as before, a name ending in "Soundtrack"), software counts as a game, and one warning
goes to arcadia.log. A soundtrack has its own manifest and lives in `steamapps/music/<installdir>`, with the same folder
name as its game in `common`, so `music` wins for a soundtrack. Steam types SDKs (Source SDK Base 2013) and the merged
Half-Life 2 episodes as `Tool`, the redistributables (228980) too. `steam://rungameid/<id>` plays a soundtrack in
Steam's player (`console_log.txt`: `ExecuteSteamURL: "steam://musicplayer/playappalbum/<id>"`); Arcadia then also opens
its page (`steam://nav/games/details/<id>`) 2 s later: each link starts a `steam.exe` that hands it to the running
Steam, and of two links opened at once Steam got only the second.

## Installs (`SteamInstallMonitor`)

A new install's manifest appears with `StateFlags` 2 and `buildid` 0, then 1026 (+1024 started) while downloading *or*
waiting, 1538 (+512) when paused, 4 when done; an update of an installed game is 1030. Steam downloads one app at a
time but flags all waiting ones alike, so the app whose `BytesDownloaded`/`BytesStaged` moved last is the downloading
one. Cancelling a new install and uninstalling (38 for a moment) both delete the manifest. Steam updates
runtimes/redistributables on its own — they are filtered like in the scanner.
The manifest's byte counters are written only on a state change (0 at the start, complete at the end), and the paused
flag came late or not at all (#278), so the monitor takes what a download does from `logs/content_log.txt`
(`SteamContentLog`), which Steam writes the moment it happens:

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
**`steamapps/downloading/<id>` is no live source:** Steam preallocates every file there before downloading, so the
folder holds the whole staging size from the first second (size on disk too); a pause rewrites the manifest at once
(`BytesDownloaded` = the next `update started`), while running it stays unchanged.
Steam has no update-only URI: `steam://install/<id>` is ignored for installed games, `steam://validate/<id>` updates
but first verifies every file (and resets modified ones).

`steam://controllerconfig/<id>` is unreliable: Steam goes to the game in its library, but often, mostly on the first
call, the controller configurator doesn't open; a second call mostly works. Cause unknown, so Arcadia offers no
"Controller-Layout" action for now.

