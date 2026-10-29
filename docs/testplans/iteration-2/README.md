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
| [213-steam-controller-as-xbox-pad-without-sisr.md](213-steam-controller-as-xbox-pad-without-sisr.md) | #213 | Steam Controller as an Xbox controller in every game, without SISR | 13 |
| [242-steam-controller-copy.md](242-steam-controller-copy.md) | #242 | Steam gets a copy of the Steam Controller (parts A, B and C) | 29 |
| [297-arcadia-runs-only-with-a-compatible-pan.md](297-arcadia-runs-only-with-a-compatible-pan.md) | #297 | Arcadia runs only with a compatible Pan | 12 |
| [214-remove-or-keep-sisr.md](214-remove-or-keep-sisr.md) | #214 | Remove SISR on request, or keep it, in the controller settings | 13 |
| [215-show-the-button-symbols-of-the-controller-in-use.md](215-show-the-button-symbols-of-the-controller-in-use.md) | #215 | Show the button symbols of the controller in use | 8 |
| [308-pan-hide-warning-loops-with-only-the-puck.md](308-pan-hide-warning-loops-with-only-the-puck.md) | #308 | Pan: hide warning loops with only the puck connected, then emulation stops | 6 |
| [141-software-tools-and-soundtracks.md](141-software-tools-and-soundtracks.md) | #141 | Software, tools and soundtracks in the library | 12 |
| [083-steam-sign-in-by-qr-code.md](083-steam-sign-in-by-qr-code.md) | #83 | Steam sign-in by QR code for owned games | 8 |
| [278-follow-steam-downloads-live.md](278-follow-steam-downloads-live.md) | #278 | Follow Steam's install progress, pauses and account switches live | 8 |
| [280-steam-sign-in-on-steams-login-page.md](280-steam-sign-in-on-steams-login-page.md) | #280 | Steam sign-in on Steam's own login page | 11 |
| [277-detect-a-steam-game-start-that-steam-refused.md](277-detect-a-steam-game-start-that-steam-refused.md) | #277 | Detect a Steam game start that Steam refused | 7 |
| [178-steam-controller-mode-default-per-launcher.md](178-steam-controller-mode-default-per-launcher.md) | #178 | Choose the Steam Controller mode default per launcher | 12 |

## Exploratory test

Charter: –. Time box: –. Findings: –.

## Acceptance tests

| Test cases | Tests |
|---|---|
| T72-1 (mapping), T72-4, T72-6, T72-7, T72-9, T72-12 | `tests/Arcadia.App.Tests/ControllerTypesTests.cs` |
| T212-2, T212-3, T212-5, T212-6, T212-7, T212-10 | `tests/Arcadia.App.Tests/SteamControllerTests.cs` |
| T169-1 to T169-3 (T169-4 and T169-5 went with the link, #178; T169-6 to T169-8 with the SISR row, #214) | `tests/Arcadia.App.Tests/SteamSettingsGroupTests.cs` |
| T180-2, T180-3 | `tests/Arcadia.App.Tests/Window/RingGeometryTests.cs`, `tests/Arcadia.App.Tests/Window/FocusRingTests.cs` |
| T155-1 to T155-12 | `tests/Arcadia.App.Tests/PageTransitionTests.cs` |
| T208-1 to T208-3, T209-3 | `tests/Arcadia.App.Tests/PageInterfaceTests.cs` |
| T209-1, T209-2 | `tests/Arcadia.App.Tests/PageTransitionTests.cs`, `Window/SearchViewTests.cs` |
| T210-1 to T210-3 | `tests/Arcadia.App.Tests/RoutingTests.cs` |
| T242-8, T242-10 to T242-16, T242-20 to T242-25 | `tests/Arcadia.App.Tests/PanEmulationTests.cs`, `SteamControllerModeTests.cs`, `Window/HoldButtonFillTests.cs` (T242-20, the fill) |
| T297-1 to T297-8 | `tests/Arcadia.App.Tests/PanUpdateAtStartTests.cs` |
| T214-1 to T214-8, T214-11; T214-10 | `tests/Arcadia.App.Tests/SisrRemovalTests.cs`; `SteamControllerModeTests.cs` |
| T215-1 to T215-6 | `tests/Arcadia.App.Tests/Window/ButtonSymbolsTests.cs` |
| T308-2, T308-3 | `tests/Arcadia.App.Tests/PanEmulationTests.cs` |
| T141-1, T141-3, T141-5, T141-7, T141-8, T141-9 | `tests/Arcadia.App.Tests/EntryTypeTests.cs` |
| T141-4, T141-7 (drawn) | `tests/Arcadia.App.Tests/Window/EntryTypeViewTests.cs` |
| T83-1 (faked Steam), T83-2, T83-5, T83-6, T83-7 | `tests/Arcadia.App.Tests/SteamAccountTests.cs` |
| T277-1, T277-3 to T277-5, T277-7 | `tests/Arcadia.App.Tests/StartRefusedTests.cs` |
| T278-2 to T278-5 (faked Steam) | `tests/Arcadia.App.Tests/SteamInstallTests.cs` |
| T278-8 (faked launcher) | `tests/Arcadia.App.Tests/GameStartTests.cs` |
| T178-1 to T178-9; T178-10 | `tests/Arcadia.App.Tests/LauncherControllerModeTests.cs`; `Window/LauncherControllerModeViewTests.cs` |
