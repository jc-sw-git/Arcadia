# #470 Debugger stops Arcadia at "Controller wiederherstellen" while a virtual Xbox pad is plugged in

**Status:** Automated: T470-1 · QE: T470-1 rerun, red once checked (no app run: the path needs Windows, VIIPER and the debugger) · Open for the owner: T470-2

- [x] **T470-1** A virtual Xbox pad whose VIIPER stream fails, ends halfway through a rumble report or is disposed while it waits for rumble reports itself as gone and ends the read without an exception leaving Arcadia's code; two bytes are still the large and the small motor, and an unplug's cancel ends the read quietly without counting as broken · E2E: no (unit test on the read step; VIIPER needs Windows and usbip)
- [ ] **T470-2** **Owner:** Debug build under the VS Code debugger, emulation running with the Xbox pad for a controller, "Controller wiederherstellen": the debugger doesn't stop, the Windows prompt comes, after "Ja" the controllers are restored and the emulation comes back; the PC doesn't hang · E2E: no (debugger, UAC, real Steam Controller)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T470-2 (before the fix) | 2026-11-04 | owner | `main` (Debug, VS Code debugger), real PC | ❌ debugger stops at `ReadOutputAsync` with `IOException`, Arcadia frozen, Pan can't restart the Steam Controller | #470 |
| T470-1 with the old read (exception passed on) | 2026-11-04 | Claude | PR branch, Debug, Linux cloud container | ❌ 3 of 5 fail with `IOException`, `EndOfStreamException`, `ObjectDisposedException` | fixed |
| T470-1 | 2026-11-04 | Claude | PR branch, Debug, Linux cloud container | ✅ 5 passed; whole suite green (Core 2449, Pan 422, App 623) | – |
| T470-1 (QE review, read error passed on again) | 2026-11-04 | Claude | PR #473, Release, Linux cloud container | ❌ 3 of 5 fail, as expected | read restored |
| T470-1 (QE review) | 2026-11-04 | Claude | PR #473, Release, Linux cloud container | ✅ 5 passed; `tools/ci/run-tests.sh 4` green (Core 2449, Pan 422, App 623) | – |
