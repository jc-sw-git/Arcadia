# #422 Pan finishes an interrupted uninstall and removes itself without Arcadia

Real uninstalls of Arcadia from Windows' "Apps" on a PC with Pan and the drivers installed. `pan.log`:
`%ProgramData%\Arcadia\Pan\pan.log` (gone once Pan is removed: read it before the next restart, or keep a copy).
"Pan is gone" means: `sc query ArcadiaPan` says 1060, `%ProgramFiles%\Arcadia\Pan` is missing.

- [ ] **T422-1** Steam Controller puck plugged in, HidHide's Configuration Client open (it blocks Pan's undo). Uninstall
      Arcadia, "Ja" to the drivers, confirm the UAC prompt. Pan stays (`sc query ArcadiaPan`: stopped), `pan.log` says
      "tries again at every Windows start". Close the Configuration Client, restart Windows: Pan gives the puck back
      (Steam sees the Steam Controller), then removes HidHide and usbip-win2 and itself; after one more restart nothing
      of Arcadia is under "Apps" or in `%ProgramFiles%\Arcadia`. · E2E: no (real uninstall, UAC, restarts)
- [ ] **T422-2** A driver uninstaller that fails: retried at the next Windows start, at most 4 attempts in all (the
      uninstall and three starts), counted before it runs; then Pan removes itself, the driver stays under "Apps" and
      the data folder keeps `pan.log`. · E2E: yes (`UninstallOrderTests`, `PendingUninstallJourneyTests`; a failing uninstaller can't be made by hand)
- [ ] **T422-3** As T422-1, but before restarting Windows install Arcadia again and start it. After the restart Pan is
      still installed, works (Big Picture with the Steam Controller), the drivers are still there; `pan.log` says
      "Pending uninstall cancelled" or "Arcadia is installed again". · E2E: no (real reinstall)
- [ ] **T422-4** Uninstall Arcadia and decline the UAC prompt. Restart Windows: within about a minute Pan is gone,
      usbip-win2 and HidHide are still under "Apps". · E2E: yes (Windows CI: `ServiceTests`, the service restarted
      without its Arcadia; `PendingUninstallJourneyTests`)
- [ ] **T422-5** Arcadia installed for another Windows user only, or a profile Pan can't read: Pan stays. · E2E: yes
      (`ArcadiaInstallationsTests`, `SelfRemovalTests`)
- [ ] **T422-6** Pan crashes or the PC loses power while it finishes the uninstall: the marker stays and the next
      start goes on; an unreadable marker never removes the drivers. · E2E: yes (`ArcadiaInstallationsTests`,
      `PendingUninstallJourneyTests`)
- [ ] **T422-7** A Velopack update swapping Arcadia's folder while Pan starts: Pan looks twice, 10 s apart, and stays
      if Arcadia is back. · E2E: yes (`SelfRemovalTests`, `PendingUninstallJourneyTests`)
- [ ] **T422-8** After T422-4, before restarting Windows, install Arcadia and the drivers again ("Treiber
      installieren"), then restart: Pan is still installed and starts (the old exe was moved aside, not scheduled in
      place). · E2E: no (needs a Windows restart)
- [ ] **T422-9** Uninstall with "Nein" to both questions: `%APPDATA%\Arcadia\viiper` is gone, the rest of
      `%APPDATA%\Arcadia` stays; with SISR installed, its VIIPER (`%LOCALAPPDATA%\VIIPER`) and a running SISR are
      untouched. The drivers question no longer lists "VIIPER" when only Arcadia's own is there. · E2E: no (real
      uninstall; Velopack's hook doesn't run in tests)
- [ ] **T422-10** A build run from its folder (no installer) whose bundled Pan ran `--install`: Pan finds it and stays.
      · E2E: yes (`ArcadiaInstallationsTests`, Windows CI `ServiceTests` installs that way)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T422-2, T422-3 (cancel), T422-5, T422-6, T422-7, T422-10 (unit) | 2026-11-03 | Claude (thread) | Debug, Linux cloud container | pass | – |
| T422-1, -2, -3, -4, -6, -7 (acceptance: uninstall and Windows starts chained, real marker file; each red once against a mutated `UninstallOrder`/`SelfRemoval`/`PendingUninstallFile`) | 2026-11-03 | Claude (QE review) | Debug, Linux cloud container | pass | – |
