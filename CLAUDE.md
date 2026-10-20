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
  `--sisr-paused-for <dir>`, and `--background` brings the window back behind the game instead of in front.
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
| `src/Arcadia.Launchers.Abstractions` | What a launcher is made of, with no dependency on the rest: `ILauncherIntegration` (scanner, optional client and install monitor, `Locate`), `ILauncherScanner`, `ILauncherClient`, `IInstallMonitor`, models (`Game`, `LauncherInfo`, `LaunchTarget`). The future plugin interface for launchers |
| `src/Arcadia.Core` | `LauncherRegistry` (the registered launchers; reads see only the enabled ones), `KnownLaunchers`, `GameSorter`, `PageSizes`/`PageWindow`, `Carousel` (detail page neighbours), `SearchQuery` (prefix parser), `GameSearch` (hits by relevance or a sort order; titles folded and sorted once, so a key press is one ordinal pass), `LauncherScope` (chips and prefix in step), `HitWindow` (the hits on screen, turned with LT/RT), `AppSettings`/`SettingsStore`/`SettingsService` (the one shared copy of the settings), `LibraryService`/`LibraryRefresher` (scan and rescan), `InstallTracker` (installs started from Arcadia or in the launcher), `Changelog` (release notes between versions), `GameFolders` (install folders of the library, to tell a game in front), `MouseModeSettings`, `ReturnWithViewSettings` |
| `src/Arcadia.Launchers` | `BuiltInLaunchers` (the one place a launcher is registered), integrations (`SteamIntegration`, `EpicIntegration`, `DemoIntegration`), Epic scanner, catalog, log reader, client and install monitor, `DirectLauncherClient`, `GameLaunchService`, `ProcessWatch` (also `PathOf(pid)` and the foreground process), Steam (`SteamScanner`, `SteamInstallMonitor`, `SteamContentLog`, `SteamLoginLog`, `TextVdf`, `BinaryVdf`, `SteamShortcuts`, `SteamInstallation`) |
| `src/Arcadia.Input` | `XInputGamepadSource` (polls 4 slots, presses and releases, auto-repeat for D-pad and sticks), `HoldGesture` (tap vs. hold), `RepeatGesture` (held erase), `LatchGesture` (shift: tap or hold), `SoloButtons` (View/Menu act alone, not together); mouse mode (#64): `MouseMode` (Back + Start, `Polled` readings, also in the background), `MouseModeMapping` (buttons → mouse/keys), `MouseModeAutomation`, `StickCurve`, `SendInputOutput` (`IMouseKeyboardOutput`), `ReturnWithView` (View held brings Arcadia back), `ControllerActivity`/`InjectedInputGate`/`SteamDesktopInputFilter` (drop Steam's desktop layout) |
| `src/Arcadia.ControllerSupport` | Steam Controller Support: `SisrHost` (`ISisrHost`), `SisrApiClient`, `UsbipDriver`, `UsbipPorts`, `GitHubReleases`, `ControllerSupportService` (`SisrState`), `ControllerSupportPlatform` (Windows/Steam, replaced in tests), `ControllerSupportRemoval` (uninstall) |
| `src/Arcadia.App` | Avalonia 12 UI (MVVM, CommunityToolkit.Mvvm 8.4 partial properties), `Theme/Theme.axaml`, `Views/MainWindow.axaml`, `ArcadiaUpdateService` (Velopack). `MainViewModel` holds navigation and wires the parts; `StatusBarViewModel` (header), `ToastViewModel`, `GameMenuViewModel` (context menu) and `GameDetailViewModel` (detail carousel, `Views/GameDetailView.axaml`) are its children; `Search/` holds `SearchViewModel` (search page; hits shown in `HitSlotViewModel` places that keep their controls when the hit changes), `OnScreenKeyboardViewModel` and `KeyboardLayout`; `Controls/` holds `HoldRing`, `RevealPanel` (the sliding keyboard), `SharedWidthRow` and `CollapsingStack` (the shrinking chips); mouse mode: `Services/MouseModeHost` (wiring, overlay, automation), `Services/ForegroundGame`, `Views/MouseModeOverlay` (own non-activating window, clicks pass outside its region) with `Controls/ScreenEdgeFill` |
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
  right click = context menu, Menu/☰ = settings (swapped by the owner, 2026-10-17). No "all launchers" tab. No dedicated favourite button.
- Dependent options sit in the same box as their main switch.
- UI texts name a controller button by the symbol on it, not by its name: ⧉ and ≡, not View/Back and Menu/Start
  (Xbox symbols for now; later those of the controller in use, #72) (owner, 2026-10-20).
- Dialogs (Ansicht, settings, prompts) show an ✕ in the top right corner while mouse or keyboard is in use
  (`MainViewModel.ShowsCloseButtons`); the controller closes them with B. A dialog that only informs (e.g. the
  mouse mode's help) has no "Schließen" button besides (owner, 2026-10-20).
- A starts a game on release; holding A for 1 s opens the detail page (a ring around the preview fills from
  0.2 s on, letting go before 1 s cancels). With the mouse a click picks a cover, a double click starts it and
  holding the left button 1 s opens the details with the same ring (owner, 2026-10-19). The context menu and the detail page offer the same actions
  (`GameActionViewModel.For`); the detail page has no menu, its actions are buttons (owner, 2026-10-17).
- The detail page is a carousel through the tab's games in the library's sort order, across pages and
  wrapping at the ends (two games: the other on both sides; one: no neighbours). LT/RT turn it, LB/RB switch
  the tab and stay on the detail page (first game), Y re-sorts with the shown game kept in the middle, B returns
  to the library page of the game shown last (owner, 2026-10-17).
- Search (#12): opened with X the on-screen keyboard is out, the hits sit in one row above it (sliding sideways,
  faded neighbours at the edges, LT/RT buttons beside them, "RS Treffer wählen" above); opened with F or a click,
  or after B, a real key or the tab on the keyboard's top edge, it slides in and the hits fill a grid that scrolls
  up and down (faded rows at the top and bottom) and the D-pad and A act on the hits as on covers; X brings the
  keyboard back. The legend at the bottom is always there and names the input in use: the controller's buttons,
  or mouse and keyboard (it never hides, so the keyboard above it doesn't move under a click). B closes the layout menu first, then
  puts the keyboard away, then closes the search; B never erases. D-pad/left stick type, the right stick picks a
  hit, LT/RT (or Page Up/Down, the wheel) turn the hits a window at a time, as LT/RT turn pages and the detail page;
  the row wraps round like the detail page, the grid stops at its ends like the library and the wheel. A scroll bar
  shows where the shown hits are: across below the row, down beside the grid (owner, 2026-10-19). X erases (held: repeats), Y space, LB shift and RB symbols page (each: tap on/off, held only while held), L3 "Filter und Sortierung" (launchers, any number, and
  the library's sort orders plus relevance, the default; not saved), R3 layout menu (all five, the last chosen
  saved globally). ≡ starts the focused hit, ≡ held 1 s opens its details, ⧉ its menu; with a real keyboard
  Enter and Enter held do the same. The sort button sits in the chip row; with many launchers the chips shrink
  (keyword first, then the name, last only the icon) and a divider appears before the sort button. No hits and
  nothing typed show an empty state the theme draws (`ContentControl.searchEmpty`). ™ ® © count for nothing in
  the search. The `/launcher` prefix still works when typed but isn't advertised; chips and filter rewrite a typed
  prefix and never add one. A detail page opened from the search turns through the hits in their order; LB/RB
  and Y do nothing there, B returns to the search with its text unchanged (owner, 2026-10-17 and 2026-10-18).
- Mouse mode (#64): Back + Start held (1–9 s, default 3) switches it, also with Arcadia in the background; one mode for all
  pads; never saved (Arcadia starts normal). Its buttons are listed in `MouseModeSettingsViewModel.HelpRows`; in it
  Arcadia ignores gamepad navigation. View and Menu therefore act on release, or once held alone 0.2 s (so ≡ held still
  opens details), and not at all when pressed together (owner, 2026-10-19). A main switch "Mausmodus" turns it all off
  and keeps the other values. "Zurück zu Arcadia" is its own setting: View held alone (0–9 s, default 1; 0 at once) brings
  Arcadia back from the background, with or without the mouse mode, with a box whose frame fills (owner, 2026-10-20).
  Exceptions per process are planned for later. "Steams Desktop-Belegung ignorieren" (on) drops simulated mouse and keys
  from other programs while a controller is in use, unless a library game is in front without the mouse mode
  (`SteamDesktopInputFilter`, low-level hooks only while needed).
- Launcher actions (open in the launcher, uninstall) are `LaunchTarget`s on `Game`;
  a null one is shown disabled with the reason, or for uninstall opens the launcher instead.
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
  `Theme.axaml` sets `ClipToBounds=False` on both. A focus ring also needs room in clipping parents
  (`ScrollViewer` padding). With several BoxShadows, the later one paints on top.
- Compiled bindings: every `DataTemplate` needs `x:DataType`; reach the window's view model
  from inside a template with `$parent[Window].((vm:MainViewModel)DataContext).Prop`.

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
