# #376 CI hygiene: job timeouts, SHA-pinned actions, Dependabot, concurrency, split the App tests

No manual case: the change is CI and test code only; nothing in Arcadia changes. Its tests are the CI runs of the PR
itself (both OSes green, run times before and after in the PR) and the test suites run sharded below.

## Test runs

| What | Date | Who | Environment | Result | Notes |
|---|---|---|---|---|---|
| All tests in one process each (before) | 2026-11-03 | Claude | Linux cloud container, 4 cores, .NET 10 | pass | Core 2,372 in 31 s, Pan 369 in 6 s, App 533 in 7:04 (2:43 of it waiting on transitions) |
| `tools/ci/run-tests.sh 3` | 2026-11-03 | Claude | same | pass | 2:53; App shards 182 + 169 + 182 = 533 |
| `tools/ci/run-tests.sh 4` | 2026-11-03 | Claude | same | pass | 2:33; App shards 130 + 137 + 133 + 133 = 533 |
| The two `TakenOverControllersTests` without fixed waits, 25 runs | 2026-11-03 | Claude | same | pass | 25 of 25 |
| `run-tests.sh 4` under `LANG=de_DE.UTF-8` with 4 extra busy cores, twice, after the first CI run | 2026-11-03 | Claude | same | pass | App shards 3:05 to 3:43, Core 39/46 s; 476 of 476 methods ran |
| Coverage with 4 shards (`--collect:"XPlat Code Coverage"`) | 2026-11-03 | Claude | same | pass | 4 Cobertura files beside Core's and Pan's |
| `run-tests.sh 4` with coverage under `LANG=de_DE.UTF-8`, Core and Pan before the shards | 2026-11-03 | Claude | same | pass | 3:14; Core 31 s, Pan 9 s, shards 1:49 to 2:14; coverage from all 6 runs |
| `LibraryServiceTests` and `PanClientTests`, 5 runs | 2026-11-03 | Claude | same | pass | 5 of 5 |
