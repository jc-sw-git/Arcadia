# #423 Restart notice at the uninstall never shows

No new manual test case: the notice is T407-1 (`../iteration-2/407-driver-install-waits-for-restart-or-firmware-update.md`),
which this fix makes pass. It needs a real uninstall and UAC, so it runs in the release candidate test (RC). The
scenarios of the issue are covered by `ControllerSupportRemovalTests` (`RestartNoticeDue`): Ja with a driver and the UAC
prompt accepted shows the notice even though nothing reads as pending yet; UAC declined, no Pan in the package, no
driver, or Nein/no answer show none. Scenario 5 (no time left) is the unchanged `RestartNoticeTime`.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| `RestartNoticeDue` with today's logic (driver state read right after Pan's hand-off, faked as nothing pending) | 2026-11-03 | Claude | PR branch, Debug, Linux cloud container | ❌ no notice | decision no longer reads the driver state |
| `ControllerSupportRemovalTests` | 2026-11-03 | Claude | PR branch, Debug, Linux cloud container | ✅ 12 passed | – |
