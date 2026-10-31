# #354 Pan update at start fails because Arcadia restarts the old Pan while --install waits for it to stop

While Pan's `--install` runs, Arcadia holds no connection to Pan and starts no service; after a decline or failure it
stays away from the old Pan as in #326 ([326-announce-the-pan-update-and-let-the-player-decline-it.md](326-announce-the-pan-update-and-let-the-player-decline-it.md)).
Needed: Windows, a build of this PR, another Pan installed (as for T326-2), Pan on, the Steam Controller with its puck.

Automated: `PanUpdateAtStartTests` (T354-1, success, failure and decline), `ControllerSupportServiceTests`
(`While_the_update_runs_Arcadia_leaves_Pan_alone_…`, `Declined_the_update_…`), `PanClientTests` (a reason keeps the connection closed).

- [ ] **T354-1** Start Arcadia, [A] "Aktualisieren", confirm the Windows prompt: the library comes with the toast
      "Pan: aktualisiert" within a few seconds, not after 90 s. `arcadia.log` has no "Pan: Dienst ArcadiaPan wird
      gestartet" between the start of the update and "Pan aktualisiert", but "Pan wird aktualisiert – Arcadia verbindet
      sich erst danach wieder"; `pan.log` has no "--install failed". · E2E: yes
- [ ] **T354-2** After T354-1, Pan's emulation works (Steam Controller as Xbox pad in a game) and the next start shows
      no "Neue Pan-Version". · E2E: no (real Pan service and Windows prompt)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T354-1 | 2026-10-31 | Claude | branch `fix/pan-update-restarts-old-pan-354`, Linux container, view models with fakes (no real service) | ✅ | – |
| T354-1 | 2026-10-31 | Claude (QE) | branch `fix/pan-update-restarts-old-pan-354`, Linux container, view models with fakes; decline case added, red without the flag | ✅ | – |
