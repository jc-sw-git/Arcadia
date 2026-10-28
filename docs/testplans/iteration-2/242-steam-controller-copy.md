# #242 Steam gets a copy of the Steam Controller (part A)

Part A (PR into `feat/replace-sisr`): Pan's emulation lifecycle, the copy's rules and allow list, the group "PAN" in
the controller settings, the greyed mode, the header and the firmware dialog. The copy itself needs the VIIPER fork
(part B) and its Windows side (part C); until then the App passes `NoSteamControllerCopy`, so Pan hides nothing and
the pad works as in #213. Cases marked "after part C" are run then. Needed: the Steam Controller with its puck, Steam,
a game on "Steam-Eingabe" and one on "Arcadia", HidHide's configuration client, "Game controllers" = `joy.cpl`. How it
works: `docs/controller-support/steam-controller-copy.md`.

Automated: `tests/Arcadia.Core.Tests` (`PanEmulationTests`, `SteamControllerCopyFilterTests`,
`SteamControllerRoutingTests`, `PanClientTests`) and the acceptance tests `tests/Arcadia.App.Tests/PanEmulationTests.cs`,
`SteamControllerModeTests.cs`.

- [ ] **T242-1** Steam sends the puck only what the allow list names: known features always, haptics only while the
      copy passes input, firmware, reset, bootloader, serial, pairing and unknown types never; reads always pass.
      `arcadia.log` names each dropped type once, without its payload. · E2E: yes (`SteamControllerCopyFilterTests`;
      the real traffic after part C)
- [ ] **T242-2** With no game running and Arcadia in front, Steam's controller test shows a controller at rest (only
      the Steam button and "…" react); with Steam in front it shows every input; with a game on "Arcadia" running, the
      game gets Arcadia's Xbox pad and Steam a controller at rest. · E2E: yes (routing rules; the real Steam after part C)
- [ ] **T242-3** In a game on "Steam-Eingabe", every button, stick, trackpad and the gyro reach the game through
      Steam; ⧉ + ≡ still opens Arcadia and never reaches Steam. · E2E: yes (filter; the real game after part C)
- [ ] **T242-4** At Arcadia's start the puck restarts once (hidden), Steam sees exactly one Steam Controller (the
      copy), `joy.cpl` lists no second one; switching a game's mode or starting a game never makes it drop out.
      · E2E: yes (order hide → copy → pad; the real devices after part C)
- [ ] **T242-5** Einstellungen → Controller → "Controller-Wächter (Pan)" off: the copy and the pad end, the puck is
      shown again (Steam reads it directly), the mode leaves the detail page; on again, it is hidden and copied again.
      · E2E: yes (lifecycle; the real puck after part C)
- [ ] **T242-6** A hide that fails (e.g. the puck unplugged during the restart) leaves the controller as it is and is
      tried again after 10 s; a copy that doesn't come up shows the puck again; more than 3 failures in 5 minutes stop
      the emulation ("Emulation angehalten"). · E2E: yes
- [ ] **T242-7** HidHide's configuration client opened (or cloaking switched off) while running: pad and copy end at
      once, the notice "Pan: Controller-Wächter pausiert …" shows, and after closing it "Pan: Controller-Wächter läuft
      wieder". Steam's battery and connection state stay current while the copy rests. · E2E: yes (the real HidHide
      client after part C)
- [ ] **T242-8** A firmware update started in Steam for the Steam Controller: nothing reaches the puck, Arcadia comes
      to the front with "Firmware-Update gestoppt" and the three steps; B / "Verstanden" closes it, "Arcadia jetzt
      beenden" closes Arcadia; over open settings or another dialog it shows after that one. · E2E: yes (the dialog;
      Steam's real firmware path is unverified, after part C)
- [ ] **T242-9** Arcadia closed normally: pad and copy go, then the puck is shown and Steam reads it directly again,
      `pan.log` logs the planned exit; a teardown by Pan (Arcadia suspended) drops both and a new emulation starts by
      itself. · E2E: yes
- [ ] **T242-10** Pan switched on but not running (e.g. VIIPER missing): the mode on the detail page is greyed with the
      triangle and A on it opens the controller settings at the row that says why. · E2E: yes
- [ ] **T242-11** The switch says Pan runs; switched off the mode is gone from the detail page and the header, and
      "Controller wiederherstellen" stays. · E2E: yes
- [ ] **T242-12** While a game starts or runs, the switch is locked with the game's name in its subtitle and works again
      after the game ends. · E2E: yes
- [ ] **T242-13** A hide that fails shows its row in the group "PAN" with "Erneut versuchen", which tries again.
      · E2E: yes
- [ ] **T242-14** A copy that keeps failing shows the red row "Emulation angehalten"; "Erneut versuchen" starts it again.
      · E2E: yes
- [ ] **T242-15** While Pan still cleans up after a teardown, the header spins and a blue row says it goes on by itself.
      · E2E: yes
- [ ] **T242-16** On the detail page the broken mode can be focused with the gamepad, A opens the row that says why,
      B goes back to the detail page. · E2E: yes
- [ ] **T242-17** After part C: Arcadia's own controller list (SDL) ignores the copy (no second Steam Controller, no
      double input in Arcadia). · E2E: no (the real copy)
- [ ] **T242-18** After part C: input latency through the copy p99 < 1 ms (raw HID read to VIIPER write), measured
      with the latency trace while a game on "Steam-Eingabe" runs; no measurable cost to the game. · E2E: no
- [ ] **T242-19** On Linux the mode doesn't show (state `Unsupported`), the Pan switch is greyed and unavailable, the
      pad behaves as before.
      · E2E: no (Linux build by hand)
- [ ] **T242-20** With Pan paused (e.g. HidHide's configuration client open), Steam in front: start a firmware update
      on the real Steam Controller in Steam and close the configuration client during it. Pan doesn't hide (the puck
      doesn't restart), the row "Pan wartet auf Steam" shows; 3 minutes after Steam left the front, Pan hides by
      itself. · E2E: yes (the guard and the row; the real update after part C)

## Test runs

| Cases | Date | Who | Environment | Result | Notes |
|---|---|---|---|---|---|
| T242-1 … T242-9 (Core: `PanEmulationTests`, `SteamControllerCopyFilterTests`, `SteamControllerRoutingTests`, `PanClientTests`) | 2026-10-28 | Claude (thread) | Debug, Linux cloud container, fakes for Pan, VIIPER and the puck; Pan 172, Core 1783 | pass | the real copy after parts B and C |
| T242-8, T242-10 … T242-16 (acceptance tests) | 2026-10-28 | Claude (thread) | Debug, Linux cloud container, Avalonia headless; each test seen red once by mutation (dialog shown over settings, switch not toggling, game lock removed, paused notice removed, stopped row not built, spinner while setting up removed, broken mode not focusable, A not leading to the row) | pass | not rendered on Windows; owner's manual run open |
| T242-9, T242-20 after the QE review (`PanEmulationTests` Core: firmware guard, a copy that fails to go leaves the puck to Pan's teardown, one restart per tick; App: the row "Pan wartet auf Steam") | 2026-10-28 | Claude (thread) | Debug, Linux cloud container; each new test seen red once by mutation (guard off, Steam's turn not noted, unhide after a failed removal, restart unbounded, row not built) | pass | real update after part C |
