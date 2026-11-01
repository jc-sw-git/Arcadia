# App (`src/Arcadia.App`, C# only)

View models, services, settings, search, keyboard, composition root. All C# files read; `ArcadiaDriver` compared
with `App.axaml.cs`. XAML/theme is in [cross-cutting.md](cross-cutting.md) (XC-11). APP-9 to APP-20 (architecture, modularity, code
quality) and "done well" continue in [app-architecture.md](app-architecture.md).

**Assessment.** Good shape for its size: exception nets at all three levels (AppDomain, `TaskScheduler`, Avalonia
Dispatcher), every timer and fire-and-forget goes through `AppErrors.Guard`/`.Forget` (one exception:
`ToastViewModel`), blocking I/O consistently pushed off the UI thread, game starts through
`GameLaunchService.TryStartAsync`, one `IPage`/`IPageHost` lifecycle honoured by all five pages, logs strip the user
profile. Only the visible page/slice is materialised (≤100 tiles, 5 detail pages, 3 search windows). The weaknesses
are structural: four classes of 665-1320 lines with 3-5 responsibilities each, a hand-duplicated composition root in
the tests with real divergences, Steam special-cased by id and by assembly reference. No Critical, no High.

| ID | Sev | Cat | Finding |
|---|---|---|---|
| APP-1 | Medium | Robustness | `MainWindow.Activated` async lambda bypasses `AppErrors`; view code-behind drives the library |
| APP-2 | Low | Robustness | `ToastViewModel.Show` uses `_ = Task.Delay(...).ContinueWith(...)`; CTS never disposed |
| APP-3 | Medium | Robustness | Update download has no timeout/cancellation; unexpected check errors logged without stack |
| APP-4 | Medium | Robustness | `HandleDialog` closes a confirmation opened by the confirmed action (latent) |
| APP-5 | Low | Robustness | `LibrarySession`: `IsLoading` not reset on failure; pending check bypasses `LibraryRefresher` |
| APP-6 | Medium | Performance | `AppLog` opens/appends/closes the file synchronously per line, on the input path |
| APP-7 | Low | Performance | `MouseModeHost` 250 ms UI timer with 4-5 P/Invokes per tick, never stopped |
| APP-8 | Low | Performance | Bitmap decoding on the UI thread; `GameActionViewModel.Icon` re-parsed per read |
| APP-9 | Medium | Modularity | Steam special-cased by launcher id and by assembly reference in App/UI |
| APP-10 | Medium | Architecture | Composition root duplicated between `App.axaml.cs` and `ArcadiaDriver`, with divergences |
| APP-11 | Medium | Architecture | God classes: `MainViewModel`, `LauncherSettingsViewModel`, `MouseModeHost`, `SearchViewModel` |
| APP-12 | Low | Tests | Static `AppErrors.Occurred` subscription and undisposed timers root `MainViewModel`s |
| APP-13 | Low | Robustness | `WebPageHost` waits on real time, not `TimeProvider` |
| APP-14 | Low | CodeQuality | `GameTileViewModel.Monogram` indexes `Title[..1]` (empty title, surrogate pairs) |
| APP-15 | Low | Docs | `docs/architecture/app.md` lists `Services/QrCodes` (missing) and omits a dozen classes |
| APP-16 | Low | CodeQuality | `SteamControllerForGames.Create` has 16 parameters and temporal coupling via captured locals |
| APP-17 | Low | Scalability | Search runs synchronously over all games per keystroke (not measured) |
| APP-18 | Low | Robustness | `ArcadiaUpdateService` mutates state from the download thread |
| APP-19 | Info | Security | `--started-game` key from the command line unvalidated (impact: a bogus badge) |
| APP-20 | Info | Modularity | `LibrarySession` settle times tuned to named launchers (Steam 30 s, Epic 5 s) |

**APP-1** `Views/MainWindow.axaml.cs:148-154` `Activated += async (_, _) => await _viewModel.Session.RefreshLibraryAsync()`
(an `async void` handler, caught only by the Dispatcher net) while `App.axaml.cs:250` wires the same event to
`PollRunningGamesAsync().Forget(...)`. Move both into one `MainViewModel.WindowActivated()`.

**APP-2** `ViewModels/ToastViewModel.cs:21-34`: the only `_ = …` task in the App (CLAUDE.md forbids it);
`FromCurrentSynchronizationContext()` throws if ever called off the UI thread; one CTS per toast never disposed.
`HideLaterAsync(token).Forget("Toast")`.

**APP-3** `Services/ArcadiaUpdateService.cs:163-167` `DownloadUpdatesAsync(update, …)` without token or timeout
(the check has a 30 s `WaitAsync`): a stalled CDN leaves `Progress` set forever, "Jetzt aktualisieren" greyed, no
further checks this session. `:107-112` logs `ex.Message` only for every non-cancel exception, so an unexpected bug
loses its stack. DoD: waits have a timeout; unexpected errors with stack. High.

**APP-4** `ViewModels/MainViewModel.cs:408-419`: `ConfirmationViewModel.Handle(Accept)` runs `_confirm()` and then
returns false (`ConfirmationViewModel.cs:49-55`, verified), after which Main sets `Confirmation = null`, so a
confirm action that opens the next dialog loses it; the comment claims the opposite order. The `GameMenu` branch
(`&& GameMenu == menu`) and the mouse path do it right. No current action chains, so latent. Mirror the menu branch.

**APP-5** `Services/LibrarySession.cs:115-122` (`IsLoading = true … = false` without `finally`), `:142-158`
(`CheckPendingInstallsAsync` calls `_library.LoadAsync()` outside `_refresher`'s `_running` gate, so an older
snapshot can overwrite a newer one; see CORE-2).

**APP-6** `Services/AppLog.cs:27-37` (verified): under one global `Gate`, `CreateDirectory` + `Exists` + `FileInfo`
+ `AppendAllText` (open/write/flush/close) per line; callers `MainWindow.axaml.cs:217,226,242` (every key, incl.
auto-repeat), `Input/GamepadRouting.cs:131` (poll thread, `Highest` priority while pads are forwarded),
`Services/InputLatency.cs:49` (per frame), `AppLogTraceListener` (every Avalonia binding warning). 0.5-3 ms per line
on an AV-scanned `%APPDATA%`, two lines per press, UI and poll thread serialised against each other. Within the 50 ms
budget on an SSD, but the one piece of blocking I/O on the hot input path. Same finding as INP-6 and XR-2. Fix: keep
the API, drain a `Channel<string>` on one background task holding an open writer, flush on error/shutdown.

**APP-7** `Services/MouseModeHost.cs:121-128,338-370,553-577,599-651`: `_desktopFilterCheck` started by the
constructor overload and never stopped; `ProcessWatch.ForegroundProcessId()` three to four times per tick, 4×/s
for the process lifetime, also with no controller and mouse mode off. Compute the foreground once per tick; stop the
timer when nothing depends on it; measure idle CPU.

**APP-8** `Services/LauncherLogos.cs:96-119` `new Bitmap(path)` on the UI thread ("Call on the UI thread"),
`Settings/LauncherSettingsViewModel.cs:649-669` avatar re-decoded on every account state change with
`catch (Exception e)` logging the type only; `ViewModels/GameActionViewModel.cs:139-149` `StreamGeometry.Parse` per
binding read. Decode on the pool as `ImportAsync` does; cache geometries per `GameActionKind`.
