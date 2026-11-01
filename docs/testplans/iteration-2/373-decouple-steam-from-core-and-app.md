# #373 Decouple Steam from Core, ControllerSupport and App; validate launcher ids

No manual case: the change moves where Steam is known (its identity into its integration, its program behind
`ILauncherProgram`, the process helpers into `Arcadia.Platform`) without changing behaviour. `ArchitectureTests`
checks the allowed references (each rule red once with a planted reference), `LauncherRegistryTests` the id rule and a
throwing plugin, and the existing unit and acceptance tests (driver installs that close Steam, SISR removal, the
Steam Controller's routing, the Steam page's desktop layout group) cover the flows.

## Test runs

| What | Date | Who | Environment | Result | Notes |
|---|---|---|---|---|---|
| `dotnet test Arcadia.slnx` | 2026-11-01 | Claude | Linux cloud container, .NET 10 | pass | Core 2,305, App 492, Pan 331 passed |
| Demo start (`--demo`, xvfb), launcher and controller settings opened | 2026-11-01 | Claude | Linux cloud container | pass | 173 demo games, no error in the log; Steam's driver install and SISR removal are Windows-only, so they ran only in the acceptance tests |
