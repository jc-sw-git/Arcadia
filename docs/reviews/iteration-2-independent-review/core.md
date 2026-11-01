# Core and Abstractions (`src/Arcadia.Core`, `src/Arcadia.Launchers.Abstractions`)

All 50 source files read. Settings, library scan/refresh, trackers, search, paging, logo check, registry.

**Assessment.** Small, well named, mostly pure logic; the error-prone parts (settings persistence, per-launcher
isolation in scan/locate/running games, process matching, search folding) are done carefully and tested. The
abstractions are dependency-free with default interface members for optional capabilities, the right shape for
plugins. No Critical or High. Gaps: `InstallTracker` is the one place where one launcher's failure still takes the
others down; a pending install on an unwatched launcher rescans the whole library every 15 s for the session; nothing
bounds how long a scanner or monitor may run; a few UI-thread costs scale with library size.

| ID | Sev | Cat | Finding |
|---|---|---|---|
| CORE-1 | Medium | Robustness | One failing install monitor aborts the install poll for all launchers, toast every 2 s |
| CORE-2 | Medium | Performance | Pending install on an unwatched launcher → full rescan every 15 s, forever |
| CORE-3 | Medium | Robustness | No timeout around scanner/monitor calls; a hang stops refresh or polling silently |
| CORE-4 | Low | Robustness | A scanner failure is invisible to the user (empty tab, no toast) |
| CORE-5 | Low | Performance | `HasChanged` builds sorted text signatures of all games on the UI thread; omits `InstalledAt`/`Title` |
| CORE-6 | Low | Robustness | Settings saved on the UI thread, not flushed; power loss can reset to defaults |
| CORE-7 | Low | Robustness | No schema version in `settings.json`; an incompatible change drops all settings |
| CORE-8 | Low | Modularity | `KnownLaunchers` in Core holds 8 launcher identities (6 unused) and a Steam flag |
| CORE-9 | Low | Robustness | `LauncherRegistry` doesn't validate ids (empty, `:`); a throwing plugin getter ends startup |
| CORE-10 | Low | CodeQuality | Keyword rule duplicated in `LauncherRegistry.Info` without the uniqueness check |
| CORE-11 | Low | Performance | `Game.Key` allocates on every access; used in per-game loops |
| CORE-12 | Low | Performance | `GameSearch` rebuilt on the UI thread on every rescan while searching |
| CORE-13 | Low | Tests | `InstallTrackerTests` has no failing-monitor case |
| CORE-14 | Info | CodeQuality | BOM literal, per-call allocations, mutable dictionaries in a shared record, undocumented thread contract |
| CORE-15 | Info | Security | SVG check: CSS escapes and SMIL not caught (inert in the rasteriser) |

**CORE-1** `Launchers/InstallTracker.cs:82`: `monitors.Select(m => (m.LauncherId, m.Poll())).ToList()` without a
per-monitor catch; `RunningGamesTracker.Ask` (`:176-200`) catches, logs and keeps the previous state. Epic's monitor
throwing → `Apply` never runs, Steam's progress stale, and the `DispatcherTimer` + `AppErrors.Guard`
(`App.axaml.cs:33,238`) toasts with stack trace every 2 s. Fix: catch per monitor, log once, keep that launcher's
last snapshot; add the test (CORE-13). Also reported as LNCH-18. High.

**CORE-2** `Launchers/InstallTracker.cs:22-64`, caller `Services/LibrarySession.cs:142-151`: `_pending` shrinks only
when a game shows up installed; while `HasPending`, `CheckPendingInstallsAsync` runs `LoadAsync()` (Locate + every
scanner) every 15 s. Cancel an install of a launcher without `IInstallMonitor` and Arcadia rescans everything until
closed, beside a running game (budgets: idle CPU < 1 %, no cost to a game). Fix: deadline per pending entry or
rescan only that launcher (`LoadAsync(launcherId)` exists). High.

**CORE-3** `Library/LibraryService.cs:54`, `Library/LibraryRefresher.cs:24-33,61-64`,
`Launchers/RunningGamesTracker.cs:129-173`, `InstallTracker.cs:74-88`; `IInstallMonitor.Poll()` and
`IRunningGamesMonitor.Poll()` take no `CancellationToken`. A blocking scanner leaves `_running` incomplete: every
unforced refresh returns null, forced ones await forever, `IsLoading` stays set; a blocking `Poll()` leaves
`_polling == true` for the session with no log line. DoD: waits have a timeout; Core owns these waits and has none.
Fix: linked CTS with `CancelAfter` per launcher scan, `WaitAsync(timeout)` around `Poll`, log and skip; add a token to
the two `Poll` methods before the plugin interface freezes. High on code, Medium on impact today.

**CORE-4** `Library/LibraryService.cs:58-63`: catch → `Trace.TraceWarning` → empty `LauncherLibrary`; no toast, no
"scan failed" flag. A corrupt `libraryfolders.vdf` shows Steam as empty. Fix: `LauncherLibrary.Problem` or a
`Failed` callback like `RunningGamesTracker.Log`.

**CORE-5** `Library/LibraryRefresher.cs:47-51,93-101`: per rescan two interpolated strings per game (10,000 with
5,000 games), two ordinal sorts, two multi-MB joins, on the UI thread (continuation), on every window activation.
The signature omits `InstalledAt` (a `SortField`) and `Title`, so a reinstall into the same folder or a renamed title
doesn't re-sort. Fix: per-launcher count + zip or dictionary compare with a fingerprint record; include the fields.

**CORE-6** `Settings/AppSettings.cs:264-267` (`WriteAllText(temp)` + `Move`), `Settings/SettingsService.cs:21-28`:
synchronous save on the UI thread on every tab switch (`MainViewModel.cs:589`), no `Flush(true)`; power loss after
the rename can leave an empty file → `JsonException` → `.broken` + defaults → `ActiveLaunchers == null` → first-run
question again. Fix: `FileOptions.WriteThrough`, save on one worker with coalescing. Medium.

**CORE-7** `Settings/AppSettings.cs:9-78,225-229`: a changed property type throws `JsonException` → whole file
discarded. Velopack rollback to an older version loses everything. Fix: `Version` property; field-level leniency;
log `JsonException.Path`.

**CORE-8** `Launchers/KnownLaunchers.cs:9-18`: `Steam … GamesUseSteamInput: true`, `Gog`, `Ubisoft`, `Ea`, `Xbox`,
`BattleNet`, `Amazon` (only tests use the last six). Nothing in Core or App reads it except
`LauncherSettingsViewModel.cs:632` and `MainViewModel.cs:837` (APP-9). Move each `LauncherInfo` into its integration.

**CORE-9** `Launchers/LauncherRegistry.cs:31-53`: only duplicates checked; `Game.LauncherIdOf` assumes no `:`
(`Abstractions/Game.cs:85-86`), `IsKeywordCharacter` not applied to `SearchKeyword`; `integration.Launcher.Id` at
`:36` is outside the try the comment promises. A plugin id `my:launcher` never matches in `UsesArcadiaPad`,
`RealStateOf`, `FallbackLaunchers`. Fix: reject empty/`:`/non `[a-z0-9-]` ids, wrap in try → `Rejected`.

**CORE-10** `LauncherRegistry.cs:86-88` vs `LauncherNames.cs:33-58`: two launchers with the same hand-edited
keyword both pass `Info`; `LauncherScope.Of` (`:22`) then scopes `/x` to both. One `IsValidKeyword` + deterministic
tie-break.

**CORE-11** `Abstractions/Game.cs:80,83`: `Key => $"{LauncherId}:{Id}"` per access; `Carousel.Find` over 5,000
games allocates 5,000 strings. `_key ??=`. **CORE-12** `Search/GameSearch.cs:34-41`: constructor folds and
culture-sorts every title (own comment: 25 ms+ for 5,000) synchronously in `SearchViewModel.Update` on each rescan
while typing. Build on the pool and swap, or reuse when keys unchanged.

**CORE-13** `tests/Arcadia.Core.Tests/InstallTrackerTests.cs`: no throwing monitor, no `HasChanged` for a changed
`InstalledAt`. **CORE-14** `Launchers/LogoFile.cs:95` invisible U+FEFF literal; `Search/SearchQuery.cs:23`
`IndexOfAny([' ', '\t'])` allocates per key press; `Library/LibraryTypes.cs:74` `Order.ToList().IndexOf` inside
`OrderBy`; `AppSettings.cs:26,33`, `MouseModeSettings.cs:71` mutable `Dictionary` `init` properties on a record
shared across threads (correct today because every `With…` copies); `InstallTracker`/`LibraryRefresher` are
single-threaded by convention without saying so; German strings in Core log lines couple Core to the UI language.
**CORE-15** `LogoFile.cs:73-173`: the check is real (strict UTF-8, DTD prohibited, no resolver, `script`/`foreignObject`/
`image`/`feImage` rejected, `href` fragments only, `url(`/`@import`/`on*` scanned); CSS escape spellings
(`u\72l(`) and SMIL `<set attributeName="href">` pass but are inert; one PNG rasterisation at import. Doc comment only.

**Done well.** `SettingsStore` (temp + move, retries, `.broken` copy, refuses to save over an unreadable file,
normalises hand-edited values); per-launcher isolation in `Locate`, `ScanAsync`, `Ask` with tests; `GameSearch` one
ordinal pass per key press; `GameFolders` walks up the path, cost independent of library size; `ProcessGames` looks
only at new pids and detects pid reuse by start time; `LogoFile` decides by content; paging/carousel/focus edge
cases (0 and 1 items) tested; abstractions document threading on each interface.
