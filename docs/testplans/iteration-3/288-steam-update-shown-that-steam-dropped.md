# #288 Steam: update shown that Steam dropped seconds later

Cases from the specification in the issue, test levels checked by the quality engineer. Steam's manifests:
`<library>\steamapps\appmanifest_<appid>.acf` (`StateFlags`, `buildid`, `TargetBuildID`, `BytesToDownload`,
`BytesDownloaded`); what Steam does: `<Steam>\logs\content_log.txt`.

- [x] **T288-1** Arcadia open on the Steam tab, Steam closed. Start Steam and watch Half-Life 2 (or any game Steam
      flags for a few seconds at its start, `state changed : Update Required` then `has no changes` in the content log):
      no update icon at any time, also not after switching to another window and back while Steam starts.
      · E2E: no (real Steam start; the brief flag with nothing new as an acceptance test: `SteamUpdateTests`)
- [x] **T288-2** A game with a real pending update (`TargetBuildID` ≠ `buildid`, or bytes left to download): start
      Arcadia, the cover shows the update icon and the detail page the note below "Spielen". · E2E: yes
- [x] **T288-3** Arcadia open, Steam finds a new update of a game: the icon appears within a few seconds, without a
      rescan and without a toast. · E2E: yes (a fake monitor reports the update)
- [x] **T288-4** Steam finishes an update in the background, or drops the flag: the icon goes within a few seconds,
      without a rescan. · E2E: yes (a fake monitor drops the update)
- [x] **T288-5** Detail page of the game open, focus on "Spielen": the update note appears and goes with the icon; page
      and focus stay. · E2E: yes
- [x] **T288-6** A manifest Steam is writing (broken or half-written): the shown update state doesn't change, Arcadia
      keeps running. · E2E: no (unit: `SteamInstallMonitorTests`)
- [x] **T288-7** A finished update whose byte counters stay in the manifest (`BytesToDownload` = `BytesDownloaded` > 0,
      same build) shows no update, also if Steam flags it for a moment. · E2E: yes (variants: `SteamScannerTests`)
- [x] **T288-8** A manifest without `TargetBuildID`: the flag alone decides, as before. · E2E: no (unit: `SteamScannerTests`)
- [x] **T288-9** 1,000 Steam games: a monitor poll stays cheap (mtime cache). · E2E: no (measured: 45 ms per poll without
      the cache, 8 ms with it, Release, Linux cloud container)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T288-1 (log evidence) | 2026-11-03 | Claude (thread) | owner's `content_log.txt` and manifests, read remotely | rule holds: Half-Life 2 flagged with the same build and 0 bytes; no flicker since 2026-10-28 | rule adjusted: `BytesDownloaded` < `BytesToDownload` instead of `BytesToDownload` > 0 |
| T288-3 to T288-5 (integration), T288-6 to T288-8 (unit) | 2026-11-03 | Claude (thread) | Debug, Linux cloud container | pass | – |
| T288-9 | 2026-11-03 | Claude (thread) | Release, Linux cloud container, 1,000 generated manifests | 45 ms → 8 ms per poll | mtime cache kept |
| T288-1 (flag part), T288-2 to T288-5, T288-7 (acceptance, `SteamUpdateTests`) | 2026-11-03 | Claude (QE) | Debug, Linux cloud container, recreated Steam folder, real install monitor | pass; each red once (rule reverted to the flag alone; monitor without update state; detail page not told of the live update; focus reset on rebuild) | – |
