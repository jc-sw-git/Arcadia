# #434 Pan service test fails when a Pan process exits while it is checked

No new manual test cases: the race is in finding the hand-off copy among the running Pan processes, which nobody can
time by hand. `HandOffCopyTests` holds it: a process that exited before its image is read counts as no copy (red
before the fix with the `InvalidOperationException` from CI, green after), and a running process is matched by its
own image only. Pan's `--uninstall` and the Windows service test now use the same check, so the second `--uninstall`
beside a running copy (#270) is covered by the existing cases unchanged.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| `HandOffCopyTests` with the old catch (`Win32Exception` only) | 2026-11-03 | Claude | PR branch, Debug, Linux cloud container | ❌ `InvalidOperationException: … the process has exited` | fixed |
| All Pan tests | 2026-11-03 | Claude | PR branch, Debug, Linux cloud container | ✅ 382 passed, 21 skipped (Windows service tests) | – |
