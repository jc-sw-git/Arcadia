# Test plan Iteration 2

Milestone "Iteration 2". Format and rules: [README](../README.md). The ticks are set by the owner's full test
before the merge.

**Acceptance review:** release candidate –, by –, on –.

One file per issue. A PR that closes an issue adds its file and lists it here; whoever tests an issue reads only
its file.

| File | Issue | Title | Test cases |
|---|---|---|---|
| [155-split-main-view-model-game-actions.md](155-split-main-view-model-game-actions.md) | #155 | Split MainViewModel: extract GameActions | 0 (refactoring) |
| [169-steam-settings-in-one-group.md](169-steam-settings-in-one-group.md) | #169 | Group the Steam settings under "Steam" | 8 |
| [208-split-main-view-model-tabs-and-page-interface.md](208-split-main-view-model-tabs-and-page-interface.md) | #208 | Split MainViewModel: shared tabs and the page interface | 0 (refactoring) |
| [209-split-main-view-model-search-page.md](209-split-main-view-model-search-page.md) | #209 | Split MainViewModel: the search page handles its own input | 0 (refactoring) |
| [210-split-main-view-model-detail-and-library.md](210-split-main-view-model-detail-and-library.md) | #210 | Split MainViewModel: detail page, LibraryViewModel and routing | 0 (refactoring) |

## Exploratory test

Charter: –. Time box: –. Findings: –.

## Acceptance tests

| Test cases | Tests |
|---|---|
| T169-1 to T169-8 | `tests/Arcadia.App.Tests/SteamSettingsGroupTests.cs` |
| T155-1 to T155-12 | `tests/Arcadia.App.Tests/PageTransitionTests.cs` |
| T208-1 to T208-3, T209-3 | `tests/Arcadia.App.Tests/PageInterfaceTests.cs` |
| T209-1, T209-2 | `tests/Arcadia.App.Tests/PageTransitionTests.cs`, `Window/SearchViewTests.cs` |
| T210-1 to T210-3 | `tests/Arcadia.App.Tests/RoutingTests.cs` |
