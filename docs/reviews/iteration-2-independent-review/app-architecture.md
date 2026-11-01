# App: architecture, modularity, code quality (APP-9 to APP-20)

Continues [app.md](app.md); the findings table and APP-1 to APP-8 are there.

**APP-9** `ViewModels/MainViewModel.cs:837-853` (`KnownLaunchers.Steam.Id` → "Steam-Einstellungen" link),
`Settings/LauncherSettingsViewModel.cs:630-639` (`if (id != KnownLaunchers.Steam.Id) return []` for the
desktop-control rows), `Settings/ControllerSettingsViewModel.cs:88-112`, `App.axaml.cs:15,117` and `Program.cs:8,77`
(`using Arcadia.Launchers.Steam`, `SteamInstallation.Find()`), `Services/ForegroundOwner.cs:23`
(`["steam.exe", "steamwebhelper.exe"]` duplicating `ILauncherIntegration.ProgramNames`, which `App.axaml.cs:265`
already uses). The product reason (Steam the program matters to controller support regardless of the launcher tab)
is documented in comments; the mechanism is hard-coded and a Steam plugin or a second launcher needing controller
rows cannot plug in. Let ControllerSupport expose the rows it needs per launcher id; feed `ForegroundOwner` from the
registry; or record the exception in `docs/product/launchers.md`. Part of the launcher-coupling theme (README).

**APP-10** `App.axaml.cs:76-349` builds ~25 objects, 6 timers, ~20 subscriptions inline (incl. the local
`ReturnAfterGameAsync` `:256-275` and the startup `Task.Run` `:277-314`); `tests/Arcadia.App.Tests/ArcadiaDriver.cs:63-175`
repeats it by hand. Divergences: `viewModel.Dialogs` set only in the App (`:180`), so `PickLogoAsync`/`PickFolderAsync`
(`LauncherSettingsViewModel:994,1054`) return at the first guard in tests (logo and folder flows have no acceptance
path); `MouseModeHost`, `SystemDialogs`, Pan overlay/`BringBack` App-only; `StartedGameEnded → ReturnAfterGameAsync`
App-only; the activation refresh lives in `MainWindow` (APP-1) and is replayed manually. Extract an `ArcadiaComposition`
with a `Parts` record of seams that both call.

**APP-11** `MainViewModel.cs` 1320 (routing + page lifecycle + three dialogs + Pan dialogs/update orchestration
`:997-1224` + controller-mode state + per-game mouse-mode state + settings factory + four host interfaces);
`LauncherSettingsViewModel.cs` 1277 (reorder lift/drop/drag + launcher page with 12 rows + account/avatar +
controller rows + text editors + logo/folder import + status builders); `MouseModeHost.cs` 665 (gamepad wiring +
foreground tracking + automation + keyboard window + search takeover + desktop filter + game block + persistence);
`SearchViewModel.cs` 868 (text/caret, scope/chips, hit windowing/scrollbar, keyboard mapping, mouse-mode typing,
filter dialog). Minimal splits: `PanDialogsViewModel`, `LauncherListViewModel` + `LauncherPageViewModel`,
`ForegroundTracker` + `MouseModeKeyboardHost`, `SearchHits`; name them in `app.md`.

**APP-12** `MainViewModel.cs:94` `AppErrors.Occurred += …` never unsubscribed (`grep "Occurred -="` empty);
`StatusBarViewModel._clock` and `GameActions._hold` never disposed; `MouseModeHost.Dispose` leaves `_window.Activated/
Deactivated`. Process-lifetime singletons in the App; in the tests every `ArcadiaDriver` stays rooted for the run.
**APP-13** `Views/WebPageHost.cs:122,192` `Task.Delay(20 s)` / `(4 s)` real time while `WebPages` uses `_time`;
the "browser doesn't start" case needs 20 real seconds. **APP-14** `ViewModels/GameTileViewModel.cs:33` vs
`Services/Formatting.cs:101-102`, which handles empty strings and text elements. **APP-15** `docs/architecture/app.md:69`
`Services/QrCodes` doesn't exist; missing: `ForegroundOwner`, `ForegroundGuard`, `WebPages`/`WebPageHost`,
`PanUpdatePageViewModel`, `FirmwareAttemptViewModel`, `ControllerLayoutsViewModel`, `MouseModeRows`,
`SteamControllerForGames`, `PanNotices`, `Formatting`, `AppErrors`/`AppLog`. **APP-16**
`Input/SteamControllerForGames.cs:69-193`: four positional `Func<bool>`s, `padForPresence`/`routingForCopy` null when
captured, assigned later. **APP-17** `Search/SearchViewModel.cs:731-774,104-105,485-491`: `_search.Find` per
keystroke, linear in library size on the UI thread; measure with 5,000 demo games. **APP-18**
`Services/ArcadiaUpdateService.cs:163-167,42`: `Progress = percent; OnChanged()` on Velopack's thread; `IsChecking`
non-atomic test-and-set between timer and row. **APP-19** `App.axaml.cs:128-134,361-389`; also `BackgroundArgument`
compared case-sensitively while the other switches ignore case. **APP-20** `Services/LibrarySession.cs:20-23,366-371`.

**Done well.** Three-layer exception net plus `AppErrors` on all 6 `DispatcherTimer`s and ~20 fire-and-forget sites;
`MouseModeHost.OnPolled` isolates the poll thread from its own failures; `InstallFolderCheck` coalesces with one in
flight; held buttons cancelled on every top change, focus loss and deactivation; `TileSlotViewModel` recycles
controls and `LibraryRefresher` returns null when nothing changed; `LauncherLogos` validates magic bytes, size,
dimensions, re-encodes to PNG and confines names to the logo folder; `SettingsService` lock + volatile snapshot with
poll-thread readers rebuilding only on reference change; every dialog handles `UiAction.Back`; the two things a pad
can't do (UAC, Windows file dialogs) are named and bridged by `SystemDialogs.HoldForDialog`; comments explain why.
