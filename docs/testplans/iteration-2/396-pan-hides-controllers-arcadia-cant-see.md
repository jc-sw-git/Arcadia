# #396 Pan hides controllers Arcadia itself can no longer see

HidHide installed while Arcadia ran leaves that Arcadia outside HidHide's allow list in effect: after a hide it sees
neither the Steam Controller nor any other hidden controller, so no copy and no pad came up while everything stayed
hidden. Now the emulation checks that Arcadia sees again what it saw before the hide, ends and shows them otherwise,
and a HidHide install from Arcadia is followed by a restart of Arcadia before Pan hides anything.

- [ ] **T396-1** Force the blind case with the Steam Controller: Pan off, Steam Controller on the puck. Install HidHide
      by hand with its own setup while Arcadia runs, then switch Pan on. Within about 6 s after the hide the Steam
      Controller is shown again (Steam sees it), a toast says Arcadia doesn't see it, the switch's lamp is yellow, and
      the row "Arcadia sieht deine Controller nicht mehr" (yellow triangle) lists "Steam Controller" in its info panel,
      with the hint to restart Windows if it comes back. Switching Pan off and on hides nothing again. [A] on
      "Arcadia neu starten" restarts Arcadia; afterwards the copy works in Steam and the Xbox pad in a game on
      "Arcadia". Regression: with HidHide installed before Arcadia started, the row never shows, also after switching
      the controller off and on. · E2E: yes (App `PanEmulationTests` T396-1; Core `PanEmulationTests` T396-1; real
      HidHide and the restart are manual)
- [ ] **T396-2** As T396-1 with a second controller (e.g. the 8BitDo Ultimate 2) connected: it is shown again too and
      listed by its name in the same row. Regression: in the normal case, unplugging it later shows no row. · E2E: yes
      (Core `TakenOverControllersTests` T396-2)
- [ ] **T396-3** The owner's case on 2026-11-01: uninstall HidHide (Windows "Apps"), restart Windows, start Arcadia,
      Einstellungen → Controller → "HidHide fehlt" → "Installieren". "HidHide installieren?" says Arcadia restarts
      afterwards; [B] installs nothing. Then [A], confirm the Windows prompt: Arcadia ends and starts again by itself
      within a few seconds; `arcadia.log` has "Arcadia startet neu (nach der HidHide-Installation)" and no
      "Pan-Emulation … läuft" line between the install and the restart. Afterwards copy and pads work. "Installieren"
      for a part other than HidHide (e.g. Pan missing) asks nothing and restarts nothing. · E2E: yes (App
      `EmulationSetupTests` T396-3; real HidHide, UAC and the restart are manual)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| Diagnosis | 2026-11-01 | Claude | owner's PC, 0.2.0-rc.1, read-only: `arcadia.log`, `pan.log`, journal, event log | HidHide installed at 18:12:29 while Arcadia (PID 25636) ran; that process saw nothing Pan hid until it ended at 22:35 | This issue |
| T396-1 to T396-3 (automated) | 2026-11-01 | Claude | branch build, owner's Windows 11 PC, fakes for Pan, HidHide, UAC and controllers | ✅ green; each new acceptance test red once with its part of the fix removed | Real HidHide, UAC, restart and controllers need the owner's test |
