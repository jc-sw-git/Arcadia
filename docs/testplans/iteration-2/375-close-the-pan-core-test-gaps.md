# #375 Close the Pan.Core test gaps the mutation run found

No new test cases: tests only, no behaviour changed; the new unit tests are in `tests/Arcadia.Pan.Tests`
(`GuardianTests`, `ControllerHidingTests`, `HidingTests`, `JournalTests`, `TeardownTests`, `PanWindowsAdapterTests`).

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| unit tests | 2026-11-01 | Claude | branch `test/close-pan-core-test-gaps-375`, Linux container (Windows-only tests skipped there, run by the Windows CI) | ✅ | – |
