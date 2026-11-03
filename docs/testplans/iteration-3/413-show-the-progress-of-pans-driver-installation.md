# #413 Show the progress of Pan's driver installation

"Treiber installieren" and the warning rows' "Installieren" / "Aktualisieren" / "Neu installieren" in Einstellungen →
Controller. `pan.log`: `%ProgramData%\Arcadia\Pan\pan.log`; Pan's step report of a run lies beside it as
`install-<run>.report`. Precondition for T413-27 to T413-29: the drivers removed through the settings' uninstall, so
no Arcadia (re)install and no Windows restart is needed.

**Which steps a run shows (D3, D4, D7, D11)**
- [ ] **T413-1** Full install (S1): nothing installed, Steam runs. "Treiber installieren" → the confirmation shows the
      USB line → A. The dialog lists the ten steps in order, the first one is the prompt; the USB hint and "Zum Schluss
      startet Arcadia einmal neu." stand for the whole run; each step goes pending → running → ✓ in order; at "Arcadia
      neu starten" Arcadia restarts as today. After the restart no dialog, every row reports its part as installed.
      · E2E: yes (`ControllerSupportServiceTests`, `DriverInstallDialogTests`)
- [ ] **T413-2** As T413-1, Steam not running: no "Steam beenden" and no "Steam starten" step; Steam is not started.
      · E2E: yes (`ControllerSupportServiceTests`)
- [ ] **T413-3** Only Pan missing (S2): "Pan fehlt" → "Installieren": only the prompt and "Pan einrichten"; no USB
      hint, no restart line, no Steam steps; closes by itself on success. · E2E: yes (`ControllerSupportServiceTests`,
      `DriverInstallDialogTests`)
- [ ] **T413-4** Only HidHide missing, Steam runs: "HidHide installieren?" has no USB line; prompt, Steam beenden,
      HidHide download and install, Pan einrichten, Steam starten, Arcadia neu starten; no usbip-win2 steps.
      · E2E: yes (`ControllerSupportServiceTests`, `DriverInstallDialogTests`)
- [ ] **T413-5** Only usbip-win2 missing: USB hint in the dialog for the whole run; no HidHide, Steam or restart steps.
      · E2E: yes (`ControllerSupportServiceTests`)
- [ ] **T413-6** Only VIIPER missing (D11): no Windows prompt, no prompt step, no Pan started; only the VIIPER step.
      · E2E: yes (`ControllerSupportServiceTests`)
- [ ] **T413-7** "Aktualisieren" and "Neu installieren" open the same dialog with that run's steps. · E2E: yes
      (`ControllerSupportServiceTests`: "Neu installieren"; "Aktualisieren" runs the same `InstallDrivers` fix as
      "Installieren")
- [ ] **T413-8** Regression: the Pan update (#326), "Controller wiederherstellen" and the uninstall show no progress
      dialog. · E2E: yes (`ControllerSupportServiceTests`, `PanUpdateAtStartTests`)

**Prompt first (D7, S4)**
- [ ] **T413-9** Prompt declined: the prompt step red with "… in der Windows-Abfrage abgelehnt …", every other step
      pending, A "Schließen"; Steam never asked to close, nothing downloaded, VIIPER unchanged; the switch then offers
      "Erneut versuchen". · E2E: yes (`EmulationSetupTests`, `ControllerSupportServiceTests`)
- [ ] **T413-10** Until the elevation answers, no download, no Steam close and no VIIPER setup started (order of calls).
      · E2E: yes (`ControllerSupportServiceTests`)

**Bar and report**
- [ ] **T413-11** Downloads move the bar by their bytes, other steps by their weight; it never goes back. · E2E: yes
      (`ControllerSupportServiceTests`)
- [ ] **T413-12** A report of an earlier run never moves this run's dialog; earlier reports are removed. · E2E: yes
      (`InstallReportTests`)
- [ ] **T413-13** The report is display only: "all ✓" with exit 3 fails, "failed" with exit 0 succeeds. · E2E: yes
      (`ControllerSupportServiceTests`)
- [ ] **T413-14** No, empty or garbled report: one step "Treiber werden installiert", ending with Pan's exit; a report
      Pan can't write never stops the install. · E2E: yes (`ControllerSupportServiceTests`, `InstallReportTests`)

**Failure, timeout, abort (D6, D8, D13)**
- [ ] **T413-15** usbip-win2 fails (exit 3): its step red, later steps pending except "Steam starten" ✓, no restart.
      HidHide fails (exit 6): HidHide red, "Pan einrichten" and "Steam starten" ✓, no restart. A or B closes →
      "Erneut versuchen". · E2E: yes (`ControllerSupportServiceTests`, `DriverInstallDialogTests`)
- [ ] **T413-16** Between prompt and hand-over: download fails (network, hash), Steam won't close, a Steam game or a
      firmware update started during the VIIPER download, Pan ends before the hand-over (4, 8, 9), exit 9 Arcadia didn't
      cause (own message), cancellation: that step red, `abort` before Steam starts again, nothing installed.
      · E2E: yes (`ControllerSupportServiceTests`)
- [ ] **T413-17** A download that hangs runs into its own timeout ("Der usbip-win2-Download dauerte länger als 5
      Minuten …"), the bar keeps its value. Elevated Pan before the hand-over: `abort`, Arcadia gone, broken pipe, no
      `go` within `PanTimeouts.HandOver` → exit 9, nothing changed. Arcadia's timeouts up to the hand-over plus 5 min fit
      into `PanTimeouts.HandOver`. · E2E: yes (`ControllerSupportServiceTests`, `InstallHandOverTests`)
- [ ] **T413-18** A new Arcadia shows no dialog for an earlier run. · E2E: yes (`ControllerSupportServiceTests`)

**Controller first and focus (D2, D9, D10)**
- [ ] **T413-19** While running, B, A and the D-pad change nothing; the footer shows no "B Zurück". · E2E: yes
      (`DriverInstallDialogTests`, `DriverInstallDialogViewTests`)
- [ ] **T413-20** After a failure A or B closes and the focus is back on the starting row; after a success without
      restart the dialog closes by itself, the focus on the starting row or else the first row of "Controller".
      · E2E: yes (`DriverInstallDialogTests`, `EmulationSetupTests`)
- [ ] **T413-21** Mouse: a click beside the running dialog does nothing; after a failure "Schließen" closes it.
      · E2E: yes (`DriverInstallDialogViewTests`)

**Confirmation (D5, D12)**
- [ ] **T413-22** "Treiber installieren?" shows the USB line; "HidHide installieren?" without usbip-win2 doesn't.
      · E2E: yes (`DriverInstallDialogTests`)

**Layout**
- [ ] **T413-23** Ten steps, bar, USB hint and footer fit at 1920×1080 and at 200 %; running row highlighted, failed
      red, pending muted. · E2E: yes (`DriverInstallDialogViewTests`: fit, bounds; look: QE's run)
- [ ] **T413-24** Minutes of a running step: the UI takes input and Pan's report moves the dialog. · E2E: yes
      (`DriverInstallDialogTests`)

**Elevation and security**
- [ ] **T413-25** One prompt per run; the pipe's DACL grants read/write to Arcadia's user only; another process is
      refused, Arcadia still gets through; the run id has 128 random bits. Elevation with another account: untested.
      · E2E: yes (`InstallHandOverPipeTests`, Windows CI; `InstallReportTests`)
- [ ] **T413-26** Refused hand-overs, one each: unannounced installer, HidHide missing or with its own hash, path
      outside the folder, `..`, relative, missing or bad hash, unknown or repeated line, no `go`, no line break, over
      4 KB, a link or junction; a half or malformed run on the command line; pipe already there → exit 9; a client
      started after Pan → refused. · E2E: yes (`InstallHandOverTests`, `InstallReportTests`, `InstallHandOverPipeTests`)

**Owner's PC (real drivers, real UAC)**
- [ ] **T413-27** Full install with USB mouse and keyboard: the Windows prompt first, before Steam closes; each step ✓
      in order; during usbip-win2 mouse and keyboard may freeze, Arcadia's window keeps animating (A1); Arcadia
      restarts, the Steam Controller works in Big Picture. · E2E: no (UAC, real drivers, real USB freeze)
- [ ] **T413-28** Decline the real prompt: Steam keeps running, `arcadia.log` shows no download,
      `%APPDATA%\Arcadia\viiper` unchanged, the dialog as in T413-9. · E2E: no (real UAC)
- [ ] **T413-29** Close Arcadia (CloseMainWindow) while usbip-win2 installs: Pan finishes on its own; at the next start
      no dialog, the rows show what is installed. · E2E: no (real elevated Pan, real installer)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T413-1 to T413-24 (unit, VM, window), T413-12, -14, -17, -26 (Pan unit) | 2026-11-03 | Claude (thread) | Debug, Linux cloud container | pass | footer still showed "B Zurück" while running: fixed |
| T413-25, T413-26 (`InstallHandOverPipeTests`, `ServiceTests` hand-over: real Pan installed through the pipe) | 2026-11-03 | Claude (thread) | Release, Windows CI runner (cee2435) | pass | – |
