# #423 Restart notice at the uninstall never shows

The notice itself is T407-1 (`../iteration-2/407-driver-install-waits-for-restart-or-firmware-update.md`),
which this fix makes pass. It needs a real uninstall and UAC, so it runs in the release candidate test (RC). The
scenarios of the issue are covered by `ControllerSupportRemovalTests` (`RestartNoticeDue`): Ja with a driver and the UAC
prompt accepted shows the notice even though nothing reads as pending yet; UAC declined, no Pan in the package, no
driver, or Nein/no answer show none. Scenario 5 (no time left) is the unchanged `RestartNoticeTime`.

- [ ] **T423-1** Driver installed, uninstall Arcadia, answer "Treiber für die Controller-Emulation auch entfernen?"
      with Ja and decline the Windows prompt: no message to restart the PC follows; usbip-win2 and HidHide are still
      under "Apps". Runs in the same uninstall as T422-4. · E2E: no (real uninstall and UAC) · RC

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| `RestartNoticeDue` with today's logic (driver state read right after Pan's hand-off, faked as nothing pending) | 2026-11-03 | Claude | PR branch, Debug, Linux cloud container | ❌ no notice | decision no longer reads the driver state |
| `ControllerSupportRemovalTests` | 2026-11-03 | Claude | PR branch, Debug, Linux cloud container | ✅ 12 passed | – |
| QE review: `ControllerSupportRemovalTests`, build of the solution | 2026-11-03 | Claude (QE) | PR branch 38b844e, Debug, Linux cloud container | ✅ 12 passed, 0 warnings | T423-1 added for scenario 2 |
