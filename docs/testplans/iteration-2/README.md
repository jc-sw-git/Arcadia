# Test plan Iteration 2

Milestone "Iteration 2". Format and rules: [README](../README.md). The ticks are set by the owner's full test
before the merge.

**Acceptance review:** release candidate –, by –, on –.

One file per issue. A PR that closes an issue adds its file and lists it here; whoever tests an issue reads only
its file.

| File | Issue | Title | Test cases |
|---|---|---|---|
| [072-support-every-controller-via-sdl3.md](072-support-every-controller-via-sdl3.md) | #72 | Support every common controller independently of Steam via SDL3 | 12 |
| [180-check-ui-changes-visibly.md](180-check-ui-changes-visibly.md) | #180 | Check UI changes visibly yourself: Linux path in arcadia-verify and a focus ring test | 4 |
| [155-split-main-view-model-game-actions.md](155-split-main-view-model-game-actions.md) | #155 | Split MainViewModel: extract GameActions | 0 (refactoring) |
| [212-read-the-steam-controller-directly-with-sdl3.md](212-read-the-steam-controller-directly-with-sdl3.md) | #212 | Read the Steam Controller directly with SDL3 | 10 |
| [169-steam-settings-in-one-group.md](169-steam-settings-in-one-group.md) | #169 | Group the Steam settings under "Steam" | 8 |
| [208-split-main-view-model-tabs-and-page-interface.md](208-split-main-view-model-tabs-and-page-interface.md) | #208 | Split MainViewModel: shared tabs and the page interface | 0 (refactoring) |
| [209-split-main-view-model-search-page.md](209-split-main-view-model-search-page.md) | #209 | Split MainViewModel: the search page handles its own input | 0 (refactoring) |
| [210-split-main-view-model-detail-and-library.md](210-split-main-view-model-detail-and-library.md) | #210 | Split MainViewModel: detail page, LibraryViewModel and routing | 0 (refactoring) |
| [242-steam-controller-copy.md](242-steam-controller-copy.md) | #242 | Steam gets a copy of the Steam Controller (part A) | 25 |

## Exploratory test

Charter: –. Time box: –. Findings: –.

## Acceptance tests

| Test cases | Tests |
|---|---|
| T72-1 (mapping), T72-4, T72-6, T72-7, T72-9, T72-12 | `tests/Arcadia.App.Tests/ControllerTypesTests.cs` |
| T212-2, T212-3, T212-5, T212-6, T212-7, T212-10 | `tests/Arcadia.App.Tests/SteamControllerTests.cs` |
| T169-1 to T169-8 | `tests/Arcadia.App.Tests/SteamSettingsGroupTests.cs` |
| T180-2, T180-3 | `tests/Arcadia.App.Tests/Window/RingGeometryTests.cs`, `tests/Arcadia.App.Tests/Window/FocusRingTests.cs` |
| T155-1 to T155-12 | `tests/Arcadia.App.Tests/PageTransitionTests.cs` |
| T208-1 to T208-3, T209-3 | `tests/Arcadia.App.Tests/PageInterfaceTests.cs` |
| T209-1, T209-2 | `tests/Arcadia.App.Tests/PageTransitionTests.cs`, `Window/SearchViewTests.cs` |
| T210-1 to T210-3 | `tests/Arcadia.App.Tests/RoutingTests.cs` |
| T242-8, T242-10 to T242-16, T242-20 to T242-25 | `tests/Arcadia.App.Tests/PanEmulationTests.cs`, `SteamControllerModeTests.cs` |
