# #278 Follow Steam's install progress, pauses and account switches live

`SteamInstallMonitor` takes what a Steam download does from Steam's content log (`SteamContentLog`), which Steam writes
the moment anything changes: start, preallocating, pause (`Disabled (Suspended)`), queued behind another download
(`Priority (Suspended)`), committing, finished, and a new session after a restart or an account switch. A percentage is
shown only when it is exact (download bytes only, as Steam's download page counts them): while a download is paused or
queued; while it runs the bar is indeterminate without a number. The manifests still decide which games take part and
how an installation ended. Polling stays at 2 s. Lines and findings: `docs/launchers/steam.md`.

- [ ] **T278-1** Install a game in Steam: Arcadia shows it within 5 s as "Wird installiert …" with an indeterminate bar
  (also while Steam preallocates), never a number that stands still; at the end "Wird abgeschlossen …", then installed ·
  E2E: no (real Steam)
- [ ] **T278-2** Pause the download in Steam: "Pausiert · n %" within 5 s, n within ±1 % of Steam's download page;
  resume it: running again (indeterminate) within 5 s · E2E: no (real Steam)
- [ ] **T278-3** Two downloads, switch between them in Steam: the one taken over shows "In der Warteschlange", the
  other as running, both within 5 s · E2E: no (real Steam)
- [ ] **T278-4** Update an installed game (`Aktualisieren` or Steam's own update): same as T278-1 with "Wird aktualisiert
  …" · E2E: no (real Steam)
- [ ] **T278-5** Switch the Steam account while a download runs: the download (and any queued one) shows "Pausiert"
  within 5 s; switched back, Steam resumes it and Arcadia shows it running again · E2E: no (real Steam)

Unit tests: `SteamInstallMonitorTests` with the owner's content log lines of 2026-10-27 for every state above (start
while preallocating, pause with Steam's number, queued after a takeover, account switch and back, committing and
finished, a scheduled update nobody started, an uninstall that cancels the download) and a rotated log. No acceptance
test: every case needs the real Steam.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| Unit tests above, all Core tests | 2026-10-27 | Claude | PR branch, Linux cloud container | ✅ | – |
