# #589 Fix the flaky tests that failed once in Iteration 3's last merges

No new test cases: the changes are in test code and in `MetadataService.WhenIdleAsync`, which only the tests call;
nothing the player sees changes. Also covers #575 and #582.

Causes, from the CI logs of the failed attempts:

| Test (where) | Failure | Cause | Change |
|---|---|---|---|
| `MetadataServiceTests.Cancelling_a_reload…` T547-3 (#582, local) | `Assert.True()` on `ReloadAll()` | `MetadataService.WhenIdleAsync` awaited the runner it saw; a runner that ends while new work came in starts the next one from its last step, so the awaited one completed with another still running and the test went on mid-run | `WhenIdleAsync` waits until no new runner or cache write took the place of the ones it awaited |
| `MetadataServiceTests.A_game_leaving_the_library_and_coming_back…` (#578, Ubuntu) | 3 requests instead of 2 | same | same |
| `DetailMetadataTests.Offline_after_a_fetch…` T493-20 (`main` after #567, Ubuntu) | description `null` | same, through the App's `RunToIdleAsync` | same |
| `MetadataServiceIgdbTests.Igdb_switched_off…` (#579, Windows) | `TimeoutException` after 5 s waiting for `Changed` | likely the same (no IGDB data yet, so switching it off changed nothing); else the thread pool on a busy runner | same, and the wait's net is 30 s |
| `ControllerSupportServiceTests.Without_a_report_Pan_s_steps…` T413-14 (`main` after #578, Windows) | `not reached in 10 s: der eine Schritt`, both variants | the Windows runner was slow: the steps up to the hand-over alone took 6 s of the run's 16 s | the net of `WaitForAsync` (the waits move on fake time) is 60 s |
| `ControllerHidingTests.A_running_controller_hide_holds_the_hang_watch…` (`main` after #584, Windows) | `TimeoutException` after 5 s waiting for the scan | the scan runs on the thread pool, which the busy runner served more than 5 s late | the test's wait and the fake's held scan share a 30 s limit |
| `ServiceTests.The_installed_service_guards…` (#575, Windows) | the fake VIIPER (a PowerShell copy) didn't print its port in 20 s; the service and the process stayed and broke `PipeServerTests` | a cold PowerShell start on a busy runner; `Dispose` caught only `IOException` and killed no process tree | the port is awaited for 2 min; `Dispose` stops and deletes a service the test left, ends the fake's process tree and doesn't throw over a locked file |
| `GameFilterTests.Counting_five_thousand_entries…` (#591, Windows; local under load) | 164.6 ms and 324 ms against 160 ms | one wall-clock measurement of 10 rounds; a runner busy with other tests slows a single try | the fastest of five tries counts; slower code still fails every try |
| T500-15 `QuickSelectionTests.Unticking_every_entry…` (#580, Windows) | `NullReferenceException`: no detail page after holding A | not reproduced (see History); background metadata runs were seen to start and end during the hold | none; a new failure with its log goes into a `bug` issue |
| `QuickSelectionTests.Turning_the_carousel…` (local, under load) | no log kept | not reproduced | none |
| a test of the first start's step "Datenquellen" (#579, Ubuntu); "test host failed to start" (#577, Ubuntu) | no log found in the failed attempts still kept | the first not reproduced; the second is the runner's test host, outside the code | none |

The new `WhenIdleAsync` has no seam for a deterministic regression test: the window is between a runner's last look at
the queue and its end. The check below injected delays there (`[DEBUG-589]`, removed) to make it red.

## #575 Flaky on Windows CI: ServiceTests' fake VIIPER times out and leaves the pipe for PipeServerTests

No new test cases: covered here, see the `ServiceTests` row above.

## #582 Flaky: T547-3 "Cancelling a reload keeps the old data" fails now and then

No new test cases: covered here, see the T547-3 row above; T547-3 keeps its meaning.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| Core 3× and Core on one core beside 3 busy loops, 2× (before the change) | 2026-11-14 | Claude | `main` at e204dac, Release, Linux cloud container, 4 cores | ✅ | the Windows failures don't show on Linux |
| T547-3 and coming_back, 30× in one process, delays injected after `WhenIdleAsync`'s look and before a runner's end | 2026-11-14 | Claude | same | ❌ 1 of 30: game 3's description `null` | confirms the cause |
| the same with the new `WhenIdleAsync`, 60× | 2026-11-14 | Claude | PR branch, same | ✅ 60 of 60 | – |
| Pan tests with the thread pool capped at 4 threads | 2026-11-14 | Claude | `main`, same | ❌ 8 tests time out in their waits | the Pan tests' 5–10 s nets depend on the pool |
| Hold A right after the start, 100×; and with a rescan, `FactsChanged`, the window activated or a metadata merge during the hold | 2026-11-14 | Claude | `main`, same, 6 busy loops | ✅ | T500-15 not reproduced |
| 200× each in one process, beside 6 busy loops: T547-3, coming_back, IGDB switched off, T413-14 (exit 0 and 3), the filter budget, the controller hide, T493-20 | 2026-11-14 | Claude | PR branch, same | ✅ 200 of 200 each | – |
| All tests as CI runs them (`tools/ci/run-tests.sh 4`) | 2026-11-14 | Claude | PR branch, same | ✅ Core 3,422, Pan 444, App 870 of 870 | – |
| `ServiceTests` | – | – | only on a Windows runner with admin rights | see the PR's CI | – |
