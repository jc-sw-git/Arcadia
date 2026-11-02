# #407 Driver install waits for a pending restart or a firmware update

Uninstalling Arcadia with its drivers leaves HidHide (and usbip-win2) in place until the PC restarts, and an install
before then meets the half removed driver; an install also restarts the puck and controllers, which could cut a
firmware update off. "Treiber installieren" is now greyed out in both cases, with a row saying why; it asks "Treiber
installieren?" before it runs; the uninstall ends with a message to restart the PC. Arcadia also recognises Sony's
PlayStation Accessories app. Needed for the manual cases: Windows, this build's installer, HidHide installed through
Arcadia, a Steam Controller with its puck, for T407-5 the PlayStation Accessories app and a DualSense.

Automated: `ControllerSupportRemovalTests` (the notice and how long it stays open), `EmulationSetupTests` (both
blocking rows, the greyed-out button, a row's install during a firmware update, the question with and without a puck),
`ControllerHidingTests` (PlayStation Accessories' process name).

- [ ] **T407-1** Uninstall Arcadia, answer "Treiber für die Controller-Emulation auch entfernen?" with Ja and accept
      the Windows prompt: a last message says Windows removes the drivers only at the next restart and to restart
      before installing Arcadia again; it closes by itself. · E2E: no (real uninstall and UAC)
- [ ] **T407-2** Without restarting, install Arcadia again, Einstellungen → Controller: "Treiber installieren" is
      greyed out; below it "Windows-Neustart ausstehend" (blue i) – "Treiberänderungen stehen noch aus"; [A] on the
      button does nothing. Restart the PC: the row is gone and "Treiber installieren" works. · E2E: yes
- [ ] **T407-3** On a PC without Arcadia's drivers, open Xbox Zubehör (or PlayStation Accessories): "Treiber
      installieren" is greyed out, below it "Controller-Update wird durchgeführt" – "Treiber kann während eines
      Firmware-Updates nicht installiert werden"; close the app: within a few seconds the button works again. · E2E: yes
- [ ] **T407-4** [A] on "Treiber installieren": "Treiber installieren?" names usbip-win2, HidHide, VIIPER and Pan, the
      Windows prompt and the recommended restart; the sentence about the puck's restart shows only with a Steam
      Controller or puck plugged in; B closes it and nothing is installed. · E2E: yes
- [ ] **T407-5** With Arcadia set up and Pan on, a DualSense connected, open PlayStation Accessories: the DualSense row
      says "PlayStation Accessories ist geöffnet" and Arcadia takes it over only after the app is closed. · E2E: no
      (real app and controller)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T407-1, T407-2 (owner, before the fix) | 2026-11-02 | Juri | 0.2.0-rc.2 (from #406), Windows, own PC, no restart after the uninstall | ❌ no hint to restart at the uninstall; "Treiber installieren" left HidHide out silently (`pan.log`), then "Windows entfernt HidHide beim nächsten Neustart" read as the new HidHide going | #407 |
| T407-2 to T407-4 (automated), T407-1 (notice text and time) | 2026-11-02 | Claude | PR branch, Debug, Linux cloud container, faked parts; T407-2 to T407-4 each seen red once (blocking rows, puck sentence and the row's install guard removed) | ✅ | – |
