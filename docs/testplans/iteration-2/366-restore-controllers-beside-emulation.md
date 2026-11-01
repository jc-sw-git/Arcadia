# #366 "Controller wiederherstellen" runs beside Arcadia's own emulation

Before Pan's `--restore`, Arcadia ends its emulation and lets go of Pan (after a hide still under way has its answer),
and connects again afterwards. `arcadia.log` says "Pan: Controller werden wiederhergestellt – Arcadia verbindet sich
erst danach wieder" while the restore runs.

- [ ] **T366-1** Pan on, a game on "Arcadia" running with the Steam Controller as Xbox pad and a second controller
      (e.g. a DualSense) taken over. Bring Arcadia to the front, Einstellungen → Controller → "Controller
      wiederherstellen" with the controller alone, confirm the Windows prompt. While the prompt is up the pad is gone
      (the log line above); after it, both controllers work in Windows (`joy.cpl`) and in the game again within a few
      seconds, no settings row stays red, no toast about a lost emulation. `pan.log`: the service's teardown ("Client …
      gone while emulating") comes before "Restore complete". · E2E: yes (App `SteamControllerXboxPadTests` T366-1;
      Core `ControllerSupportServiceTests` T366-1, `PanClientTests` with Pan's real `Guardian`; real UAC, HidHide and
      controllers are manual)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T366-1 (automated) | 2026-11-01 | Claude | branch build, Linux cloud container, fakes for Pan, UAC and controllers; `PanClientTests` against the real `Guardian` and journal | ✅ green; each new test red once with the let-go removed | Real UAC, HidHide and controllers need the owner's test |
