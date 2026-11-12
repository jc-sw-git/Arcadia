# #518 Run the layout guards over one shared page list

No new test cases: test infrastructure only.

The window guards walk `tests/Arcadia.App.Tests/Guards/GuardPages.cs`; `GuardPageListTests` (no window, every PR) fails
while a page, dialog or settings category is missing from it. Guard cases: 47 before, 43 after.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| `GuardPageListTests` with six entries' view models left out of the list | 2026-11-12 | Claude | PR branch, Release, Linux cloud container | ✅ red, names the six missing view models | – |
| `tools/ci/run-tests.sh 4` (PR set: 648 App methods, no window) | 2026-11-12 | Claude | PR branch, Release, Linux cloud container | ✅ 1:12 min | – |
| `WINDOW_GUARDS=1 tools/ci/run-tests.sh 4` (main set: 683 App methods) | 2026-11-12 | Claude | PR branch, Release, Linux cloud container | ✅ 3:50 min | – |
| Focus ring guard on the newly walked pages | 2026-11-12 | Claude | PR branch, Release, Linux cloud container | ❌ search: the focused hit in the first row has its ring only half as bright at the top | State left out in its entry (`RingFinding`); bug for the owner to decide |
