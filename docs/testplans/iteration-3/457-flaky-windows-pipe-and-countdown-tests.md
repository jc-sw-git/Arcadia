# #457 Pan pipe test T413-25 fails on Windows CI now and then

No new manual test cases: both failures were in the tests, not in Arcadia or Pan; T413-25 and T340-1 keep their
meaning and stay automated.

- **T413-25** (`InstallHandOverPipeTests.Another_process_is_refused_and_Arcadia_still_gets_through`): the second pipe
  client was `powershell.exe`, whose start on a busy Windows runner sometimes took longer than the 60 s the test gave
  it (`the other client ended` after 1 min, green on re-run). The client is now `cmd.exe` redirecting `echo` into the
  pipe, which opens it, is refused and ends at once. The test also waited on nothing between the client's end and
  reading Pan's log; it now awaits the refusal's log line (10 s).
- **T340-1** (`PanStoppingViewTests`, scaling 2): the countdown reads the clock after each Pan check that runs off the UI
  thread. When the test moved the fake clock on while a check was still running, the countdown skipped a second, so
  it could jump from 10 to 8 and the loop waiting for exactly "9" ran on to "5". The test now waits for any one-digit
  second, which is what it checks (the number doesn't move when it loses a digit).

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T340-1 old loop, Pan check slowed to 20 ms in `FakeSteam` | 2026-11-04 | Claude | PR branch, Debug, Linux cloud container (headless) | ❌ scaling 2: `Expected "9", Actual "6"` (CI: "5") | wait for any one-digit second |
| T340-1 new loop, Pan check slowed to 20 ms | 2026-11-04 | Claude | PR branch, Debug, Linux cloud container (headless) | ✅ 3 passed | – |
