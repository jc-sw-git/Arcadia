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
- Versioning (owner's rule): SemVer, 0.x until the first full release 1.0. Major may break; minor adds only
  non-breaking features; patch only fixes bugs — no features, no intended behaviour change (security aside).
  This matters because third-party plugins are planned.

## Verifying UI changes

Tests cover Core/Launchers/ControllerSupport logic, not the UI. For UI changes, run the app and
look at it — see the skill `.claude/skills/arcadia-verify/SKILL.md` and the scripts in
`tools/dev/` (screenshot with key presses, foreground tracing, XInput probe, Steam shortcuts).
The owner often tests manually at the same time — say before driving the app with keys.

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
| `src/Arcadia.Core` | `LauncherRegistry` (the registered launchers; reads see only the enabled ones), `KnownLaunchers`, `GameSorter`, `PageSizes`/`PageWindow`, `SearchQuery`, `AppSettings`/`SettingsStore`, `LibraryService`/`LibraryRefresher` (scan and rescan), `InstallTracker` (installs started from Arcadia or in the launcher), `Changelog` (release notes between versions) |
| `src/Arcadia.Launchers` | `BuiltInLaunchers` (the one place a launcher is registered), integrations (`SteamIntegration`, `EpicIntegration`, `DemoIntegration`), Epic scanner, catalog, log reader, client and install monitor, `DirectLauncherClient`, `GameLaunchService`, `ProcessWatch`, Steam (`SteamScanner`, `SteamInstallMonitor`, `SteamContentLog`, `TextVdf`, `BinaryVdf`, `SteamShortcuts`, `SteamInstallation`) |
| `src/Arcadia.Input` | `XInputGamepadSource` (polls 4 slots, auto-repeat for D-pad/stick) |
| `src/Arcadia.ControllerSupport` | Steam Controller Support: `SisrHost`, `SisrApiClient`, `UsbipDriver`, `GitHubReleases`, `ControllerSupportService`, `ControllerSupportRemoval` (uninstall) |
| `src/Arcadia.App` | Avalonia 12 UI (MVVM, CommunityToolkit.Mvvm 8.4 partial properties), `Theme/Theme.axaml`, `Views/MainWindow.axaml`, `ArcadiaUpdateService` (Velopack). `MainViewModel` holds navigation and wires the parts; `StatusBarViewModel` (header) and `ToastViewModel` are its children |
| `tests/Arcadia.Core.Tests` | xUnit |

Big Picture UI is laid out at 1920×1080 inside a `Viewbox`. Focus/navigation is done in the
view models (`MainViewModel.Handle(UiAction)`), not with Avalonia focus: buttons are
`Focusable=False`, keys are handled in a tunnelling `KeyDown` handler.

## Rules that are easy to get wrong

**Product decisions** (agreed with the owner; don't change without asking):
- Page sizes 10/25/50/100, default 25. A size is selectable if the library has *more* games
  than the next smaller size (`PageSizes.IsAllowed`).
- Sorting: name / install date / last played, each ascending or descending; games without a
  date always last.
- One "Ansicht" modal (Y) for sort + page size. X = search (all launchers, on-screen keyboard,
  `/steam-epic term` prefix, keyword per launcher), R3 = keyboard layout switch, Menu/☰ or
  right click = context menu. No "all launchers" tab. No dedicated favourite button.
- Dependent options sit in the same box as their main switch.
- If an update is available, A starts the update in the launcher; "play without update" stays
  available with a hint.

**Avalonia 12:**
- A locally set property (e.g. `Fill="…"`, `Stroke="…"`) beats any style — state colours must
  come entirely from styles (`Classes.x="{Binding …}"` + `Style Selector="….x"`).
- `RenderTransform` can't be animated with key frames → give the element its own
  `<RotateTransform/>` and animate `RotateTransform.Angle` (see `Path.spinner`).
- `SystemDecorations` is now `WindowDecorations`. Box shadows of `Button.plain` go on the
  template part: `Button.x /template/ Border#Chrome`.
- Compiled bindings: every `DataTemplate` needs `x:DataType`; reach the window's view model
  from inside a template with `$parent[Window].((vm:MainViewModel)DataContext).Prop`.

**Steam / Steam Controller** (all verified on the owner's PC, 2026-10-09):
- Steam gives the Steam Controller to processes it didn't start only as mouse/keyboard
  (desktop layout). `steam://forceinputappid/<appid>` only switches the layout; it does not
  deliver XInput to a foreign process. Restarting Arcadia through Steam was tried and dropped:
  everything Arcadia then starts (e.g. the Epic launcher) keeps "Arcadia" running in Steam.
- Therefore Arcadia runs **SISR** (github.com/Alia5/SISR, GPL-3.0, Go) as a separate process
  from `%LOCALAPPDATA%\SISR` (the official install location — SISR uses the marker shortcut
  that points to its own exe; a copy elsewhere creates a second marker). Started hidden with
  `--api.listen-address localhost:47640 --update-notify none --log.file …`, stopped via
  `POST /api/v1/quit`. Ready = API answers and `/api/v1/devices` shows a `viiper_device`.
- Default: SISR runs except while a game from the Steam launcher runs (double input);
  per-game override in `AppSettings.ControllerSupportOverrides`.
- After SISR stops, Steam keeps SISR's layout until the foreground changes — `force-config
  enforce:false` and `forceinputappid/0` are not enough. Arcadia bounces the focus to the
  desktop for 150 ms (40 ms is too short; a second Arcadia window doesn't count) and takes it
  back via `AttachThreadInput`. While SISR starts, `LockSetForegroundWindow` keeps Arcadia in front.
- usbip-win2 ≥ 0.9.8.1 is required. Its uninstall entry was renamed between versions
  ("USBip version 0.9.7.7" → "USBip 0.9.8.1"). Updating it replaces the emulated host
  controller → SISR must restart; Windows wants a reboot.
- Steam shortcut app ids in modern Steam are *not* CRC32(exe+name) — read the stored `appid`.
  `rungameid` = `(appid << 32) | 0x02000000`. Steam writes `shortcuts.vdf` only on exit;
  never write it while Steam runs. `BinaryVdf` round-trips the real file byte for byte.

**Steam library** (`SteamScanner`, verified 2026-10-12 against the owner's 3 libraries): libraries from
`steamapps/libraryfolders.vdf` (`path`), one `appmanifest_<appid>.acf` per installed app. `StateFlags` bit 2 =
update pending (4 = installed, 6 = with update, +64 while running); a first install without `buildid` is not
shown. Playtime (minutes) and `LastPlayed` come from `userdata/<user>/config/localconfig.vdf` →
`UserLocalConfigStore/Software/Valve/Steam/apps/<appid>`; that file also holds app tickets — never log it.
Steam stores no install date (the game folder's creation time is used). Owned but uninstalled games are **not**
readable locally: `appcache/packageinfo.vdf` keeps packages after a refund (owner's 007 First Light), and the real
license list (`userdata/<id>/config/licensecache`) is encrypted — they come with a Steam login (#83). Libraries are re-read on every scan,
so added or missing drives need no handling of their own.
`SteamInstallMonitor` (verified 2026-10-12 with real installs): a new install's manifest appears with `StateFlags` 2
and `buildid` 0, then 1026 (+1024 started) while downloading *or* waiting, 1538 (+512) when paused, 4 when done;
an update of an installed game is 1030. Steam downloads one app at a time but flags all waiting ones alike, so the
app whose `BytesDownloaded`/`BytesStaged` moved last is the downloading one. Cancelling a new install and
uninstalling (38 for a moment) both delete the manifest. Steam updates runtimes/redistributables on its own —
they are filtered like in the scanner.
The manifest's byte counters are written rarely (an update of Schedule I: 0 at the start, complete at the end), so the
monitor also reads `logs/content_log.txt` (`SteamContentLog`): `AppID <id> state changed : …,Update Running,…` marks
the app Steam works on (a waiting or paused one lacks it), `App update changed : …Verifying Staged|Committing` the end.
Steam has no update-only URI: `steam://install/<id>` is ignored for installed games, `steam://validate/<id>` updates
but first verifies every file (and resets modified ones).

**Epic:** all locally installed games have `bRequiresAuth` → they need the launcher. If it isn't
running, `EpicLauncherClient` starts it with `-silent`, waits for `EpicWebHelper` + 4 s, starts
the game via `com.epicgames.launcher://…` and re-sends once if no process appears from the
install directory within 30 s.
Owned but uninstalled games come from `Data\Catalog\catcache.bin` (Base64 JSON, refreshed by the
launcher, no login needed). Games = category `games` without `addons`/`digitaldownloads` and with
an empty `mainGameItem.id` (DLC/season passes name their game there): 331 on the owner's PC vs.
333 in the Epic library — the difference is Unreal Engine (one tile for all `engines` versions) and Unreal Editor for Fortnite (`addons/launchable`), which Epic shows but which are no games. Epic's own library list is cached in `webcache_*/IndexedDB/https_launcher.store.epicgames.com_0.indexeddb.blob` (V8-serialized, 431 apps incl. DLC). The cache is **ASCII-only** — every other
character is `?` ("Batman? Arkham", "K?ln"); `EpicCatalog.RepairTitle` drops trademark `?`s,
umlauts stay broken until metadata providers (#43) supply titles. Install: `…?action=install`.
`EpicInstallMonitor` (verified 2026-10-10 with real installs): queue order from
`Data\DownloadManager\DownloadState_<account>.json` (incl. paused, `[]` when idle); per-app events
from `EpicGamesLauncher.log` (`Queued install for the`, `StartTask: BinaryId [`, `Resume of active
installation`, `Pause is requested`, `Cancel of active installation`, `HandleTaskComplete … AlertCode=[ok]`,
`FCommunityPortalAppManagerImpl: Uninstall` — an uninstall then runs through the same queue with
the same `Queued`/`StartTask`/`HandleTaskComplete` lines, so only that first line tells it apart); install size from
`Manifests\Pending\*.item`, which **stays behind after a cancel** (never use it as state). The
queue file can lag behind (owner saw a running install vanish after cancelling a paused one), so
for apps the log reported on while Arcadia ran, the log decides; the file only orders them.
Reordering logs `New active position is requested for the <app>` followed by a pause of the old one. Resuming a paused app while
another downloads logs only `Resume of active installation` for the new one; the running one is cancelled
without a pause line (verified 2026-10-12), so only the latest `StartTask` counts as downloading. Progress =
size of the whole install dir / `InstallSize` (download goes to `.egstore` inside it, moved at the end).
Epic writes no percentages to the log.
Epic keeps total playtime only on its servers (login: #82). Locally the logs (`EpicGamesLauncher*.log`, current plus a few
rotated backups, UTC times) have `LogPortalPublishTime: Started/Stopped tracking playtime for <ns>:<item>:<app>` per
session — Arcadia keeps the latest start per app in `%APPDATA%\Arcadia\epic-last-played.json` because the backups
rotate away — and, on each launcher start, `Sending response: {…"get-installed-apps-response"…}` with
`installedVersion` and `hasUpdate` per app; a report whose version differs from the manifest's `AppVersionString` is stale. An update has no progress measure (the install dir
is full from the start), so `EpicInstallMonitor` reports it without a fraction. The library is also rescanned when the window is activated
and when a selected installed game's directory is missing.

## Git

- Work on feature branches; the owner decides when to commit and push — ask first.
- **Never** end commit messages with a `Co-Authored-By` trailer.
- Commit messages: English, imperative subject, body explains why.
- GitHub writes (issues, comments, PRs, pushes) only when the owner explicitly asks for them.
- License: GPL-3.0-only. Third-party notes in `THIRD-PARTY-NOTICES.md`.
