# #156 A launcher says itself whether its games use Steam Input

No new manual test cases: pure refactoring without new behaviour (owner, refinement 2026-10-28). Defaults stay Steam
games "Steam-Eingabe", all others "Arcadia"; covered by `ControllerSupportTests` (Core) and the existing acceptance tests
of the Steam Controller mode.

History:

| Date | Environment | Run |
|---|---|---|
| 2026-10-28 | Linux cloud container, .NET 10.0.113 | `dotnet test Arcadia.slnx` green; the unknown-launcher test made red once by defaulting `LauncherRegistry.GamesUseSteamInput` to true |
