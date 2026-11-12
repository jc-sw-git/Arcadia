# #517 Cut the per-feature window tests and run the layout guards on main only

No new test cases: removes tests only; the window guards in `tests/Arcadia.App.Tests/Guards/` run on `main` only.

Deleted window tests and the test cases they covered (the acceptance review of I3 checks these cases in the quality
engineer's own run of the app; the files of these issues stay as they are):

| Deleted test class (methods) | Test cases it covered |
|---|---|
| `Window/ArtworkViewTests` (1) | T487-19 |
| `Window/ArtworkWindowTests` (5, incl. "…stay fast") | T487-11, T487-18, T487-20, T487-22 |
| `Window/ButtonSymbolsTests` (7) | T215-1 to T215-6, T315-1 |
| `Window/ChangeArtworkViewTests` (2) | T490-2, T490-5 |
| `Window/ControllerLayoutsViewTests` (2) | T238-13 |
| `Window/DataSourcesViewTests` (3) | T489-1, T489-14, T489-24, T506-65 |
| `Window/DetailPageViewTests` (8) | T11-1, T11-2, T127-1, T127-10, T499-1, T499-2, T499-3, T499-8, T499-16, T499-17, T499-23, T499-25, T499-26 |
| `Window/DriverInstallDialogViewTests` (2) | T413-20, T413-21, T413-23 |
| `Window/EntryTypeViewTests` (5) | T141-4, T141-7 |
| `Window/EpicArtworkWindowTests` (2) | T488-14, T488-15 |
| `Window/FirstRunViewTests` (5) | T139-3, T139-6, T237-6 |
| `Window/GameMenuTests` (4) | T10-1, T10-2 |
| `Window/HeaderTests` (2) | T127-5, T127-6 |
| `Window/HeldKeysTests` (1) | T476-9 |
| `Window/HoldButtonFillTests` (1) | T242-20 |
| `Window/InfoBoxViewTests` (5) | T506-1, T506-2, T506-3, T506-11, T506-14, T506-21, T506-23, T506-27 to T506-33 |
| `Window/InfoBoxWindowTests` (5) | T506-5, T506-13, T506-21, T506-22, T506-26, T506-30, T506-34, T506-35 |
| `Window/KeyboardAndMouseTests` (4) | T8-1, T10-4, T127-7, T127-8 |
| `Window/LauncherControllerModeViewTests` (2) | T178-10 |
| `Window/LauncherPageViewTests` (8) | T137-1, T137-4, T137-8, T477-6 |
| `Window/LauncherViewTests` (4) | T136-2, T136-8 |
| `Window/LibraryViewTests` (2) | T41-1, T138-1 |
| `Window/MouseModeOverlayTests` (2) | T243-36 |
| `Window/OnScreenKeyboardViewTests` (3) | T110-4, T129-7, T477-9 |
| `Window/PanStoppingViewTests` (2) | T340-1, T340-2 |
| `Window/SearchViewTests` (13) | T12-1, T12-4, T12-5, T12-6, T12-10, T12-12, T12-16, T12-18, T110-10, T110-14, T209-2, T237-7, T476-1, T476-6, T476-7 |
| `Window/SettingsViewTests` (10) | T15-1, T15-2, T15-4, T15-6, T15-9, T64-11, T92-3, T92-5, T127-9 |
| `Window/StatusModalViewTests` (3) | T166-12, T166-13, T166-15 |
| `Window/WaitingListSoughtViewTests` (1) | T506-89 |
| `Window/WaitingListViewTests` (5) | T491-9, T491-13, T491-17, T491-20, T506-80, T506-82, T506-88, T506-98, T506-106, T506-116 |
| `Window/WebPageViewTests` (1) | T280-9 |
| `Window/WindowsRestartViewTests` (4) | T382-7, T382-18, T382-19, T382-22, T382-23, T382-25 |
| `ControllerTypesTests.Without_a_usable_SDL3_…` (1) | T72-9 (its toast and log stay covered without a window) |
| `ScanFailureTests.The_window_shows_the_failed_state_…` (1) | T369-1 |

Moved, not deleted: the guards `FocusRingTests`, `RingGeometryTests`, `GridFadeTests`, `SettingsRowsFadeTests`,
`ConsistentTextsViewTests`, `EmbeddedFontTests`, `CoverAndIconTests`, `LibrarySpeedTests`, `SearchSpeedTests` (from
`Window/` and the project root to `Guards/`, 47 cases); the tests of `LibrarySpeedTests` and `SearchSpeedTests` that
open no window moved to `InputLatencyTests` (T229-2) and `KeptSearchKeyboardTests` (T477-2) and still run in PRs.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| `tools/ci/run-tests.sh 4` (PR set: 711 App cases, no window) | 2026-11-12 | Claude | PR branch, Release, Linux cloud container, de_DE | ✅ 1:03 min | – |
| `WINDOW_GUARDS=1 tools/ci/run-tests.sh 4` (main set: 758 App cases) | 2026-11-12 | Claude | PR branch, Release, Linux cloud container, de_DE | ✅ 2:28 min | – |
| `ArcadiaWindow` in a file outside `Guards/` | 2026-11-12 | Claude | PR branch, Linux cloud container | ✅ run fails with the file named | – |
