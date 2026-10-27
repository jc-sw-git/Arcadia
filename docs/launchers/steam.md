# Steam — findings

Read before touching `src/Arcadia.Launchers/Steam`. Verified on the owner's PC.

## Shortcuts (2026-10-09)

- Steam shortcut app ids in modern Steam are *not* CRC32(exe+name) — read the stored `appid`.
  `rungameid` = `(appid << 32) | 0x02000000`. Steam writes `shortcuts.vdf` only on exit;
  never write it while Steam runs. `BinaryVdf` round-trips the real file byte for byte.

## Finding Steam (2026-10-23, #138)

`SteamInstallation.Find` reads `HKCU\Software\Valve\Steam\SteamPath`, else `%ProgramFiles(x86)%\Steam`. A Steam without
registry entry (e.g. copied to another drive) is picked by hand on Steam's launcher page: the folder must hold
`steam.exe` and `steamapps`, or be `steamapps` itself (its parent is taken); it is checked when picked and only then
saved (`LauncherPreferences.Folder`). Once set, `ChosenRoot` is the only place `Find` looks; a folder that has gone
means "not found", not a fall back to the registry. Such a Steam has no logged-in account in the registry. Not yet
verified with a real Steam outside the registry.

## Library and installs

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
The manifest's byte counters are written rarely (an update of Schedule I: 0 at the start, complete at the end), so the
monitor also reads `logs/content_log.txt` (`SteamContentLog`): `AppID <id> state changed : …,Update Running,…` marks
the app Steam works on (a waiting or paused one lacks it), `App update changed : …Verifying Staged|Committing` the end.
Steam has no update-only URI: `steam://install/<id>` is ignored for installed games, `steam://validate/<id>` updates
but first verifies every file (and resets modified ones).

`steam://controllerconfig/<id>` is unreliable (owner's PC, 2026-10-18): Steam goes to the game in its library, but
often, mostly on the first call, the controller configurator doesn't open; a second call mostly works. Cause unknown,
so Arcadia offers no "Controller-Layout" action for now (owner's decision).

## Account (#83, 2026-10-27, partly checked against the real Steam)

`Steam/Account/`: sign-in on Steam's own store login page in Arcadia's browser (`IWebPages`, a system web view:
WebView2 / WebKitGTK, profile in `%APPDATA%\Arcadia\WebView`), the way Playnite does it. Every store page then holds
`#application_config` with `data-userinfo` (`logged_in`, `steamid`) and `data-store_user_config` (`webapi_token`, a JWT:
`sub` = SteamID64, `exp`, about a day); `SteamStorePage` reads both. A new token: reload `store.steampowered.com/explore/`
unseen (Steam renews its own cookies there); a page signed out or in with another account means the sign-in is gone.
Sign-out deletes the cookies of `steampowered.com`/`steamcommunity.com`. **Never pose as another app:** the first #83
build signed in by QR as Steam's phone app (`MobileApp`, refresh token, token as `steamLoginSecure` cookie), and Steam
restricted the owner's account as possibly stolen the same day (2026-10-27); such a stored sign-in is deleted at start.
Owned games: `IPlayerService/GetOwnedGames` with `access_token`, `include_appinfo`, `include_played_free_games`,
`include_free_sub`. Owner's account (T83-3, 2026-10-27): 413 apps, games and software only, no soundtracks or tools,
and fewer games than Steam's "Spiele" count. So Arcadia also reads the store's `dynamicstore/userdata` (`rgOwnedApps`,
every owned app incl. DLC; read unseen in the browser, so the store's own cookies count) and
names and types those apps from `appinfo.vdf`; one `appinfo.vdf` doesn't know is left out. Still missing (2026-10-27):
free tools and soundtracks only a package holds (169 tools in Steam, 120 from package 0). So the store's
`rgOwnedPackages` plus package 0 are resolved to apps via `appcache/packageinfo.vdf` (`SteamPackageInfo`, v39/v40, no
entry size); of package 0 only tools count (Steam hides its applications, e.g. Source Filmmaker). `ISteamUser/GetPlayerSummaries`
answers HTTP 400 to an access token (it wants a web API key); the profile name comes from
`IPlayerService/GetPlayerLinkDetails` (`public_data.persona_name`).
Games hidden in Steam: `userdata/<id>/config/cloudstorage/cloud-storage-namespace-1.json`, a JSON list of
`[key, {key, value, …}]`; the entry `user-collections.hidden` holds the collection as a JSON string, app ids in
`added` (owner's PC: 7 ids, plain text). Another account in the running Steam (`ActiveUser`, read every 2 s) keeps the
games, but blocks installing (`steam://install` installs into Steam's account) and notes it on every Steam game.

## Running games (#95, 2026-10-21)

`SteamRunningGames` reads `HKCU\Software\Valve\Steam\Apps\<appid>\Running` (1 while the game runs) and, like SISR
already does, `RunningAppID` (the game Steam started last, while it runs; it counts even without the flag). There
are thousands of `Apps` keys (every app Steam ever touched), so only a poll after a pause of more than 10 s (Arcadia
just started, or the PC slept) reads them all; the polls in between read `RunningAppID` and the keys
of the games already running. A game that starts meanwhile becomes `RunningAppID`. "Läuft seit" is the last write
time of the app's key, read once per run (`RegQueryInfoKey`). Checked on the owner's PC (T95-12,
2026-10-24): Steam sets `Running` to 1 while the game runs and back to 0 when it ends, also when the game is killed in
Task Manager. **But the flag can stick:** Marvel's Guardians of the Galaxy (1088850), not installed, still had
`Running` = 1, its key last written on 2026-10-20 although it was last played in 2025; cause unknown. Not the cause:
`steam://rungameid` on an uninstalled game (opens the install dialog, leaves `Running` at 0; checked 2026-10-24). A
stale flag would show such a game as running for good once it is installed again (#182).
Play stats: Steam writes `LastPlayed` into the manifest at the start and the account's `localconfig.vdf` at some point after the end; the scanner takes the later of both,
and Arcadia rescans right after a game ended and 30 s later.
