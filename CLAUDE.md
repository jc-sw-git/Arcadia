# Arcadia — working notes for Claude

Arcadia is a game launcher that unites all other launchers: one tab per launcher, Big Picture
mode fully controllable by gamepad (incl. Steam Controller), desktop mode by mouse/keyboard.
The repo was formerly "SteamButler" (Rust); that code lives in `legacy/steambutler-rs/` as a
reference for porting (binary VDF codec, scanner ideas, architecture notes).

- **Designs (source of truth for UI):** https://claude.ai/artifact/8A9qxc67RcsuFmavaqCAuN
  (artboards: library, detail page, search, artwork modal, Spotify, launcher settings,
  localization, appearance, Big Picture settings, desktop mode). Read it before building UI.
- **Open work:** GitHub issues (label `roadmap`). Roadmap #26 → one epic per area (`type:epic`)
  → tasks, all linked as GitHub sub-issues. Add new issues as a sub-issue of the matching epic
  (`gh api -X POST repos/jc-sw-git/Arcadia/issues/<epic>/sub_issues -F sub_issue_id=<issue id, not number>`).
  Features are `type:story` and start with a `> **Story:**` line. See `docs/roadmap.md`.
- **Refinement first:** before implementing an issue (Iteration 1) or an epic (from Iteration 2), refine
  it with the owner using the skill `.claude/skills/refinement`; rules in `docs/roadmap.md` → Refinement.
- **Language:** the owner writes German; UI texts are German for now (localization is an issue).
  Code, comments and commit messages are English.

## Build, test, run

```bash
dotnet build Arcadia.slnx
dotnet test Arcadia.slnx
dotnet run --project src/Arcadia.App -- --demo        # demo libraries for all launchers
```

- .NET 10 SDK. `Directory.Build.props` sets `TreatWarningsAsErrors` — keep the build warning-free.
- A running Arcadia locks `bin/` → close it before building: `Get-Process Arcadia | % { $_.CloseMainWindow() }`
  (CloseMainWindow, not Stop-Process: Arcadia must shut SISR down cleanly).
- Logs: `%LOCALAPPDATA%\Arcadia\logs\arcadia.log` (keys, gamepad buttons, SISR start/stop),
  `sisr.log` (SISR's own log, overwritten per start). Settings: `%APPDATA%\Arcadia\settings.json`.
- Crashes before the first log line (e.g. XAML/style errors) only show in the Windows event log:
  `Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=(Get-Date).AddMinutes(-5)} | ? ProviderName -match '.NET Runtime'`.

## Releases

- Windows x64 only for now. Installer and self-update with **Velopack**: `tools/release/Pack.ps1 -Version x.y.z`
  publishes self-contained and runs `vpk pack` → `artifacts/releases/Arcadia-win-Setup.exe` + update feed.
  Installs without admin rights to `%LOCALAPPDATA%\Arcadia\current\Arcadia.exe` (the logs folder next to it
  survives updates). The `Velopack` package and the `vpk` tool (`.config/dotnet-tools.json`) share one version.
- `VelopackApp.Build()…Run()` must stay the first thing in `Program.Main`. Uninstalling asks (20 s, default
  no — Velopack kills the hook after 30 s) whether SISR, VIIPER and usbip-win2 go too (`ControllerSupportRemoval`).
- `ArcadiaUpdateService` checks GitHub Releases (stable only) every 6 h and on "Nach Updates suchen" in
  Einstellungen → "Über Arcadia". "Jetzt aktualisieren" shows only with an update; it downloads, Arcadia closes
  and Velopack installs silently (no input needed) and restarts it. Updating while a game runs is fine: SISR
  keeps running across the restart (the new Arcadia adopts it), pauses for running games are handed over with
  `--sisr-paused-for <dir>`, games started from Arcadia with `--started-game <key>` (their end brings Arcadia back, #94),
  and `--background` brings the window back behind the game instead of in front.
  "Was ist neu" lists the bullets of every release body between the running and the newest version
  (`Changelog`). Push a tag `vX.Y.Z` to release (`release.yml`).
- Every CI run on Windows packs the installer; only pushes to `main` upload it as an artifact (1 day). The account's
  artifact storage is small and counted per hour over the billing month, so deleting doesn't free it at once:
  `prune-artifacts.yml` keeps only the newest 5 (after each CI run, daily, or by hand), and a failed upload only
  warns, it doesn't fail the CI (#115).
- Versioning (owner's rule): SemVer, 0.x until the first full release 1.0. Major may break; minor adds only
  non-breaking features; patch only fixes bugs — no features, no intended behaviour change (security aside).
  This matters because third-party plugins are planned.

## Verifying UI changes

Tests cover Core/Launchers/ControllerSupport logic, not the UI. For UI changes, run the app and
look at it — see the skill `.claude/skills/arcadia-verify/SKILL.md` and the scripts in
`tools/dev/` (screenshot with key presses, foreground tracing, XInput probe, Steam shortcuts).
The owner often tests manually at the same time — say before driving the app with keys.

For bugs that aren't obvious from the code, or a fix that didn't hold, use the skill
`.claude/skills/diagnose` (a check that goes red on the bug comes before any hypothesis).

## Definition of Done

A change (PR) is done when all of this holds. Every feature iteration ends with a code review
issue (e.g. #66) that checks the whole milestone again, then a manual test on the release
candidate; this list keeps both small. Larger review findings become `type:tech-debt` issues at
the start of the next feature iteration. Process and test plan format: `docs/testplans/README.md`.

- **Builds and tests:** `dotnet build` without warnings, `dotnet test` green, CI green on all platforms.
  New logic in Core/Launchers/Input/ControllerSupport comes with tests, error paths included.
- **Robust:** no exception can end Arcadia. No unguarded `async void` or fire-and-forget task;
  unexpected errors are logged with stack trace to `arcadia.log` and shown as a toast.
  Network and process waits have a timeout.
- **Secure:** no user- or file-supplied string goes unchecked into a process start, path or URI;
  downloads are verified; no secrets or personal data in logs; admin rights only where unavoidable.
- **Controller first:** every new flow works with a gamepad alone (no mouse/keyboard), B goes back.
  Anything that can't (e.g. UAC) is named in the PR.
- **Readable:** names say what things are; methods do one thing; comments explain *why*, not *what*.
  A developer new to the code understands the change from the diff and `CLAUDE.md`.
- **Modular:** launcher-specific code stays in its launcher folder; UI and Core don't special-case launchers.
  A launcher is one `ILauncherIntegration`, registered in `BuiltInLaunchers`; built-in launchers use only
  `Arcadia.Launchers.Abstractions`, as a plugin would.
- **Themeable:** no colour, brush or shadow values in views or styles; they are resources in
  `Theme/Theme.axaml`, so a theme (later possibly a plugin) can replace them all in one place.
- **Verified for real:** UI, input, launcher and Steam Controller changes were run and looked at
  (`.claude/skills/arcadia-verify`); what was only checked mechanically is said so.
- **Docs current:** `CLAUDE.md` / `docs/` updated when a fact, rule or workflow changed.
- **Test plan:** a PR that closes an issue adds that issue's manual tests to
  `docs/testplans/iteration-<n>.md` (IDs `T<issue>-<n>`, each marked `E2E: ja/nein`) and records
  the test runs done during implementation with their real environment. If nothing new needs a
  manual test (e.g. a refactoring of features already covered by tests), the issue's section
  says so in one line with the reason. The `Testplan` CI check fails without the section.
- **Performant:** no blocking work on the UI thread; code on a hot path (start, scan, library,
  input, running in the background beside a game) stays within the budgets below, measured when touched.

Budgets (Release build, owner's PC): start to usable library < 2 s with 1,000 games; gamepad
input to visible focus move < 50 ms; idle CPU < 1 % with gamepad polling; < 400 MB RAM with
5,000 games; a rescan never blocks the UI; no measurable cost to a running game.

**When criteria conflict**, the higher one wins: 1. security and privacy, 2. robustness and data
safety, 3. controller first, 4. performance, 5. readability and maintainability, 6. modularity and
scalability. Performance beats readability only when a measurement breaks a budget. A conflict
decided on purpose is named in the PR (and in the review issue).

**Iterations:** feature iteration N (milestone "Iteration N": issues → code review incl. a
retrospective of the processes (keep, change or drop each) → manual test → release), then acceptance iteration N (milestone "Abnahme Iteration N": every `E2E: ja`
test case of N becomes an automated acceptance test in CI). Feature iteration N+1 starts only
when acceptance iteration N is closed, so a change in N+1 that breaks a feature of N shows at once.

## Layout

| Project | Content |
|---|---|
| `src/Arcadia.Launchers.Abstractions` | What a launcher is made of, with no dependency on the rest: `ILauncherIntegration` (scanner, optional client, install monitor and running games, the launcher's `ProgramNames`, `Locate`, optional `FolderChoice`), `ILauncherFolderChoice` (a folder the user picks when the launcher isn't found, checked when picked, #138), `ILauncherScanner`, `ILauncherClient`, `IInstallMonitor`, `IRunningGamesMonitor`, models (`Game`, `LauncherInfo` with its `Logo` path, `LaunchTarget`). The future plugin interface for launchers |
| `src/Arcadia.Core` | `LauncherRegistry` (the registered launchers; reads see only the active ones in tab order, `Locate` finds them off the UI thread), `LauncherSelection` (active and inactive launchers from `AppSettings.ActiveLaunchers`, #136), `KnownLaunchers`, `LauncherNames` (rules for a launcher's own name and keyword, #137), `LogoFile` (a picked logo checked by its content; an SVG may not load or run anything, #138), `GameSorter`, `PageSizes`/`PageWindow`, `Carousel` (detail page neighbours), `SearchQuery` (prefix parser), `TextWithCaret` (the search field's text and caret), `GameSearch` (hits by relevance or a sort order; titles folded and sorted once, so a key press is one ordinal pass), `LauncherScope` (chips and prefix in step), `HitWindow` (the hits on screen, turned with LT/RT), `KeyboardLayout` (the on-screen keyboard's real layouts, three levels per key), `KeyGrid` (focus over its keys), `FullKeyboard` (a layout as the real keyboard's keys, ISO or ANSI), `KeyboardPlacement` (the mouse mode keyboard's edge and size on a monitor), `AppSettings`/`SettingsStore`/`SettingsService` (the one shared copy of the settings; `LauncherPreferences` per launcher: name, keyword, logo file, folder), `LibraryService`/`LibraryRefresher` (scan and rescan; every active launcher keeps a library, also empty or not found, `ScannedAt`), `InstallTracker` (installs started from Arcadia or in the launcher), `RunningGamesTracker` (which games run and which of them Arcadia started, #94/#95; off the UI thread, every 2 s also in the background), `ProcessGames` (running games by their processes, only for launchers without `IRunningGamesMonitor`; each round looks only at new processes), `Changelog` (release notes between versions), `GameFolders` (install folders of the library, to tell a game in front; paths compared in one spelling), `LaunchedGames` (a game Arcadia started whose path can't be read), `MouseModeSettings`, `ReturnWithViewSettings`, `ListFocus` (focus in a column of rows that come and go, tabs that wrap) |
| `src/Arcadia.Launchers` | `BuiltInLaunchers` (the one place a launcher is registered), integrations (`SteamIntegration` with `SteamFolderChoice`, `EpicIntegration`, `DemoIntegration`), Epic scanner, catalog, log reader, client and install monitor, `DirectLauncherClient`, `GameLaunchService`, `ProcessWatch` (also `PathOf(pid)`, the foreground process and the process ids), `SystemProcessTable`, running games (`SteamRunningGames`, `EpicRunningGames`), Steam (`SteamScanner`, `SteamInstallMonitor`, `SteamContentLog`, `SteamLoginLog`, `TextVdf`, `BinaryVdf`, `SteamShortcuts`, `SteamInstallation`) |
| `src/Arcadia.Input` | `XInputGamepadSource` (polls 4 slots, presses and releases, auto-repeat for D-pad and sticks), `HoldGesture` (tap vs. hold), `RepeatGesture` (held erase), `LatchGesture` (shift: tap or hold), `SoloButtons` (View/Menu act alone, not together), `ButtonText` (the buttons in a text), `KeyboardButton` (the buttons while the mouse mode's keyboard is open); mouse mode (#64): `MouseMode` (Back + Start, `Polled` readings, also in the background), `MouseModeMapping` (buttons → mouse/keys), `MouseModeAutomation`, `StickCurve`, `SendInputOutput` (`IMouseKeyboardOutput`), `ReturnWithView` (View held brings Arcadia back), `ControllerActivity`/`InjectedInputGate`/`SteamDesktopInputFilter` (drop Steam's desktop layout) |
| `src/Arcadia.ControllerSupport` | Steam Controller Support: `SisrHost` (`ISisrHost`), `SisrApiClient`, `UsbipDriver`, `UsbipPorts`, `GitHubReleases`, `ControllerSupportService` (`SisrState`, `HealthIssues.WorstProblem`), `ControllerSupportPlatform` (Windows/Steam, replaced in tests), `ControllerSupportRemoval` (uninstall) |
| `src/Arcadia.App` | Avalonia 12 UI (MVVM, CommunityToolkit.Mvvm 8.4 partial properties), `Theme/Theme.axaml`, `Views/MainWindow.axaml`, `ArcadiaUpdateService` (Velopack), `Services/LauncherLogos` (the user's logos copied as PNG into `%APPDATA%\Arcadia\logos`, the launchers' own as shapes) with `LauncherBadge` (logo, picture or letter tile) drawn by `Controls/LauncherLogo` wherever a launcher is named, `Services/SystemDialogs` (Windows' file and folder dialogs; opened from the controller they switch the mouse mode on, #138). `MainViewModel` holds navigation and wires the parts; `StatusBarViewModel` (header), `ToastViewModel`, `GameMenuViewModel` (context menu) and `GameDetailViewModel` (detail carousel, `Views/GameDetailView.axaml`) and `EmptyTabViewModel` (a tab without games or whose launcher is gone, or no launcher active, #136) are its children; `Search/` holds `SearchViewModel` (search page and its keyboard's `IKeyboardTarget`; hits shown in `HitSlotViewModel` places that keep their controls when the hit changes); `Keyboard/` holds the one on-screen keyboard (#110): `OnScreenKeyboardViewModel` (drawn by `Views/OnScreenKeyboardView`) with `BoardKeyViewModel`, `KeyboardLevels` (shift/AltGr), `LayoutMenuViewModel`, `IKeyboardTarget` (what it types into; its user greys out or leaves out keys, `BoardKeyState`) and `WindowInFrontTarget` (the mouse mode's); `Settings/` holds the settings page (#15): `SettingsPageViewModel` (categories, LB/RB, B), one `SettingsCategoryViewModel` per category (`LauncherSettingsViewModel` with `LauncherRowViewModel`s and its `ILauncherSettingsHost`, #136, and a page per launcher with `LauncherPageRows`, #137; `ControllerSettingsViewModel` with SISR's prompts as its `Dialog`, `AboutSettingsViewModel`; a new category is a subclass added in `MainViewModel.ShowSettings`), rows (`SettingsRowViewModel`: `ChoiceRowViewModel`, `SwitchRowViewModel`, `ActionRowViewModel`, `InfoRowViewModel`, `TextRowViewModel` with `TextEditorViewModel`, a text written with the on-screen keyboard and checked while typed; each with its `Info` as `InfoBlock`s for the info panel, optionally a `Preview` box above it and a `Lamp` beside the title), `SettingsChanges` (saves at once, reports a failed save); `Views/TabStrip` (launcher tabs and categories: centred, faded, wheel); `Controls/` holds `ButtonTextBlock` (buttons drawn framed inside a text), `KeyHint`/`KeyText` (a hint or label showing the controller's button or the keyboard's key, after `Input/InputHints`), `HoldRing`, `RevealPanel` (the sliding keyboard), `SharedWidthRow` and `CollapsingStack` (the shrinking chips), `CentredDockPanel` (a key's name centred between icon and button); mouse mode: `Services/MouseModeHost` (wiring, overlay, automation), `Services/ForegroundGame` (also what is in front after a game ended, #94), `Services/GameWindow` ("Zurück ins Spiel"), `Services/ForegroundWindow` (its bounds and caret), `Views/MouseModeKeyboardWindow` with `Views/MouseModeKeyboardLegend` (the keyboard outside Arcadia, #110), `Views/MouseModeOverlay` (own non-activating window, clicks pass outside its region) with `Controls/ScreenEdgeFill` |
| `tests/Arcadia.Core.Tests` | xUnit |

Big Picture UI is laid out at 1920×1080 inside a `Viewbox`; a window of another shape gets bars in the window's
background beside or above and below it. Bands that run from edge to edge (footer line, the search's legend and
keyboard, dialog dimming, the search's background) reach into those bars with the `bleed` classes in `Theme.axaml`,
so the `Viewbox` doesn't clip. Focus/navigation is done in the
view models (`MainViewModel.Handle(UiAction)`), not with Avalonia focus: buttons are
`Focusable=False`, keys are handled in a tunnelling `KeyDown` handler.

## Rules that are easy to get wrong

**Product decisions** (agreed with the owner; don't change without asking):
- Page sizes 10/25/50/100, default 25. A size is selectable if the library has *more* games
  than the next smaller size (`PageSizes.IsAllowed`).
- Sorting: name / install date / last played, each ascending or descending; games without a
  date always last.
- Sort order and preferred page size are global, not per tab, and are saved; Arcadia opens in the
  tab the user last chose, always on page 1 with the first game focused (owner, 2026-10-17).
- One "Ansicht" modal (Y) for sort + page size. X = search (all launchers, on-screen keyboard,
  `/steam-epic term` prefix, keyword per launcher), R3 = keyboard layout switch, View/⧉ or
  right click = context menu, Menu/☰ = settings (swapped by the owner, 2026-10-17). The context menu stands beside the cover
  after a right click on it, over the preview's "Optionen" button after ⧉, R or a click on that button (owner, 2026-10-21). No "all launchers" tab. No dedicated favourite button.
- Dependent options sit in the same box as their main switch; on the settings page in the same group right below it,
  indented one step per level (`SettingsRowViewModel.Indent`), greyed out while it is off, keeping their values. Each
  group of the settings page is a box of its own (owner, 2026-10-22).
- UI texts name a controller button by the symbol on it, not by its name: ⧉ and ≡, not View/Back and Menu/Start
  (Xbox symbols for now; later those of the controller in use, #72), framed like in the legend also inside a text
  (`ButtonTextBlock`; in prose other buttons are written in brackets, e.g. "[L3]", "[LS]") (owner, 2026-10-20).
- Every button hint follows the input in use (#127, owner 2026-10-21): after a controller button the controller's
  buttons, after a key or a click the keyboard's keys and the mouse (`KeyHint`, `KeyText`; the mouse merely moving
  changes nothing). Controller buttons are round or a pill, keys square with rounded corners, both with a darker
  lower edge. An element no key reaches shows only its icon. Search and settings are chips in the header (key +
  symbol), so the footer doesn't list them.
- The keyboard plays like a game, one hand on WASD (owner, 2026-10-21): WASD move, Space = A (held: details),
  Esc = B, F = X, V = Y, Q/E = LT/RT, Shift+Q/E = LB/RB, 1 … 0 = launcher tab 1–10, Enter = ≡, R = ⧉. Arrows,
  Page Up/Down, Ctrl+Tab, F2, the menu key and Shift+F10 stay as unadvertised alternatives. In the search a real
  keyboard types, so only keys without text act there (arrows, Enter = ≡ starts the hit, Esc, Page Up/Down).
- The header's tab strip sits in the middle between logo and status, as wide as its tabs need, LB/RB right beside
  them. At its widest it keeps 40 px to both, with a divider in each gap; the selected tab stands in its middle and
  it fades out where more tabs follow. Dividers also stand between a status message and the search chip and
  between the settings chip and the date (owner, 2026-10-21).
- Settings (#15, owner 2026-10-21 and 2026-10-22): a whole page in place of the library (≡, Enter, the chip); B or ✕ go
  back to the same place. Categories are tabs in the header like the launcher tabs (LB/RB, Q/E without Shift for now, a
  click), only those with content; a warning or an error of a category shows its symbol beside the name, info nothing.
  Pattern "Leiste" for every category: one column of rows, ▲ ▼ (W/S) move, ◀ ▶ (A/D) change switches and choices at
  once, A (Space) starts actions; only settings and actions are rows, facts about a setting (e.g. SISR's status,
  version, drawbacks) are boxes (`InfoSection`) below its description in the info panel. The info panel beside the
  rows explains the focused row, RS (Shift+W/S, the wheel) scrolls it, fades show more; the mouse over a row focuses
  it. A row that can't change now (e.g. while SISR starts) still takes the focus. The footer names the focused row's buttons. Applies at once, no save button. Questions
  (e.g. "Steam beenden?") stay dialogs over the page; the header's status symbols open their category.
- Launchers (#136, owner 2026-10-22): the settings' first category "Launcher" lists "Aktiv · erscheinen als Tabs" in tab
  order (key 1 … 0), then "Nicht aktiv" (installed first, then not found, greyed out). ◀ ▶ switch "Aktiv": off means not
  scanned, no tab, no search hits, not watched; on goes to the end of the active ones, off to the top of the inactive
  ones; a click anywhere on the switch's box switches it too (a click elsewhere on the row opens its page). Not found can't be switched on; active and no longer found can be
  switched off. Y lifts a row, ▲ ▼ move it,
  Y drops it (under "Nicht aktiv": off), B cancels; the mouse drags. X reads all again; A opens the launcher's page
  (#137, below). Saved as `ActiveLaunchers` (order = tabs); without it every launcher found is active. An active launcher
  without games or no longer found keeps its tab with an empty state ("Keine Spiele in …" / "… nicht gefunden",
  "Launcher-Einstellungen" and "Neu einlesen" / "Erneut suchen"); with no launcher active the library says "Kein Launcher
  aktiv" the same way. Beside the rows two boxes: "Tab-Leiste" (the tabs as they will be) and "Launcher-Details" (head,
  warnings, library, location; no repeat of what row and preview show). "Steam Controller Kompatibilität" has a lamp
  beside its name while on: green while SISR runs. A status message row shows one short line below its title
  (`HealthIssue.Summary`), the whole text in the info panel; descriptions below a row name stay at one, at most two
  lines. A row's action is a pill as wide as the value chips, filled with the accent while focused (owner, 2026-10-22).
- Launcher page (#137, #138, owner 2026-10-23): A or a click on a launcher in the list opens its page in place of the rows;
  B goes back to the list with that launcher focused, LT/RT (Shift+Q/E, a click on a neighbour in the head) turn to the
  launcher before or after it in the list's order, wrapping. Groups "Allgemein": Aktiv, "Name im Tab", "Such-Kürzel",
  "Logo"; "Bibliothek": "Ordner" and "Neu einlesen" (only active and found). Name and keyword are written with the shared
  on-screen keyboard below the page (without ↹ and the edge key; the search's buttons, ≡/Enter take, B/Esc drop); a
  problem (keyword taken, "-", too long) shows in red while typing, taken anyway the old value stays and a toast says
  why; empty means the launcher's own again. Own logo: ▶ or A open the file dialog (PNG, SVG, ICO, WebP up to 2 MB, copied),
  ◀ the launcher's own logo; in the tab it is fitted into 24 × 24 px. "Ordner" is only a fallback when Arcadia can't find
  the launcher (no choice between automatic and own): the folder is checked when picked and taken only if the launcher
  is there, then it is switched on and read. The mouse mode's "Bei System-Dialogfenstern einschalten" (under "Mausmodus",
  for "Von Hand" and "Automatisch", greyed out with "Aus") switches it on while a dialog opened from the controller is open.
- Dialogs (Ansicht, settings' prompts) show an ✕ in the top right corner while mouse or keyboard is in use
  (`MainViewModel.ShowsCloseButtons`); the controller closes them with B. A dialog that only informs has
  no "Schließen" button besides (owner, 2026-10-20).
- A starts a game on release; holding A for 1 s opens the detail page (a ring around the preview fills from
  0.2 s on, letting go before 1 s cancels). With the mouse a click picks a cover, a double click starts it and
  holding the left button 1 s opens the details with the same ring (owner, 2026-10-19). The context menu and the detail page offer the same actions
  (`GameActionViewModel.For`); the detail page has no menu, its actions are buttons (owner, 2026-10-17) in three rows:
  Spielen, Zu Favoriten, In … öffnen, Artwork ändern; then the game's own settings (Steam Controller mode,
  "Individueller Mausmodus", later e.g. launch options); last "Deinstallieren …" alone (owner, 2026-10-21).
- The detail page is a carousel through the tab's games in the library's sort order, across pages and
  wrapping at the ends (two games: the other on both sides; one: no neighbours). LT/RT turn it, LB/RB switch
  the tab and stay on the detail page (first game), Y re-sorts with the shown game kept in the middle, B returns
  to the library page of the game shown last (owner, 2026-10-17). X opens the search over it; B in the search
  returns to it (#127). Below the buttons only "Spielen" has a note (update, installation); a disabled button tells
  why in its tooltip, since the focus skips it (owner, 2026-10-21).
- Search (#12): opened with X the on-screen keyboard is out, the hits sit in one row above it (sliding sideways,
  faded neighbours at the edges, LT/RT buttons beside them, "RS Treffer wählen" above); opened with F or a click,
  or after B, a real key or the tab on the keyboard's top edge, it slides in and the hits fill a grid that scrolls
  up and down (faded rows at the top and bottom) and the D-pad and A act on the hits as on covers; X brings the
  keyboard back. The legend at the bottom is always there and names the input in use: the controller's buttons,
  or mouse and keyboard (it never hides, so the keyboard above it doesn't move under a click). B closes the layout menu first, then
  puts the keyboard away, then closes the search; B never erases. D-pad/left stick type, the right stick picks a
  hit, LT/RT (or Page Up/Down, the wheel) turn the hits a window at a time, as LT/RT turn pages and the detail page;
  the row wraps round like the detail page, the grid stops at its ends like the library and the wheel. A scroll bar
  shows where the shown hits are: across below the row, down beside the grid (owner, 2026-10-19). The keyboard is the
  shared one without ↹ and the edge key; ↵ starts the focused hit like ≡, the arrows move the field's caret (up: to
  the start, down: to the end), "Ausblenden" puts it away like B (owner, 2026-10-22). X erases (held: repeats), Y space, LB shift and RB AltGr (each: tap on/off, held only while held; one at a time), L3 "Filter und Sortierung" (launchers, any number, and
  the library's sort orders plus relevance, the default; not saved), R3 layout menu (all five, the last chosen
  saved globally). ≡ starts the focused hit, ≡ held 1 s opens its details, ⧉ its menu; with a real keyboard
  Enter and Enter held do the same. The sort button sits in the chip row; with many launchers the chips shrink
  (keyword first, then the name, last only the icon) and a divider appears before the sort button. No hits and
  nothing typed show an empty state the theme draws (`ContentControl.searchEmpty`). ™ ® © count for nothing in
  the search. The `/launcher` prefix still works when typed but isn't advertised; chips and filter rewrite a typed
  prefix and never add one. A detail page opened from the search turns through the hits in their order; LB/RB
  and Y do nothing there, X returns to the search with its text unchanged, and B, as on every detail page, goes to
  the library: the tab of the game's launcher, its page, the game focused; the search closes (owner, 2026-10-17,
  2026-10-18 and 2026-10-21).
- On-screen keyboard (#129, #110): one design for every keyboard Arcadia shows, in the search, over other windows in the
  mouse mode, later in settings or plugins (owner, 2026-10-22). A 1:1 replica of the chosen real keyboard, like Steam's:
  the keys where the real one has them (DE, UK, FR, ES ISO with the tall Enter, US ANSI), ⌫ ↹ ↵ ⇧ space and AltGr
  with symbol and name (the name centred in the key, symbol or icon at its left edge, the button at its right), caps lock a blank key the focus skips, no Ctrl/Alt/Win; a right block with "Layout: DE" (R3),
  "Nach oben/unten" (⧉), "Ausblenden" (B) and the arrows. Whoever shows it decides what it types into and may grey
  out or leave out keys at any time (`IKeyboardTarget`, `Configure`); a left-out key leaves its place empty. With
  the controller the D-pad (left stick) moves over the keys, passing over empty, greyed-out and left-out ones: left/right
  at a row's last key go on to the nearest key wholly beside it in another row (² on AltGr: left to ↹), up/down to the
  nearest key above or below, so a far key in the next row doesn't win over one right below in the row after (↹ on
  AltGr: down to ⇧, not ↵); the tall Enter is one key in both rows (owner, 2026-10-22); in the mouse mode the buttons are the mouse mode's
  (below). On the
  normal level a key shows its character large, the shift one small above (left out when it is just the capital
  letter) and the AltGr one small at the bottom right; with shift or AltGr on, only that level's characters, keys
  without one stay empty and the focus passes over them. Dead keys type their own character. A layout without
  AltGr (US) greys RB out. The gamepad buttons stay Arcadia's, not Steam's (owner, 2026-10-21).
- Mouse mode (#64): Back + Start held (1–9 s, default 3) switches it, also with Arcadia in the background; one mode for all
  pads; never saved (Arcadia starts normal). Its buttons are listed in `MouseModeRows.HelpRows`; in it
  Arcadia ignores gamepad navigation. View and Menu therefore act on release, or once held alone 0.2 s (so ≡ held still
  opens details), and not at all when pressed together (owner, 2026-10-19). "Mausmodus" is one choice of three, so only one way
  decides (owner, 2026-10-20): "Aus" (all off, the other values kept), "Von Hand" (Back + Start; "Mausmodus beenden, wenn Arcadia im Vordergrund ist"
  optionally ends it when Arcadia comes to the front) or "Automatisch" (on when Arcadia loses the focus to a non-game, off when Arcadia or a game comes to
  the front; Back + Start overrides it until Arcadia was in front again, e.g. for a game Arcadia doesn't know).
  "Beenden des Mausmodus in Spielen erzwingen" (only "Von Hand"): "Nein", "Ja" (in a library game in front Back + Start
  doesn't switch it on and a game coming to the front ends it) or "Individuell" (a default "Erlaubt"/"Deaktiviert", and
  "Individueller Mausmodus" in each game's preview and detail page: "Standard (erlaubt)" or "Standard (deaktiviert)",
  naming the default, then "Erlaubt", "Deaktiviert") (owner, 2026-10-20 and 2026-10-21). "Zurück zu Arcadia" is its own setting: View held alone (1–9 s, default 1; no "at once", since a tap of ⧉ moves the mouse mode's keyboard, #110) brings
  Arcadia back from the background, with or without the mouse mode, with a box whose frame fills (owner, 2026-10-20).
  Exceptions per process are planned for later. Mouse mode's on-screen keyboard (#110): Y opens it in front of other
  windows (own topmost window that never takes the focus and goes back on top whenever another window comes to the
  front; the Start menu and Windows' other shell flyouts still cover it, only signed programs under Program Files get above them; characters go out as Unicode with `SendInput`, ⌫ ↹ ↵ and the
  arrows as keys), B hides it (layout menu first), the mouse mode goes on. While it is open: D-pad focus, A types,
  X erases (held: repeats), Y space, LB shift (also on ↹ ↵ and the arrows) and RB AltGr as in the search, ≡ Enter,
  R3 layout menu, ⧉ tapped moves it to the other screen edge (saved), ⧉ held = "Zurück zu Arcadia" and ⧉ + ≡ as
  always, both close it; triggers and sticks stay the mouse. Laid out like the real keyboard (DE, UK, FR, ES ISO
  with the tall Enter, US ANSI), caps lock a blank key the focus skips, no Ctrl/Alt/Win; right block: layout,
  edge, hide, arrows. On the foreground window's monitor, centred at the last edge unless the caret Windows knows
  would lie under it; scales like the Big Picture layout. Shares the layout last chosen with the search. Closes
  when a game comes to the front or the mouse mode ends. With Arcadia's search in front, Y doesn't open a second
  keyboard: the search's takes the mouse mode's buttons (≡ = ↵ starts the hit) until B, the search closes or Arcadia
  leaves the front (owner, 2026-10-22). "Steams Desktop-Belegung ignorieren" (on; its own box, independent of
  the mouse mode, owner 2026-10-20) drops simulated mouse and keys from other programs while a controller is in use; beside a library game without the mouse mode only while ⧉ + ≡ are
  held, and then only keys (`SteamDesktopInputFilter`: low-level hooks only while a controller is connected and the
  option needs them, set anew every 30 s because Windows drops a slow hook silently). "Arcadia in front" is the
  foreground window's process and no other program's window covering Arcadia whole (`MouseModeHost.IsArcadiaActive`),
  not Avalonia's activation, which got out of step beside games. A game in front is a process from a library game's
  folder, or one whose path can't be read (anti-cheat) that started after Arcadia last started a game (owner, 2026-10-20).
- Status messages (`HealthIssue`, header, the settings row and category they belong to) have three levels, each with its own
  symbol: **Info** (round blue i) for intended behaviour in a situation, e.g. SISR paused because other controllers
  use Steam Input; **Warning** (yellow triangle with !) when a function is impaired and the user must act, e.g.
  Steam not running, usbip driver missing, SISR switched on but no longer installed; **Error** (red X) for lasting
  failures, e.g. SISR keeps crashing. The header shows the worst level; info alone doesn't make a status unhealthy
  (owner, 2026-10-21).
- Launcher actions (open in the launcher, uninstall) are `LaunchTarget`s on `Game`;
  a null one is shown disabled with the reason, or for uninstall opens the launcher instead.
- Running games (#95, owner 2026-10-20 and 2026-10-21): every running game shows "Läuft", started from Arcadia or in its
  launcher; the launcher tells (`IRunningGamesMonitor`: Steam's registry, Epic's log), else a process from the install
  folder (`ProcessGames`, only for launchers without a monitor, never on the UI thread). Asked every 2 s, also with
  Arcadia in the background, since it may be seen on a second screen (owner, 2026-10-21). From A until it is seen running
  a game shows "Wird gestartet …" (2 min at most); a second A then only toasts. A on a running game is "Zurück ins
  Spiel" (its window comes to the front, a toast if it has none); several games may run at once. The cover gets a
  "Läuft" badge, preview and detail page "Läuft seit HH:MM", likewise the search hits and the context menu. While it
  runs or starts, "Deinstallieren …" and the game's settings that only apply at its next start (the Steam Controller
  mode) are disabled with the reason in the tooltip; "Individueller Mausmodus" applies at once and stays. After the end
  the library is read again (playtime, "Zuletzt gespielt"). Only a game started from Arcadia brings Arcadia to the front
  when it ends (#94), 1 s later, and only if that game, its launcher (`ProgramNames`), the desktop or no window is in
  front then; another program keeps the front.
- If an update is available, A still says "Spielen" and starts the game; the launcher installs the update
  first. The update shows as an icon, and the detail page says below the button that the launcher may update
  first (owner, 2026-10-12). Launchers have no update-only command: Steam ignores `steam://install` for
  installed games and `steam://validate` verifies every file first; Playnite offers no update action either.

**Errors:** a game start goes through `GameLaunchService.TryStartAsync`, which never throws. Work nobody awaits
(timer handlers, `Task`s started from sync code) runs through `AppErrors.Guard`/`.Forget(context)`: unexpected
errors land with stack trace in `arcadia.log` and as a toast. No `_ = SomethingAsync()` in the App.

**Avalonia 12:**
- A locally set property (e.g. `Fill="…"`, `Stroke="…"`) beats any style — state colours must
  come entirely from styles (`Classes.x="{Binding …}"` + `Style Selector="….x"`).
- `RenderTransform` can't be animated with key frames → give the element its own
  `<RotateTransform/>` and animate `RotateTransform.Angle` (see `Path.spinner`).
- `SystemDecorations` is now `WindowDecorations`. Box shadows of `Button.plain` go on the
  template part: `Button.x /template/ Border#Chrome`.
- Fluent's `Button` and `ItemsControl` clip to their bounds, so an outer focus ring (BoxShadow) is cut off;
  `Theme.axaml` sets `ClipToBounds=False` on both. A `UserControl` clips too; set it on the one whose edge a ring reaches. A focus ring also needs room in clipping parents
  (`ScrollViewer` padding). With several BoxShadows, the later one paints on top.
- Compiled bindings: every `DataTemplate` needs `x:DataType`; reach the window's view model
  from inside a template with `$parent[Window].((vm:MainViewModel)DataContext)?.Prop`.
- A binding path through a property that can be null (`Detail.X`, `FocusedGame.X`, a `DataContext` not set yet)
  logs a binding error each time, `FallbackValue` or not. Write `Detail?.X` and give a value type a
  `TargetNullValue` (`IsVisible="{Binding Detail?.IsFromLibrary, TargetNullValue=False}"`). `XamlBindingTests` checks
  both on every `.axaml` against the App's properties declared nullable (`T? Name`), so declare nullable what can be null.

**Launcher and controller findings** (verified on the owner's PC; read the matching file before
touching that code, and add new findings there, not here):
- Steam library, installs, updates, `shortcuts.vdf`: `docs/launchers/steam.md`
- Epic launcher, catalog, install monitor, playtime: `docs/launchers/epic.md`
- Steam Controller Support (SISR, usbip-win2, focus handling): `docs/controller-support.md`

## Git

- Commit and push to feature branches freely; never push to `main`. Changes reach `main` only through a PR
  that the owner reviews and merges.
- Branch names: `{prefix}/{issue-title}-{issue-number}` in kebab case, e.g. `fix/steam-scanner-crash-42`; without
  an issue, drop the number. Prefixes: `feat` (new or changed feature behaviour), `fix` (bug fix), `ci` (CI only),
  `test` (tests only), `doc` (docs only, agent skills included), `td` (any other product code change, i.e. tech
  debt). Rename generated names such as `claude/project-thread-…` before the first push.
- **Never** end commit messages with a `Co-Authored-By` trailer.
- Commit messages: English, imperative subject, body explains why.
- Opening a PR needs no prior OK (tell the owner the link). Merging, tags/releases, closing issues and posting
  comments only on the owner's explicit word.
- License: GPL-3.0-only. Third-party notes in `THIRD-PARTY-NOTICES.md`.
