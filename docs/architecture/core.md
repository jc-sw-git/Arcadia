# Architecture: `Arcadia.Launchers.Abstractions` and `Arcadia.Core`

The other projects: [README.md](README.md).

## `src/Arcadia.Launchers.Abstractions`

What a launcher is made of, with no dependency on the rest; the future plugin interface for launchers.

- `ILauncherIntegration`: scanner, optional client, install monitor and running games, the launcher's
  `ProgramNames`, `Locate`, optional `FolderChoice`. `ILauncherFolderChoice`: a folder the user picks when the launcher
  isn't found, checked when picked.
- `ILauncherScanner`, `ILauncherClient`, `IInstallMonitor`, `IRunningGamesMonitor` (also the starts the launcher
  aborted, `RefusedStart`).
- Models: `Game` with its `GameType`, `LauncherInfo` with its `Logo` path and `GamesUseSteamInput`, `LaunchTarget`.
- `LibraryTypeOption`: a launcher's switch for software, tools or soundtracks.
- `ILauncherAccount`: a sign-in to the launcher's online account for its uninstalled games, shown as group "Konto" on
  its page. `IWebPages`: Arcadia's own browser for a launcher's web pages (a sign-in on its own page), its cookies kept
  in Arcadia's data folder.

## `src/Arcadia.Core`

**Launchers**
- `LauncherRegistry`: the registered launchers; reads see only the active ones in tab order, `Locate` finds them off
  the UI thread. `LauncherSelection`: active and inactive launchers from `AppSettings.ActiveLaunchers`.
- `FirstRunSetup`: the first start's question which launchers are active (#139): due while no list is saved,
  `--first-run`, `--demo`.
- `KnownLaunchers`, `LauncherNames` (rules for a launcher's own name and keyword), `LogoFile` (a picked logo checked
  by its content; an SVG may not load or run anything).

**Library**
- `LibraryService`/`LibraryRefresher`: scan and rescan; every active launcher keeps a library, also empty or not
  found, `ScannedAt`. `LibraryTypes`: which types a tab shows by the launcher's switches, counts per type.
- `GameSorter`, `PageSizes`/`PageWindow`, `Carousel` (detail page neighbours).
- `InstallTracker`: installs started from Arcadia or in the launcher. `Changelog`: release notes between versions.

**Running games**
- `RunningGamesTracker`: which games run and which of them Arcadia started; off the UI thread, every 2 s also in the
  background; a start that was refused or timed out.
- `ProcessGames`: running games by their processes, only for launchers without `IRunningGamesMonitor`; each round
  looks only at new processes.
- `GameFolders`: install folders of the library, to tell a game in front; paths compared in one spelling.
  `LaunchedGames`: a game Arcadia started whose path can't be read.

**Search and on-screen keyboard**
- `SearchQuery` (prefix parser), `TextWithCaret` (the search field's text and caret), `GameSearch` (hits by relevance
  or a sort order; titles folded and sorted once, so a key press is one ordinal pass), `LauncherScope` (chips and
  prefix in step), `HitWindow` (the hits on screen, turned with LT/RT).
- `KeyboardLayout` (the real layouts, three levels per key), `KeyGrid` (focus over its keys), `FullKeyboard` (a layout
  as the real keyboard's keys, ISO or ANSI), `KeyboardPlacement` (the mouse mode keyboard's edge and size on a
  monitor).

**Settings and navigation**
- `AppSettings`/`SettingsStore`/`SettingsService`: the one shared copy of the settings; `LauncherPreferences` per
  launcher: name, keyword, logo file, folder. `MouseModeSettings`, `ReturnWithViewSettings`.
- `ListFocus`: focus in a column of rows that come and go, tabs that wrap. `ValueCycle`: a ◀ ▶ selector whose step
  past one end comes in at the other.

**Diagnostics**
- `LogLocation`: `arcadia.log` in `%APPDATA%\Arcadia\logs`, beside the settings. `LogPrivacy`: the Windows account's
  name taken out of paths in log lines and stack traces.
