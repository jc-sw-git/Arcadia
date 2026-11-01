# #374 Split ControllerSupportService (SISR removal out) and share the composition root with the tests

No manual case: the change moves code (the SISR removal into `SisrLeftovers`, the wiring into `ArcadiaComposition`)
without changing behaviour; the existing unit tests (`ControllerSupportServiceTests`) and acceptance tests
(`SisrRemovalTests`, all of `Arcadia.App.Tests`, now built through the App's own composition) cover it.

## Test runs

| What | Date | Who | Environment | Result | Notes |
|---|---|---|---|---|---|
| `dotnet test Arcadia.slnx` | 2026-11-01 | Claude | Linux cloud container, .NET 10.0.114 | pass | Core 2,276, App 491, Pan 331 passed |
| Demo start (`--demo`, Release, xvfb) | 2026-11-01 | Claude | Linux cloud container | pass | library of 173 demo games shown, Steam Controller wiring logged ready, no error; SISR is Windows-only, so its removal ran only in the acceptance tests (`SisrRemovalTests`, fake PC) |
