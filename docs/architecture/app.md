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
  pages disposed; contract on `IPage`).
- Pages are `IPage`s and ask Main through `IPageHost` to close, show a game's menu or the shared filter dialog.
- `LibraryViewModel`: the cover grid of the selected tab, its pages, focus, view and sort, drawn in
  `TileSlotViewModel` places that keep their controls when page or tab change (#229), and `EmptyTabViewModel` for a
  tab without games or whose launcher is gone, or no launcher active (#136). It lies under every page and asks more
  through `ILibraryHost`; whether the focused game's install folder is gone it asks `Services/InstallFolderCheck` off
  the UI thread (#227). It keeps each tab's sorted list until a rescan or the sort changes it.
- `GameDetailViewModel`: detail carousel of `GameDetailPageViewModel`s, `Views/GameDetailView.axaml`; covers in the
  grid are `GameTileViewModel`s (also what the preview panel shows).
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

- `Setup/`: the first start's page (#139): `FirstRunViewModel` with `FirstRunLauncherViewModel` rows, drawn by
  `Views/FirstRunView`.
- `Search/`: `SearchViewModel` (search page, which takes all its input, #209, and its keyboard's `IKeyboardTarget`;
  hits shown in `TileSlotViewModel` places too).
- `Keyboard/`: the one on-screen keyboard (#110): `OnScreenKeyboardViewModel` (drawn by `Views/OnScreenKeyboardView`)
  with `BoardKeyViewModel`, `KeyboardLevels` (shift/AltGr), `LayoutMenuViewModel`, `IKeyboardTarget` (what it types
  into; its user greys out or leaves out keys, `BoardKeyState`) and `WindowInFrontTarget` (the mouse mode's).
- `Settings/`: the settings page (#15):
  - `SettingsPageViewModel` (categories, LB/RB, B), one `SettingsCategoryViewModel` per category; a new category is a
    subclass added in `MainViewModel.ShowSettings`.
  - `LauncherSettingsViewModel` with `LauncherRowViewModel`s and its `ILauncherSettingsHost` (#136), and a page per
    launcher (#137) with the rows in `Settings/LauncherPageRows.cs` (`LauncherPageHeadViewModel`, `LauncherActiveRowViewModel`), its Steam Controller default `LauncherControllerModeRowViewModel` and the
    reset's `SettingsQuestionViewModel` as its `Dialog` (#178); `ControllerSettingsViewModel` (`PanRows`, `SisrRows`, the Steam prompts as
    its `Dialog`); `AboutSettingsViewModel`.
  - Rows (`SettingsRowViewModel`): `ChoiceRowViewModel`, `SwitchRowViewModel`, `ActionRowViewModel`,
    `InfoRowViewModel`, `TextRowViewModel` with `TextEditorViewModel` (a text written with the on-screen keyboard and
    checked while typed). Each has its `Info` as `InfoBlock`s for the info panel, optionally a `Preview` box above it
    and a `Lamp` beside the title.
  - `SettingsChanges` saves at once and reports a failed save.
  - `ControllerLayoutsViewModel`: the modal "Controller zuordnen" (#238), one `ControllerLayoutRowViewModel` per model.
- `ViewModels/TypeCountLine`, drawn by `Views/CountLineView`: the count line per type in the header and on the
  launcher page, with `Controls/LineIcon` (a stroked icon geometry from `Theme.axaml`).
- `Views/TabStrip`: launcher tabs and categories (centred, faded, wheel).

## Controls

`Controls/`: `LauncherLogo` (draws a `LauncherBadge`: logo, picture or letter tile, wherever a launcher is named),
`ButtonTextBlock` (buttons drawn framed inside a text), `KeyHint`/`KeyText` (a hint or label showing the controller's
button or the keyboard's key, after `Input/InputHints`), `PadGlyph` (one controller button in the printing of the
controller in use, `InputHints.Symbols` from `MainViewModel.ButtonSymbols`, #215), `HoldRing`, `RevealPanel` (the
sliding keyboard), `SharedWidthRow` and `CollapsingStack` (the shrinking chips), `CentredDockPanel` (a key's name
centred between icon and button), `ScreenEdgeFill`. `Input/GamepadRouting` takes the controller's buttons to
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
- `Services/InputLatency`: each key or button press timed to the frame that shows it, one `Bild:` line in arcadia.log
  (#229).

## Mouse mode

`Services/MouseModeHost` (wiring, overlay, automation), `Services/ForegroundGame` (also what is in front after a game
ended, #94), `Services/GameWindow` ("Zurück ins Spiel"), `Services/ForegroundWindow` (its bounds and caret),
`Services/ForegroundGuard` (Arcadia's window in front of or behind others), `Services/ForegroundOwner` (Steam's window,
by the programs of the launcher that owns Steam Input, or a library game in front, asked on every poll),
`Services/SystemProcessTable` (the PC's processes for `ProcessGames`),
`Views/MouseModeKeyboardWindow` with `Views/MouseModeKeyboardLegend` (the keyboard outside Arcadia, #110),
`Views/MouseModeOverlay` (own non-activating window, clicks pass outside its region) with `Controls/ScreenEdgeFill`.
