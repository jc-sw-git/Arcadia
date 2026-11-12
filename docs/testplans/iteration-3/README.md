# Test plan Iteration 3

Milestone "I3: SteamGridDB integration and more, polished settings". Format and rules: [README](../README.md). The
ticks are set by the owner's full test before the merge.

**Acceptance review:** release candidate –, by –, on –.

One file per issue. A PR that closes an issue adds its file and lists it here; whoever tests an issue reads only
its file.

| File | Issue | Title | Test cases |
|---|---|---|---|
| [166](166-status-messages-modal-with-y.md) | #166 | Open and fix the status messages in a modal with Y | T166-1 to T166-17 |
| [237](237-settings-rows-edge-fade.md) | #237 | Fade out the settings rows where the list can still scroll, with one shared edge fade | T237-1 to T237-8 |
| [288](288-steam-update-shown-that-steam-dropped.md) | #288 | Steam: update shown that Steam dropped seconds later | T288-1 to T288-9 |
| [106](106-epic-cancelled-install-keeps-waiting.md) | #106 | Epic: cancelled install keeps showing "Warte auf Bestätigung" | none (covered by T343-2, T343-14) |
| [343](343-cancelled-steam-install-dialog.md) | #343 | Steam: cancelled install dialog leaves the game on "Bitte in Steam bestätigen" | T343-1 to T343-14 |
| [370](370-guard-the-remaining-unguarded-paths.md) | #370 | Guard the remaining unguarded paths | T370-1 to T370-6 |
| [376](376-ci-hygiene.md) | #376 | CI hygiene: job timeouts, SHA-pinned actions, Dependabot, concurrency, split the App tests | none (CI only) |
| [399](399-show-repeated-error-again-after-quiet-period.md) | #399 | Show a repeated unexpected error again after a quiet period | T399-1 to T399-17 |
| [413](413-show-the-progress-of-pans-driver-installation.md) | #413 | Show the progress of Pan's driver installation | T413-1 to T413-29 |
| [415](415-move-controller-and-pan-block-out-of-mainviewmodel.md) | #415 | Move the controller and Pan block out of MainViewModel | none (refactoring) |
| [418](418-explain-back-pan-install-and-removal.md) | #418 | Explain-back: Pan install and removal | none (doc fixes) |
| [420](420-split-launchersettingsviewmodel-by-concern.md) | #420 | Split LauncherSettingsViewModel by concern | none (refactoring) |
| [422](422-pan-finishes-interrupted-uninstall.md) | #422 | Pan finishes an interrupted uninstall and removes itself without Arcadia | T422-1 to T422-12 |
| [433](433-recent-messages-and-logs-in-about-arcadia.md) | #433 | Show recent toasts and status messages and open the logs in "Über Arcadia" | T433-1 to T433-25 |
| [423](423-restart-notice-at-uninstall.md) | #423 | Restart notice at the uninstall never shows | T423-1; touches T407-1 (both RC) |
| [429](429-close-launcher-confirmation-before-action.md) | #429 | Close the launcher settings' confirmation before its action runs | none (order of closing and confirming; touches T83-7) |
| [434](434-pan-service-test-race.md) | #434 | Pan service test fails when a Pan process exits while it is checked | none (test race, automated test only) |
| [457](457-flaky-windows-pipe-and-countdown-tests.md) | #457 | Pan pipe test T413-25 fails on Windows CI now and then | none (test races in T413-25 and T340-1, automated tests only) |
| [459](459-log-row-test-t433-21-fails-on-windows-ci.md) | #459 | Log-row test T433-21 fails on Windows CI now and then | none (test race in T433-21, automated test only) |
| [465](465-filter-library-by-installed-state-and-type.md) | #465 | Filter the library by installed state and type | T465-1 to T465-14 |
| [382](382-smooth-out-the-controller-setup-after-an-update.md) | #382 | Smooth out the controller setup after an update: one restart row, no wrong hints, no extra puck restarts | T382-1 to T382-34 |
| [470](470-debugger-stops-at-restore-controllers.md) | #470 | Debugger stops Arcadia at "Controller wiederherstellen" while a virtual Xbox pad is plugged in | T470-1, T470-2 |
| [471](471-restore-ends-emulation-despite-declined-prompt.md) | #471 | "Controller wiederherstellen" ends the emulation even when the Windows prompt is declined | T471-1 to T471-4 |
| [024](024-design-polish-font-covers-page-sizes.md) | #24 | Design polish: Figtree font, 2:3 covers, page sizes that fill the grid | T24-1 to T24-16 |
| [476](476-wrap-lists-and-repeat-held-buttons.md) | #476 | Wrap round at the ends of every list and repeat LT/RT and LB/RB while held | T476-1 to -17 |
| [477](477-search-opens-slowly.md) | #477 | Opening the search from the library takes half a second or more | T477-1 to T477-10 |
| [480](480-move-ui-texts-into-resource-files.md) | #480 | Move UI texts into resource files | T480-1 to T480-17 (none for the owner) |
| [339](339-make-ui-texts-consistent.md) | #339 | Make UI texts consistent | T339-1 to T339-19 (owner: T339-18) |
| [487](487-show-real-steam-covers-and-backgrounds.md) | #487 | Show real Steam covers and backgrounds in the library, preview, detail page and search | T487-1 to T487-25 |
| [499](499-detail-page-new-layout.md) | #499 | Detail page: new layout with areas, carousel into depth and info box | T499-1 to T499-31 (owner: T499-30, T499-31) |
| [488](488-show-epics-covers-and-backgrounds.md) | #488 | Show Epic's covers and backgrounds from its catalog | T488-1 to T488-17 (owner: T488-16, -17) |
| [489](489-settings-category-data-sources.md) | #489 | Settings category "Datenquellen": SteamGridDB key, artwork order and search | T489-1 to T489-27, T490-21, -22, -27 to -30, -32 |
| [490](490-change-a-games-artwork.md) | #490 | Change a game's artwork in "Artwork ändern", with own files and images set by hand | T490-1 to T490-35 |
| [491](491-find-artwork-by-name.md) | #491 | Find artwork by name and let the player pick when several games match | T491-1 to T491-29 (owner: T491-28, -29) |
| [507](507-one-steam-image-keeps-epic-covers-away.md) | #507 | One Steam image that doesn't answer keeps all Epic covers away | T507-1 to T507-3 (owner: T507-3) |
| [506-c](506-c-clearer-waiting-list.md) | #506 | Part C: "Warten auf deine Auswahl" in two levels, what is sought, only hits with images, own search term | T506-80 to T506-115 (owner: T506-112 to -115) |

## Release candidate tests (restart or installation)

Cases that need a Windows restart or an Arcadia installation, tested on the release candidate, outside the 30 cases
of the acceptance review.

| Issue | Test cases |
|---|---|
| #422 | T422-4, T422-8, T422-11, T422-1, T422-3, T422-9, T422-12 (in this order, one PC with Pan, the drivers and the puck) |
| #166 | T166-8 (the update from the status modal on the installed release candidate) |
| #423 | T407-1, then T407-2 (uninstall with Ja and UAC accepted; reinstall before restarting); T423-1 with T422-4 |
| #382 | T382-30, then T382-31 (Windows unrestarted in between), T382-32, T382-33, T382-34 (the update end to end) |

## Exploratory test

Charter: –. Time box: –. Findings: –.

## Acceptance tests

| Test cases | Tests |
|---|---|
| T166-1 to -11, -14, -17 | `StatusModalTests`, variants in `StatusModalOrderTests` |
| T166-12, -13, -15 (in part) | `Window/StatusModalViewTests`; T166-13 also `ControllerSupportTests` |
| T433-1, -2, -5 to -21, -23, -24 | `RecentMessagesAndLogsTests` (T433-7 included); T433-19 in `LoggingRowsTests` |
| T476-8 to -12 | `HeldButtonsTests`, `Window/HeldKeysTests`; variants in `GamepadPollsTests`, `ListFocusTests`, `ValueCycleTests`, `HitWindowTests` |
| T476-1 to -4, -6, -7, -14, -16, -17 | `HeldButtonsTests` (T476-16), `SearchWrapTests`, `LibraryTests` (grid, pages, page buttons), `Window/SearchViewTests.More_hits_than_fit_the_row_…_turns_by_two_rows_and_round`, `SteamControllerTests`; variants in `GridFocusTests`, `HitWindowTests`, `GamepadPollsTests` |
| T476-5 | `ListWrapTests`, `FirstRunTests.The_first_starts_rows_go_round_…`, `EveryControllerForGamesTests.Controller_zuordnen_goes_round_…`, `WindowsRestartTests.The_restart_questions_two_buttons_go_round_…`, `LibraryFilterTests.The_filters_values_go_round_…`, `OnScreenKeyboardTests.The_D_pad_walks_the_keys_…_goes_round_at_the_edges_…`, `LibraryFilterTests.Alle_Filter_zuruecksetzen_…`; variants in `ListFocusTests`, `KeyGridTests`, `FullKeyboardTests` |
| T465-1 to -9, -11 to -13 | `LibraryFilterTests`, variants in `GameFilterTests` (Core) |
| T288-1, T288-3, T288-4 | `SteamUpdateTests.While_Arcadia_is_open_a_Steam_update_appears_and_goes_without_a_rescan_or_a_toast_and_a_brief_flag_with_nothing_new_never_shows` |
| T288-2, T288-7 | `SteamUpdateTests.At_the_start_a_real_Steam_update_shows_on_the_cover_and_the_detail_page_and_a_flag_with_nothing_new_doesnt` |
| T288-5 | `SteamUpdateTests.On_the_detail_page_the_update_note_appears_and_goes_while_page_and_focus_stay` |
| T343-1…T343-8 | `tests/Arcadia.App.Tests/CancelledInstallDialogTests.cs` |
| T382-7, -9, -10 | `EmulationSetupTests` (restart row); variants T382-2, -8, -11, -29 in `ControllerSupportRestartTests` (Core), T382-3, -4 in `UnchangedPanTests` (Pan) |
| T382-1, -5, -6, -8, -12 to -17, -20, -21, -26 to -28 | `WindowsRestartTests`; T382-18, -19, -22, -23, -25 in `Window/WindowsRestartViewTests` |
| T477-1, -2, -5, -6, -9 | `SearchSpeedTests`; T477-6 in `Window/LauncherPageViewTests`, T477-9 in `Window/OnScreenKeyboardViewTests` |
| T480-8, -9 | `BrokenTextTests`, `PluralTextsTests`; the fallback T480-1 to -7, -10, -11 in `UiTextTests`, T480-15 in `UiTextsStayInResourcesTests` (Core) |
| T339-1 to -3 | `ResourceTextPlaceholderTests` (Core); T339-4 `ConsistentTextsTests`, T339-12 `WindowsRestartTests`, T339-13 `SteamControllerXboxPadTests`, T339-14, -15 `Window/ConsistentTextsViewTests` |
| T24-1 to -4, -6 to -13 | `PagingTests`, `SettingsServiceTests`, `LibraryTests`, `GridFadeTests`, `LibrarySpeedTests`, `Window/CoverAndIconTests`, `Window/EmbeddedFontTests` |
| T499-1 to -8, -10 to -26 | `DetailAreasTests`, `DetailPageStateTests`, `DetailPageTests` (T499-3), `Window/DetailPageViewTests` |
| T487-1, -4 | `ArtworkTests` (also T487-5 and -17 through a rescan) |
| T487-11, -18, -20, -22 | `Window/ArtworkWindowTests` |
| T487-19 | `Window/ArtworkViewTests`; the variants T487-2, -3, -5 to -10, -12 to -17 in `ArtworkServiceTests`, `ArtworkCacheTests`, `SteamArtworkTests`, `ArtworkFormatTests`, `ImageCheckTests` (Core) |
| T488-1, -8, -13 | `EpicArtworkTests` |
| T488-14, -15 | `Window/EpicArtworkWindowTests`; the variants T488-2 to -7, -9 to -12 in `EpicArtworkTests` (Core) |
| T489-1, -2, -12, -13, -16, -19, T490-21, -22, -27, -29 | `DataSourcesTests` (#490's cases in `DataSourcesTests.ArtworkChanges`); T489-1 (layout) and -24 in `Window/DataSourcesViewTests`; the variants in `SteamGridDbKeyTests`, `SteamGridDbClientTests`, `ArtworkServiceSteamGridDbTests` (Core) |
| T490-1, -3, -4, -6, -8, -10, -12 to -15, -23, -24, -27, -31 | `ChangeArtworkTests`; the variants T490-7, -9, -11, -16 to -20, -25, -26 in `ArtworkByHandTests` (Core) |
| T490-2, -5 | `Window/ChangeArtworkViewTests` |
| T491-1, -3, -9 to -12, -14 to -18, -21, -22, -26 | `FindArtworkByNameAcceptanceTests` (T491-1, -3, -10, -11, -22), `WaitingListTests`; T491-9, -13, -17, -20 in `Window/WaitingListViewTests`; the variants in `ArtworkServiceByNameTests`, `SteamGridDbNameTests`, `EpicArtworkTests` (Core) |
