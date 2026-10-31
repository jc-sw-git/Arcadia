# #345 Hide and copy the Steam Controller by cable

Pan hides the Steam Controller by cable (`28DE:1302`) beside the puck and Arcadia copies it for Steam; the copy follows
the controller between cable and puck. Findings: [emulated-steam-controller.md](../../controller-support/emulated-steam-controller.md),
[steam-controller-copy.md](../../controller-support/steam-controller-copy.md). Setup for all cases: Pan and HidHide
installed, Arcadia with Steam Controller support on, Steam running, the puck plugged in.

- [ ] **T345-1** Switch the controller off, plug it in by cable, start Arcadia: Steam lists one Steam Controller (the
      copy) with the usual name and icon; trackpads, gyro, buttons and haptics work in Steam's controller test;
      Arcadia's own navigation works by controller. `arcadia.log` has "Kopie für Steam angesteckt", `pan.log` "3
      device(s) hidden" or more (the puck's too). · E2E: yes (Pan `CableHidingTests`, Core
      `SteamControllerByCableTests`: hide and the cable's copy)
- [ ] **T345-2** By cable, a game on "Steam-Eingabe" gets Steam's layout and a game on "Arcadia" the Xbox pad, each
      without the other's input. · E2E: no (real Steam and games; the input rules are the puck's, `SteamControllerCopyRules`)
- [ ] **T345-3** While taken over by cable, unplug the cable: the controller goes to the puck within a few seconds and
      keeps working in Steam and Arcadia; Steam lists one controller, no "Emulation angehalten". `arcadia.log` has
      "Steam Controller jetzt am Puck, die Kopie folgt" and no "Kopie für Steam verloren". · E2E: yes (Core
      `SteamControllerByCableTests`)
- [ ] **T345-4** While taken over on the puck, switch the controller off and plug it in by cable: the copy follows to
      the cable ("Steam Controller jetzt am Kabel, die Kopie folgt"), Steam lists one controller, `pan.log` has no
      "not hidden" and no "needs a replug". · E2E: yes (Pan `CableHidingTests`, Core `SteamControllerByCableTests`)
- [ ] **T345-5** While the controller is on and connected to the puck, plug in the cable: it stays on the puck
      (charging), nothing in Arcadia or Steam changes, `arcadia.log` has no "die Kopie folgt". · E2E: no (the
      controller's own behaviour)
- [ ] **T345-6** By cable, when Steam offers a firmware update: Arcadia shows "Steam will die Firmware aktualisieren";
      after "Controller freigeben" Steam flashes the controller (bootloader `1005`), it comes back as `1302` and stays
      Steam's (`pan.log`: no hide while the bootloader is there); `controller.txt` shows the new `FWTimestamp`.
      · E2E: no (a real firmware update; Pan `CableHidingTests` covers no hide while `1005` is connected)
- [ ] **T345-7** By cable, end Arcadia normally, then once hard (Task Manager): Steam gets the real controller back
      within a few seconds, HidHide's list has no `1302` entry of Pan's left. · E2E: yes (Pan `CableHidingTests`:
      teardown and a restarted Pan after a power loss)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| Unit and acceptance tests | 2026-10-31 | Claude | PR branch, Linux cloud container, fakes only | ✅ | – |
| T345-1 to T345-7 | – | owner | owner's PC | open | – |
