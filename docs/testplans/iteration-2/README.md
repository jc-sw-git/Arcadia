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
| [313-pan-lets-arcadia-release-the-puck-before-its-own-restarts.md](313-pan-lets-arcadia-release-the-puck-before-its-own-restarts.md) | #313 | Pan: let Arcadia release the puck before Pan's own puck restarts | 0 (T308-7) |
| [141-software-tools-and-soundtracks.md](141-software-tools-and-soundtracks.md) | #141 | Software, tools and soundtracks in the library | 12 |
| [083-steam-sign-in-by-qr-code.md](083-steam-sign-in-by-qr-code.md) | #83 | Steam sign-in by QR code for owned games | 8 |
| [278-follow-steam-downloads-live.md](278-follow-steam-downloads-live.md) | #278 | Follow Steam's install progress, pauses and account switches live | 8 |
| [280-steam-sign-in-on-steams-login-page.md](280-steam-sign-in-on-steams-login-page.md) | #280 | Steam sign-in on Steam's own login page | 11 |
| [277-detect-a-steam-game-start-that-steam-refused.md](277-detect-a-steam-game-start-that-steam-refused.md) | #277 | Detect a Steam game start that Steam refused | 7 |
| [178-steam-controller-mode-default-per-launcher.md](178-steam-controller-mode-default-per-launcher.md) | #178 | Choose the Steam Controller mode default per launcher | 12 |
| [315-steam-desktop-layout-on-steams-page.md](315-steam-desktop-layout-on-steams-page.md) | #315 | "Steams Desktop-Steuerung zulassen" on Steam's page, alike for every controller | 7 |
| [304-own-viiper-loopback-auth.md](304-own-viiper-loopback-auth.md) | #304 | Run only Arcadia's own VIIPER, on loopback, with Windows-assigned ports and API auth | 12 |
| [326-announce-the-pan-update-and-let-the-player-decline-it.md](326-announce-the-pan-update-and-let-the-player-decline-it.md) | #326 | Announce the Pan update before the Windows prompt opens, and let the player decline it | 11 |
| [335-pan-hides-every-controller.md](335-pan-hides-every-controller.md) | #335 | Pan hides every controller Arcadia takes over | 5 |
| [238-every-controller-through-arcadia.md](238-every-controller-through-arcadia.md) | #238 | Pass every controller to games through Arcadia | 13 |
| [340-pan-stopping-page-names-the-usual-30-seconds.md](340-pan-stopping-page-names-the-usual-30-seconds.md) | #340 | Say on the "Pan wird beendet" page that the old Pan ends after about 30 seconds | 4 |
| [354-pan-update-at-start-without-restarting-the-old-pan.md](354-pan-update-at-start-without-restarting-the-old-pan.md) | #354 | Pan update at start fails because Arcadia restarts the old Pan while --install waits for it to stop | 2 |
| [272-measure-pan-cost-in-a-release-build.md](272-measure-pan-cost-in-a-release-build.md) | #272 | Measure Pan's cost in a Release build (T243-21) | 0 (T243-21) |
| [295-spike-steams-firmware-path.md](295-spike-steams-firmware-path.md) | #295 | Spike: Steam's firmware update path, and what Arcadia guards | 6 |
| [345-hide-and-copy-the-steam-controller-by-cable.md](345-hide-and-copy-the-steam-controller-by-cable.md) | #345 | Hide and copy the Steam Controller by cable | 7 |
| [357-xbox-pad-only-while-a-game-on-arcadia-is-in-front.md](357-xbox-pad-only-while-a-game-on-arcadia-is-in-front.md) | #357 | Steam Controller: Xbox pad passes input only while a game on "Arcadia" is in front | 4 |
| [360-keep-pans-memory-under-20-mb.md](360-keep-pans-memory-under-20-mb.md) | #360 | Pan's memory grows to 22 MB while Arcadia is connected | 0 (T243-21) |
| [112-spike-steam-input-without-sisr.md](112-spike-steam-input-without-sisr.md) | #112 | Spike: own Steam Input helper instead of SISR | 0 (spike) |
| [156-launcher-states-steam-input.md](156-launcher-states-steam-input.md) | #156 | A launcher says itself whether its games use Steam Input | 0 (refactoring) |
| [227-check-install-folder-off-ui-thread.md](227-check-install-folder-off-ui-thread.md) | #227 | Check a focused game's install folder off the UI thread | 5 |
| [228-library-snapshot-session-and-page-lifecycle.md](228-library-snapshot-session-and-page-lifecycle.md) | #228 | Follow-ups to the MainViewModel split: sorted snapshot, UI-free session, page lifecycle | 4 |
| [229-library-focus-page-and-tab-changes-instant.md](229-library-focus-page-and-tab-changes-instant.md) | #229 | Make focus, page and tab changes in the library feel instant | 3 |
| [239-spike-emulated-steam-controller.md](239-spike-emulated-steam-controller.md) | #239 | Spike: Emulated Steam Controller for Steam (HidHide + VIIPER proxy) | 0 (spike) |
| [243-pan-guardian-service.md](243-pan-guardian-service.md) | #243 | Pan, the guardian service (part A) | 45 |
| [249-spike-steam-controller-by-cable.md](249-spike-steam-controller-by-cable.md) | #249 | Spike: hide and copy the new Steam Controller by cable | 0 (spike) |
| [270-remove-bundled-pan-after-uninstall.md](270-remove-bundled-pan-after-uninstall.md) | #270 | Remove the bundled Pan.exe that stays behind after uninstalling with the drivers | 6 |
| [296-restart-the-puck-only-while-steam-is-closed.md](296-restart-the-puck-only-while-steam-is-closed.md) | #296 | Restart the Steam Controller's puck for a driver install only while Steam is closed | 7 |
| [307-demo-game-does-not-lock-pan.md](307-demo-game-does-not-lock-pan.md) | #307 | A demo game shown as running no longer locks the Pan switch | 2 |
| [314-steam-requests-to-empty-puck-slots.md](314-steam-requests-to-empty-puck-slots.md) | #314 | Steam's refused requests to the puck no longer flood the log | 2 |
| [325-takeover-dialog-names-the-steam-controller.md](325-takeover-dialog-names-the-steam-controller.md) | #325 | Pan: takeover dialog at Arcadia's start shows only the puck | 2 |
| [331-steam-lists-controllers-twice.md](331-steam-lists-controllers-twice.md) | #331 | Steam lists the Steam Controller and Arcadia's Xbox pad only once | 3 |
| [347-remove-stale-device-entries.md](347-remove-stale-device-entries.md) | #347 | Remove the stale device entries Arcadia's emulated devices leave in Windows | 7 |
| [363-deeply-nested-vdf-files-overflow-the-stack.md](363-deeply-nested-vdf-files-overflow-the-stack.md) | #363 | Deeply nested VDF files overflow the stack and end Arcadia | 5 |
| [364-culture-proof-tests.md](364-culture-proof-tests.md) | #364 | Tests fail on a German Windows: culture-dependent formatting in tests and in Pan's log | 0 (the suite) |
| [365-docs-after-the-sisr-replacement.md](365-docs-after-the-sisr-replacement.md) | #365 | Docs after the SISR replacement: README, notices, architecture docs, decisions and the latency budget | 0 (docs) |
| [366-restore-controllers-beside-emulation.md](366-restore-controllers-beside-emulation.md) | #366 | "Controller wiederherstellen" runs beside Arcadia's own emulation | 1 |
| [372-bind-steam-sign-in-to-steams-hosts.md](372-bind-steam-sign-in-to-steams-hosts.md) | #372 | Bind the Steam sign-in web view to Steam's hosts | 2 |
| [368-sign-in-cleans-up-after-an-error.md](368-sign-in-cleans-up-after-an-error.md) | #368 | B after an unexpected sign-in error leaves the launcher page stuck | 4 |
| [370-guard-the-remaining-unguarded-paths.md](370-guard-the-remaining-unguarded-paths.md) | #370 | Guard the remaining unguarded paths: SDL consumers, Pan's keeper, async void, toast | 7 |
| [375-close-the-pan-core-test-gaps.md](375-close-the-pan-core-test-gaps.md) | #375 | Close the Pan.Core test gaps the mutation run found | 0 (tests only) |
| [371-cancellable-monitor-polls.md](371-cancellable-monitor-polls.md) | #371 | Cancellation token for monitor polls; a failing or hanging monitor holds no one | 0 (unit tests) |
| [369-show-a-failed-scan-instead-of-an-empty-launcher-tab.md](369-show-a-failed-scan-instead-of-an-empty-launcher-tab.md) | #369 | Show a failed scan instead of an empty launcher tab | 3 |

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
| T242-10 to T242-16 | `tests/Arcadia.App.Tests/PanEmulationTests.cs`, `SteamControllerModeTests.cs` |
| T295-2, T295-3, T295-5, T295-6 | `tests/Arcadia.App.Tests/PanEmulationTests.cs`, `EveryControllerForGamesTests.cs`, `Window/HoldButtonFillTests.cs` (the fill) |
| T297-2 (within T326-4), T297-4; T326-1 to T326-8 | `tests/Arcadia.App.Tests/PanUpdateAtStartTests.cs` |
| T340-1, T340-2, T340-4 | `tests/Arcadia.App.Tests/Window/PanStoppingViewTests.cs`, `PanUpdateAtStartTests.cs` (within T326-2, T326-4, T326-6) |
| T354-1 | `tests/Arcadia.App.Tests/PanUpdateAtStartTests.cs` |
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
| T315-1, T315-3, T315-4 | `tests/Arcadia.App.Tests/SteamDesktopLayoutTests.cs` |
| T335-4, T335-5 | `tests/Arcadia.Pan.Tests/ControllerHidingTests.cs`, `tests/Arcadia.Core.Tests/PanClientTests.cs` |
| T345-1, T345-3, T345-4, T345-7 | `tests/Arcadia.Pan.Tests/CableHidingTests.cs`, `tests/Arcadia.Core.Tests/SteamControllerByCableTests.cs` |
| T357-1, T357-2, T357-4 | `tests/Arcadia.Core.Tests/SteamControllerRoutingTests.cs`, `tests/Arcadia.App.Tests/SteamControllerModeTests.cs`, `SteamControllerXboxPadTests.cs` |
| T238-1, T238-3 to T238-7, T238-12 (fakes) | `tests/Arcadia.App.Tests/EveryControllerForGamesTests.cs`, `tests/Arcadia.Core.Tests/TakenOverControllersTests.cs` |
| T366-1 | `tests/Arcadia.App.Tests/SteamControllerXboxPadTests.cs`, `tests/Arcadia.Core.Tests/ControllerSupportServiceTests.cs` |
| T369-1 to T369-3 | `tests/Arcadia.App.Tests/ScanFailureTests.cs` |
