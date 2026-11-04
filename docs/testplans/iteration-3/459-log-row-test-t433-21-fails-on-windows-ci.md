# #459 Log-row test T433-21 fails on Windows CI now and then

**Status:** no new cases; the touched case runs as an automated test (see History).
**Open for the owner:** nothing.

- [x] **T433-21** (automated) Big Picture, gamepad: A on each log row opens that log, and each open by controller asks
      for the mouse mode. · E2E: yes

No new manual test cases: the failure was in the test, not in Arcadia; T433-21 keeps its meaning and stays automated.

- **Cause** (`RecentMessagesAndLogsTests.A_on_a_log_row_opens_that_log_and_by_controller_asks_for_the_mouse_mode`):
  the test waited for "last opened log is Arcadia and as many opens by controller as logs opened". Before the first
  open the list is empty, its `LastOrDefault()` is `LogKind.Arcadia` (the enum's first value) and both counts are 0,
  so the wait for the Arcadia log was met at once. A then went to the VIIPER row while Arcadia's open was still
  queued on the thread pool; when VIIPER's open ran first, the list ended `[Viiper, Arcadia]` and the wait for
  "VIIPER last" never came true.
- **Fix:** the test takes the number of opened logs before A and waits for exactly one more, with this log last.
  `FakeLogFiles.Opened` hands out a copy under its lock, as the opens run off the UI thread.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T433-21 old wait, 40× in one process | 2026-11-04 | implementation | PR branch, Release, Linux cloud container (headless) | ❌ 3 of 40: `opened=[Viiper,Arcadia]` | wait by count |
| T433-21 old wait, Arcadia's open slowed to 300 ms in `FakeLogFiles` | 2026-11-04 | implementation | same | ❌ 3 of 3 | confirms the cause |
| T433-21 new wait, Arcadia's open slowed to 300 ms | 2026-11-04 | implementation | same | ✅ 1 of 1 | – |
| T433-21 new wait, 40× in one process, 3 runs; and 25 separate runs | 2026-11-04 | implementation | same | ✅ 120 of 120; 25 of 25 | – |
| Full suite as CI runs it (`tools/ci/run-tests.sh 4`) | 2026-11-04 | implementation | same | ✅ | – |
