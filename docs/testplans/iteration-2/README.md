# Test plan Iteration 2

Milestone "Iteration 2". Format and rules: [README](../README.md). The ticks are set by the owner's full test
before the merge.

**Acceptance review:** release candidate –, by –, on –.

One file per issue. A PR that closes an issue adds its file and lists it here; whoever tests an issue reads only
its file.

| File | Issue | Title | Test cases |
|---|---|---|---|
| [072-support-every-controller-via-sdl3.md](072-support-every-controller-via-sdl3.md) | #72 | Support every common controller independently of Steam via SDL3 | 12 |
| [169-steam-settings-in-one-group.md](169-steam-settings-in-one-group.md) | #169 | Group the Steam settings under "Steam" | 8 |

## Exploratory test

Charter: –. Time box: –. Findings: –.

## Acceptance tests

| Test cases | Tests |
|---|---|
| T72-1 (mapping), T72-4, T72-6, T72-7, T72-9, T72-12 | `tests/Arcadia.App.Tests/ControllerTypesTests.cs` |
| T169-1 to T169-8 | `tests/Arcadia.App.Tests/SteamSettingsGroupTests.cs` |
