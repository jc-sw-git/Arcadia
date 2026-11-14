# Architecture: `src/Arcadia.App`

The App's classes; the other projects: [README.md](README.md). Avalonia 12 UI (MVVM, CommunityToolkit.Mvvm 8.4
partial properties), `Theme/Theme.axaml`, `Views/MainWindow.axaml`; Avalonia pitfalls: [avalonia.md](avalonia.md).

## Composition

`ArcadiaComposition` builds the object graph (launchers, `ControllerSupportService`, game trackers, `MainViewModel`,
then the controllers' routing and `SteamControllerForGames`) for both `App.axaml.cs` and the acceptance tests'
`ArcadiaDriver`. What only a window or Windows has (timers, `SystemDialogs`, the mouse mode, the Pan pipe, VIIPER)
`App.axaml.cs` makes and passes in, so a change to the wiring belongs there and reaches the tests by itself.

## Routing and pages

- `MainViewModel` routes each press to the topmost layer (first start or settings, else a dialog, else the page on
  top; what a page leaves, ≡ and X, opens the settings or the search), keeps the rules between pages, holds the
  dialogs and wires the parts (#155, #210). It runs one lifecycle for every page (`OnPagesChanged`: top change, closed
  pages disposed; contract on `IPage`). The controller and Pan parts are its own view models, which Main only wires
  and routes to: `InputInUseViewModel` (input kind and controller in use, the hints' printing and the dialogs' ✕,
  #127, #215), `ControllerModeViewModel` (the per-game Steam Controller mode, whether Pan's emulation runs, the way
  to the controller settings; host `IControllerModeHost`, #213, #242) and `PanDialogsViewModel` (Pan's firmware
  notice and update page over everything, #295, #326).
- Pages are `IPage`s and ask Main through `IPageHost` to close, show a game's menu or the shared filter dialog.
- `LibraryViewModel`: the cover grid of the selected tab, its pages, focus, view and sort, drawn in
  `TileSlotViewModel` places that keep their controls when page or tab change (#229), and `EmptyTabViewModel` for a
  tab without games or whose launcher is gone, or no launcher active (#136). It lies under every page and asks more
  through `ILibraryHost`; whether the focused game's install folder is gone it asks `Services/InstallFolderCheck` off
  the UI thread (#227). It keeps each tab's sorted and filtered list until a rescan, the sort or the tab's filter
  changes it. The filter modal (`Filter/`, #44, #466): [app-filter.md](app-filter.md).
- Detail page (carousel, areas, quick selection, "Übersicht anpassen"): [app-detail-page.md](app-detail-page.md).
- `GameActions`: what a game does (start, install, back to it, held A), one per window.
- `LauncherTabsViewModel` the tabs; `StatusBarViewModel` (header), `ToastViewModel`, `GameMenuViewModel` (context menu),
  `ViewOptionsViewModel` ("Ansicht": sort and page size, the search's filter), `ConfirmationViewModel` (asks before
  what can't be undone, focus on "Abbrechen").
- Pan's pages and notices: `PanUpdatePageViewModel` (start page while the installed Pan isn't this Arcadia's, #326),
  `FirmwareAttemptViewModel` (Steam tried a firmware update through the copy, #295), `Input/PanNotices` (a toast in
  front, the overlay notice behind a game), `Input/SteamControllerForGames` (wires the emulation, `IPanEmulationView`).
- Launcher web pages (#83, #280): `Services/WebPages` (one page at a time), `WebPageViewModel` (shown for a sign-in,
  B goes back, or loaded unseen), `Views/WebPageHost` (the system's web view, a fresh one per page).
- `Services/LibrarySession` loads and rescans the launchers, keeps what they delivered, shows each tab only the types
  its launcher's switches allow (#141) and follows installs and running games. It knows no view models and raises
  events (libraries, install and run states, `LibraryNotice`s), which Main turns into tabs, covers and toasts
  (`LibraryToasts`), #228.

## Folders

- `Setup/`: the first start (#139, #497): `FirstRunViewModel` (launcher page, `Views/FirstRunView`), then
  `FirstRunDataSourcesViewModel`, a settings category shown alone as the settings page (`IsFirstRun`); Main's part
  in `MainViewModel.FirstRun.cs`.
- `Search/`: `SearchViewModel` (search page, which takes all its input, #209, and its keyboard's `IKeyboardTarget`;
  hits shown in `TileSlotViewModel` places too).
- `Keyboard/` (#110): `OnScreenKeyboardViewModel` (view `Views/OnScreenKeyboardView`), `BoardKeyViewModel`,
  `KeyboardLevels` (shift/AltGr), `LayoutMenuViewModel`, `IKeyboardTarget` (what it types into; its user greys out or
  leaves out keys, `BoardKeyState`), `WindowInFrontTarget` (the mouse mode's), `SharedKeyboard` (taken in turn, #477).
- `Messages/` (#433): `RecentMessages` (the last 50 toasts and status changes, any thread), `StatusMessageChanges`
  (the header's status messages turned into entries; fed by `StatusBarViewModel`), `ILogFiles`/`LogFiles` (the three
  logs by `LogKind`, never a path). `ToastViewModel.Show` takes a level and records every toast; the group "LOGGING" is
  `Settings/LoggingRows`, and a log opened by controller makes `MouseModeHost` hold the mouse mode until Arcadia is back.
- `Status/` (#166): `StatusModalViewModel` (Y) with `StatusEntryViewModel`s from `StatusBarViewModel.Entries`; Main is
  its `IStatusModalHost` (settings, update). Drawn by `Views/StatusModalView`.
- `Settings/`: the settings page (#15):
  - `SettingsPageViewModel` (categories, LB/RB, B), one `SettingsCategoryViewModel` per category; a new category is a
    subclass added in `MainViewModel.ShowSettings`.
  - `LauncherSettingsViewModel` with `LauncherRowViewModel`s and its `ILauncherSettingsHost` (#136), and a page per
    launcher (#137). It only routes the input and puts the rows together; its parts get what they need through
    `ILauncherCategory`: `LauncherOrder` (on/off, lifting and dragging), `LauncherRowStates` (a list row's state and
    info), and per page group `LauncherAccountRow`, `LauncherGeneralRows` (name, keyword, logo), `LauncherLibraryRows`
    (folder, library switches, rescan) and `LauncherControllerRows` (Steam Controller default, its reset's
    `SettingsQuestionViewModel` as `Dialog` (#178), Steam's desktop layout). The page's own row types are in
    `Settings/LauncherPageRows.cs` (`LauncherPageHeadViewModel`, `LauncherActiveRowViewModel`,
    `LauncherControllerModeRowViewModel`); `ControllerSettingsViewModel` (`PanRows`, `SisrRows`, the Steam prompts as
    its `Dialog`); `AboutSettingsViewModel`.
  - Rows (`SettingsRowViewModel`): `ChoiceRowViewModel`, `SwitchRowViewModel`, `ActionRowViewModel`,
    `InfoRowViewModel`, `TextRowViewModel` with `TextEditorViewModel` (a text written with the on-screen keyboard and
    checked while typed). Each has its `Info` (`InfoBlock`s) for the info panel, below its `ValueBoxes` (#506,
    `Views/ValueBoxesView`) and a `DimmedNote` on top; optionally a `Preview` box above it
    and a `Lamp` beside the title.
  - `SettingsChanges` saves at once and reports a failed save.
  - `ControllerLayoutsViewModel`: the modal "Controller zuordnen" (#238), one `ControllerLayoutRowViewModel` per model.
- `ViewModels/TypeCountLine`, drawn by `Views/CountLineView`: the count line per type in the header and on the
  launcher page, with `Controls/LineIcon` (a stroked icon geometry from `Theme.axaml`).
- `Views/TabStrip`: launcher tabs and categories (centred, wheel, dividers at its widest).

## Controls

`Controls/`: `LauncherLogo` (draws a `LauncherBadge`: logo, picture or letter tile, wherever a launcher is named),
`ButtonTextBlock` (buttons drawn framed inside a text), `KeyHint`/`KeyText` (a hint or label showing the controller's
button or the keyboard's key, after `Input/InputHints`), `PadGlyph` (one controller button in the printing of the
controller in use, `InputHints.Symbols` from `InputInUseViewModel.ButtonSymbols`, #215), `HoldRing`, `RevealPanel` (the
sliding keyboard), `SharedWidthRow` and `CollapsingStack` (the shrinking chips), `CentredDockPanel` (a key's name
centred between icon and button), `ScreenEdgeFill`, `EdgeFade` (#237: the one edge fade of every list or strip that
fades where it is cut off; per place orientation, edges, sizes, colour, condition or `ScrollTarget`, `FocusRoom` with
`ScrollIntoView`; defaults in the style `ctl|EdgeFade` in `Theme.axaml`). `Input/GamepadRouting` takes the controller's buttons to
`MainViewModel`, ⧉/≡ through `SoloButtons`, and tells it the controller in use (`ControllerInUse`).

## Services

- `ArcadiaUpdateService` (Velopack).
- `Services/LauncherLogos`: the user's logos copied as PNG into `%APPDATA%\Arcadia\logos`, the launchers' own as shapes.
- `Services/SystemDialogs`: Windows' file and folder dialogs; opened from the controller they switch the mouse mode on
  (#138).
- `Services/GameTypeTexts`: a type's name, icon key and what A says (#141).
- `Services/AppLogTraceListener`: Core's and the launchers' `Trace` lines into arcadia.log.
- `Services/AppErrors`: the last line of defence: unexpected errors logged with stack trace and shown as a toast;
  `Guard`/`.Forget(context)` for work nobody awaits.
- `Input/UiAction`/`UiActionMap`: keys and buttons as the actions `MainViewModel.Handle` routes.
- `Input/HeldPress`: whether the press being handled repeats a held button or key; where a step wraps round
  (`ListFocus.Cycle`, `ValueCycle.Step`, `GridFocus.Step`, `HitWindow.Turn`/`Move` with `held`), a repeat stops at the end (#476).
  `Input/RepeatGate`: a repeat acts only on the page, dialog and layout (`IPage.Layout`) its press acted on.
- `Services/InputLatency`: each key or button press timed to the frame that shows it, one `Bild:` line in arcadia.log
  (#229).

## Artwork

Covers and backgrounds in the views (#487): [app-artwork.md](app-artwork.md).

## Mouse mode

The App's part of the mouse mode: [app-mouse-mode.md](app-mouse-mode.md).
