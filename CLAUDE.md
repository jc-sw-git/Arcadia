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
- `ArcadiaUpdateService` checks GitHub Releases (stable only) every 6 h; "Jetzt aktualisieren" in Einstellungen →
  "Über Arcadia" downloads, then Arcadia closes normally (SISR stops) and Velopack installs and restarts it.
  While a library game runs it installs on exit instead. Push a tag `vX.Y.Z` to release (`release.yml`).
- Versioning (owner's rule): SemVer, 0.x until the first full release 1.0. Major may break; minor adds only
  non-breaking features; patch only fixes bugs — no features, no intended behaviour change (security aside).
  This matters because third-party plugins are planned.

## Verifying UI changes

Tests cover Core/Launchers/ControllerSupport logic, not the UI. For UI changes, run the app and
look at it — see the skill `.claude/skills/arcadia-verify/SKILL.md` and the scripts in
`tools/dev/` (screenshot with key presses, foreground tracing, XInput probe, Steam shortcuts).
The owner often tests manually at the same time — say before driving the app with keys.

## Layout

| Project | Content |
|---|---|
| `src/Arcadia.Core` | Models (`Game`, `LauncherInfo`, `LaunchTarget`), `GameSorter`, `PageSizes`/`PageWindow`, `SearchQuery`, `AppSettings`/`SettingsStore`, `ILauncherScanner`, `ILauncherClient` |
| `src/Arcadia.Launchers` | Scanners (`Epic`, `Demo`), launcher clients (`EpicLauncherClient`, `DirectLauncherClient`), `GameLaunchService`, `ProcessWatch`, Steam (`BinaryVdf`, `SteamShortcuts`, `SteamInstallation`) |
| `src/Arcadia.Input` | `XInputGamepadSource` (polls 4 slots, auto-repeat for D-pad/stick) |
| `src/Arcadia.ControllerSupport` | Steam Controller Support: `SisrHost`, `SisrApiClient`, `UsbipDriver`, `GitHubReleases`, `ControllerSupportService` |
| `src/Arcadia.App` | Avalonia 12 UI (MVVM, CommunityToolkit.Mvvm 8.4 partial properties), `Theme/Theme.axaml`, `Views/MainWindow.axaml` |
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
Reordering logs `New active position is requested for the <app>` followed by a pause of the old one. Progress =
size of the whole install dir / `InstallSize` (download goes to `.egstore` inside it, moved at the end).
Epic writes no percentages to the log. The library is also rescanned when the window is activated
and when a selected installed game's directory is missing.

## Git

- Work on feature branches; the owner decides when to commit and push — ask first.
- **Never** end commit messages with a `Co-Authored-By` trailer.
- Commit messages: English, imperative subject, body explains why.
- GitHub writes (issues, comments, PRs, pushes) only when the owner explicitly asks for them.
- License: GPL-3.0-only. Third-party notes in `THIRD-PARTY-NOTICES.md`.
