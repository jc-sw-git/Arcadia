# #434 Pan service test fails when a Pan process exits while it is checked

No new manual test cases: the race is in finding the hand-off copy among the running Pan processes, which nobody can
time by hand. `HandOffCopyTests` holds it: a process that exited before its image is read counts as no copy (red
before the fix with the `InvalidOperationException` from CI, green after), and a running process is matched by its
own image only. Pan's `--uninstall` and the Windows service test now use the same check, so the second `--uninstall`
beside a running copy (#270) is covered by the existing cases unchanged.

The same service test had a second race (T422-4): it deleted the Arcadia stand-in while the service was running, but
the service's pipe opens before it looks for Arcadia, so a late first look found none and Pan removed itself before the
test stopped it ("service does not exist" in `WaitForStatus`). The stand-in now goes while the service is stopped.
This one shows only on a Windows runner; it was not made red on purpose.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| `HandOffCopyTests` with the old catch (`Win32Exception` only) | 2026-11-03 | Claude | PR branch, Debug, Linux cloud container | ❌ `InvalidOperationException: … the process has exited` | fixed |
| All Pan tests | 2026-11-03 | Claude | PR branch, Debug, Linux cloud container | ✅ 382 passed, 21 skipped (Windows service tests) | – |
| Windows service test, second race (T422-4) | 2026-11-03 | CI | PR #436, Windows runner | ❌ `Cannot open 'ArcadiaPan' service` at `ServiceTests.cs:300` | stand-in deleted only while the service is stopped |
