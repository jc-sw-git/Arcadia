# #278 Follow Steam's install progress, pauses and account switches live

`SteamInstallMonitor` takes what a Steam download does from Steam's content log (`SteamContentLog`), which Steam writes
the moment anything changes: start, preallocating, pause (`Disabled (Suspended)`), queued behind another download
(`Priority (Suspended)`), committing, finished, and a new session after a restart or an account switch. A percentage is
shown only when it is exact (download bytes only, as Steam's download page counts them): while a download is paused or
queued; while it runs the bar is indeterminate without a number. The manifests still decide which games take part and
how an installation ended. Polling stays at 2 s. Lines and findings: `docs/launchers/steam/library-and-installs.md`.

- [x] **T278-1** Install a game in Steam: Arcadia shows it within 5 s as "Wird installiert …" with an indeterminate bar
  (also while Steam preallocates), never a number that stands still; at the end "Wird abgeschlossen …", then installed ·
  E2E: yes, deferred (without a signed-in Steam account a new install has no tile until it is done, and the sign-in is
  being replaced by #280; the same display for an update is automated as T278-4)
- [x] **T278-2** Pause the download in Steam: "Pausiert · n %" within 5 s, n within ±1 % of Steam's download page;
  resume it: running again (indeterminate) within 5 s · E2E: yes (faked Steam: manifests and content log lines)
- [x] **T278-3** Two downloads, switch between them in Steam: the one taken over shows "In der Warteschlange", the
  other as running, both within 5 s · E2E: yes (faked Steam)
- [ ] **T278-4** Update an installed game (`Aktualisieren` or Steam's own update): same as T278-1 with "Wird aktualisiert
  …" · E2E: yes (faked Steam)
- [x] **T278-5** Switch the Steam account while a download runs: the download (and any queued one) shows "Pausiert"
  within 5 s; switched back, Steam resumes it and Arcadia shows it running again · E2E: yes (faked Steam)
- [x] **T278-6** Pause a download in Steam, then restart Steam: after the restart Arcadia shows the download as Steam
  does (Steam queues it again and resumes it), within 5 s, never as running while Steam waits · E2E: no (what Steam does
  after its own restart; the lines are in a unit test)
- [x] **T278-7** Pause an update of an installed game, then remove it from Steam's download queue: the tile no longer
  shows an installation within 5 s · E2E: no (Steam's lines for a removal were not recorded yet; the assumed ones are in a
  unit test)
- [ ] **T278-8** Start an installed game with an update pending in Steam, with another download running so the update
  waits in the queue; move the update up by hand in Steam: if Steam doesn't start the game after the update, Arcadia
  starts it once more about 10 s later ("Wird gestartet …" stays); if it still doesn't run 30 s after that, the cover
  shows "Spielen" again. A start without an update shows "Wird gestartet …" for 30 s at most · E2E: yes (faked launcher,
  `GameStartTests`)

Unit tests: `SteamInstallMonitorTests` with the owner's content log lines of 2026-10-27 for every state above (start
while preallocating, pause with Steam's number, queued after a takeover, account switch and back, committing and
finished, a scheduled update nobody started, an uninstall that cancels the download) and a rotated log. Acceptance
tests (`tests/Arcadia.App.Tests/SteamInstallTests.cs`) replay the same lines with faked manifests through the App; the
manual run checks that the real Steam writes them.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| Unit tests above, all Core (958) and App (337) tests; the owner's whole `content_log.txt` of 2026-10-27 replayed through `SteamContentLog` | 2026-10-27 | Claude | PR branch, Linux cloud container | ✅ every pause, takeover, restart and finish of that day read as in Steam | the ended update kept its bytes for the next one: fixed and tested |
| Decision 7: does `steamapps/downloading/<id>` follow Steam's %? Check script (`Run-Check-278.ps1`), 4 readings running, 1 paused | 2026-10-27 | owner, Claude | owner's PC, real Steam, new install of a 6.5 GB game on F: | ❌ the folder is preallocated to the full staging size at the start and barely grows (also on disk); the paused manifest matched Steam (2 %) | no live number: a running download stays indeterminate (decision 2) |
| T81-3 acceptance test changed: a running download shows no number | 2026-10-27 | Claude | PR branch, Linux cloud container | ✅ all App and Core tests | – |
| QE review: acceptance tests T278-2 to T278-5, each red once on a broken monitor (see PR, section Review) | 2026-10-27 | Claude (QE) | PR branch, Linux cloud container, faked Steam | ✅ | a paused update removed from Steam's queue stayed "Pausiert": fixed in the PR, T278-7 added; T278-6 added for a Steam restart |
| Owner's test: T278-1 to T278-7 | 2026-10-28 | owner | PR build, owner's PC, real Steam | ✅ all but T278-4 (the update was too small to watch); after an update that waited in Steam's queue and was moved up by hand, Steam didn't start the game | T278-4 goes to the iteration's acceptance review (owner); second start after an update and "Wird gestartet …" 30 s instead of 2 min added (T278-8) |
| T278-8: `RunningGamesTrackerTests` (start again once after 10 s, give up 30 s later, not for a game that runs or a start without update) and the acceptance test in `GameStartTests`, each red once without the second start; all Core and App tests | 2026-10-28 | Claude | PR branch, Linux cloud container | ✅ | owner's run of T278-8 and T278-4 in the acceptance review |
