# #410 A missing part shows a contradicting HidHide row and still offers "Controller zuordnen"

While a part of the emulation is missing (usbip-win2, VIIPER, Pan, HidHide), too old or waits for the restart, the
settings show only that part's row below "Controller-Wächter (Pan)": no row from the emulation's state beside it
(e.g. "HidHide ist nicht aktiv" next to "HidHide fehlt") and no "Controller zuordnen". Needed: Windows, Arcadia set
up with Pan on, a way to remove HidHide (Windows' Apps, then a restart), a controller.

Automated: `EveryControllerForGamesTests` (HidHide missing, Pan on, a DualSense connected).

- [ ] **T410-1** Uninstall HidHide in Windows' Apps and restart the PC; Pan on, a controller connected; Einstellungen →
      Controller: below the switch only "HidHide fehlt" with "Installieren", no "HidHide ist nicht aktiv", no
      "Controller zuordnen"; the switch says "Es liegt ein Problem vor. Prüfe die Warnmeldungen darunter.". After
      "Installieren" (and Arcadia's restart) "Controller zuordnen" is back. · E2E: yes

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T410-1 (owner, before the fix) | 2026-11-03 | Juri | 0.2.0-rc.2 (from #406), Windows, own PC, HidHide gone after the restart | ❌ "HidHide fehlt" and "HidHide ist nicht aktiv" together, "Controller zuordnen" offered | #410 |
| T410-1 (automated) | 2026-11-03 | Claude | PR branch, Debug, Linux cloud container, faked parts; seen red with both guards removed (both rows and "Controller zuordnen" shown) | ✅ | – |
