# Architecture: `Arcadia.Launchers.Abstractions` and `Arcadia.Core`

The other projects: [README.md](README.md).

## `src/Arcadia.Launchers.Abstractions`

What a launcher is made of, with no dependency on the rest; the future plugin interface for launchers.

- `ILauncherIntegration`: scanner, optional client, install monitor and running games, the launcher's
  `ProgramNames`, `Locate`, optional `FolderChoice` and `Program` (`ILauncherProgram`: the launcher's own program,
  closed and started again for the Steam Controller's drivers, its running game, its folder and its entries of other
  programs, which SISR's cleanup needs). `ILauncherFolderChoice`: a folder the user picks when the launcher
  isn't found, checked when picked.
- `ILauncherScanner`, `ILauncherClient`, `IInstallMonitor`, `IRunningGamesMonitor` (also the starts the launcher
  aborted, `RefusedStart`).
- Models: `Game` with its `GameType`, `LauncherInfo` with its `Logo` path, `GamesUseSteamInput` and `OwnsSteamInput`
  (the launcher whose program holds the Steam Controller: Steam), `LaunchTarget`. `LauncherIds`: the rule for a
  launcher id (a-z, 0-9, `-`).
- `LibraryTypeOption`: a launcher's switch for software, tools or soundtracks.
- `ILauncherArtwork`: a launcher's own covers and backgrounds per game (`ArtworkCandidate`: a file or an `https`
  address), best first (#487); `IArtworkLookup.StoreIdOf` the game's id at a store SteamGridDB knows (`ArtworkStoreId`, #489).
- `ILauncherAccount`: a sign-in to the launcher's online account for its uninstalled games, shown as group "Konto" on
  its page. `IWebPages`: Arcadia's own browser for a launcher's web pages (a sign-in on its own page), its cookies kept
  in Arcadia's data folder.

## `src/Arcadia.Core`

**Launchers**
- `LauncherRegistry`: the registered launchers; reads see only the active ones in tab order, `Locate` finds them off
  the UI thread. Rejects an integration with a broken id, parts of another launcher or a throwing identity;
  `SteamInputOwner` is the launcher that owns Steam Input, which the App and the controller support ask instead of
  naming Steam. `LauncherSelection`: active and inactive launchers from `AppSettings.ActiveLaunchers`.
- `FirstRunSetup`: the first start's question which launchers are active (#139): due while no list is saved,
  `--first-run`, `--demo`.
- `LauncherNames` (rules for a launcher's own name and keyword), `LogoFile` (a picked logo checked
  by its content; an SVG may not load or run anything).

**Library**
- `LibraryService`/`LibraryRefresher`: scan and rescan; every active launcher keeps a library, also empty or not
  found, `ScannedAt`. `LibraryTypes`: which types a tab shows by the launcher's switches, counts per type.
- `GameSorter`, `PageSizes`/`PageWindow`, `Carousel` (detail page neighbours).
- `GameFilter` (#44): a tab's filter, its hit counts per value, what is left of it `Within` a tab's `FilterOffer` (the categories that
  tell its entries apart) and a choice laid back `Over` the saved one; saved by name per launcher as `Settings/SavedFilter` in `AppSettings.LibraryFilters`.
- `InstallTracker`: installs started from Arcadia or in the launcher, and the update state a monitor reports live (#288). `Changelog`: release notes between versions.

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

**Artwork** (`Artwork/`, #13, #487, #489)
- `ArtworkService`: after each scan one background run brings every game's cover and background into the cache: the
  launcher's images (`ILauncherArtwork`) in their order, the first that fits `ArtworkFormat` (2 : 3, 3.1 : 1, 15 %
  cut away at most), else the first at all; downloads only for what is missing, 10 s each, the run stops downloading
  at the first network failure; removes the automatic images of games that left a library read whole. `Of(key)` is
  what the views show (`GameArtwork`), `Changed` says for which games it changed. The order of the sources and the
  tolerance come from `ArtworkSettings`; SteamGridDB is asked only where nothing cached fits, once per game and kind
  and session (`SearchAgain` asks again); `Search`/`SearchChanged`: the missing counts and the run's progress.
- `SteamGridDb/`: `SteamGridDbClient` (API v2, the key only in the `Authorization` header, 10 s, waits on 429, checks
  every answer), `SteamGridDbKey` (the key's shape, state None/Unchecked/Valid/Rejected, nothing sent without a
  usable key) and `SteamGridDbKeyStore` (encrypted file through injected protect/unprotect, else memory only).
- `ArtworkCache`: `artwork/<game>/cover|hero.*` plus `artwork.json` (source, origin, stamp, "set by hand"), every file
  through a temporary file and a rename; folder names from the key that never leave the cache; unreadable metadata
  makes the folder's images "set by hand". `ImageCheck`: a whole PNG, JPEG or WebP of at most 20 MB and 40 megapixels, and its size.

**Settings and navigation**
- `AppSettings`/`SettingsStore`/`SettingsService`: the one shared copy of the settings; `LauncherPreferences` per
  launcher: name, keyword, logo file, folder. `MouseModeSettings`, `ReturnWithViewSettings`.
- `ListFocus`: focus in a list that goes round at its ends (rows, menus, buttons, tabs), `Next` passing over items
  that can't take it. `ValueCycle`: a ◀ ▶ selector whose step
  past one end comes in at the other. `GridFocus`: focus in a grid cut into pages (library, search hits), round at
  the ends and across page edges.

**Diagnostics**
- `LogLocation`: `arcadia.log` in `%APPDATA%\Arcadia\logs`, beside the settings. `LogPrivacy`: the Windows account's
  name taken out of paths in log lines and stack traces.
