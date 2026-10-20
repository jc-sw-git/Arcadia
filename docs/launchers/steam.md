# Steam — findings

Read before touching `src/Arcadia.Launchers/Steam`. Verified on the owner's PC.

## Shortcuts (2026-10-09)

- Steam shortcut app ids in modern Steam are *not* CRC32(exe+name) — read the stored `appid`.
  `rungameid` = `(appid << 32) | 0x02000000`. Steam writes `shortcuts.vdf` only on exit;
  never write it while Steam runs. `BinaryVdf` round-trips the real file byte for byte.

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
license list (`userdata/<id>/config/licensecache`) is encrypted — they come with a Steam login (#83). Libraries are re-read on every scan,
so added or missing drives need no handling of their own.
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
