# #308 Pan: hide warning loops with only the puck connected, then emulation stops

With only the puck plugged in, every hide failed: Windows refused the puck restart (System log, event 225) because
Arcadia's SDL held the puck's four vendor collections open, waiting for a controller. Each failure counted, the 4th in
5 minutes stopped the emulation. A hide that worked (controller switched on) broke 5 s later: Pan took Arcadia's copy
for Steam for a new puck, hid it from Steam and restarted the real puck. Now Arcadia lets go of the puck in SDL while
Pan restarts it, Pan neither hides nor restarts a device on a software USB host (the copy), and a retry keeps its
warning. Findings: [pan-puck-restart.md](../../controller-support/pan-puck-restart.md).

- [ ] **T308-1** Only the puck plugged in (Steam Controller off), Steam closed, Pan on, Arcadia started: within a few
      seconds `pan.log` says "Steam Controller hidden for generation N: 13 instance(s)" and nothing like "not
      restarted … needs a replug"; the System log has no event 225 (Kernel-PnP) for `USB\VID_28DE&PID_1304`.
      · E2E: no (real puck, Windows' PnP; Core `PanEmulationTests` covers that Arcadia lets go around every hide and show)
- [ ] **T308-2** As T308-1, then 10 minutes with the settings open on Controller: no "Steam Controller lässt sich
      nicht verstecken", no "Emulation angehalten", no Pan toast; `pan.log` shows one hide, no new generation.
      · E2E: yes (`PanEmulationTests` in App)
- [ ] **T308-3** A hide that keeps failing: the row "Steam Controller lässt sich nicht verstecken" stays through the
      retries (no flicker every 10 s) until it works or the emulation stops. · E2E: yes (`PanEmulationTests` in App
      and Core; no way to make the real hide fail on purpose after the fix)
- [ ] **T308-4** After T308-1, switch the Steam Controller on: "Controller #N verbunden: Steam Controller" in
      `arcadia.log`, Arcadia's Xbox pad works in a game on "Arcadia"; switch it off: the pad goes, the emulation keeps
      running, no warning, no failure counted. · E2E: no (real controller and puck)
- [ ] **T308-5** After T308-1 with Steam started: Steam sees the Steam Controller (the copy) and it works in Steam's
      menu; HidHide's configuration client lists only the puck's `7&…` instances, none of the copy's (`4&…`);
      `pan.log` has no "device arrived while hiding, not hidden". · E2E: no (real HidHide and Steam; Pan
      `HidHideWireTests` covers telling the copy from the puck by the device tree)
- [ ] **T308-6** The owner's PC still lists the copy's entries from the run of 2026-10-29 (journal, HidHide): switch
      Pan off once after installing this build; HidHide's configuration client then lists no `VID_28DE&PID_1304`
      entry, Arcadia isn't on its application list, and `journal.json` is empty. · E2E: no (one-off clean-up of the
      owner's state; `HidingTests` cover the show)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| Logs of the owner's run (puck only, Pan on, `--demo`): `arcadia.log`, `pan.log`, journal, Kernel-PnP and System event logs | 2026-10-29 | Claude (read by Remote Control, nothing changed) | `feat/replace-sisr` 0d81734, owner's PC, Debug build | ❌ every hide vetoed by `dotnet.exe` (event 225, the four `MI_0x&Col03` collections); the one working hide (controller on) hid the copy 5 s later | Root cause, this PR |
| T308-1 to T308-3 (Core `PanEmulationTests`, App `PanEmulationTests`), SDL's let-go (`SdlGamepadSourceTests`), copy vs. puck (Pan `HidHideWireTests`), all App, Core and Pan tests | 2026-10-29 | Claude | PR branch, Linux cloud container, Avalonia headless | ✅ (each new test ❌ once with the fix taken out: no let-go, no retry status kept, no software host) | – |
