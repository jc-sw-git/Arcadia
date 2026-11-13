# Architecture: artwork in the App

Covers and backgrounds (#13, #487); the cache and its run are Core's (`core.md` → Artwork). The other parts of the App:
[app.md](app.md).

- `Services/ArtworkFolder`: the cache in `%LOCALAPPDATA%\Arcadia\artwork` and the `ArtworkService` with an `HttpClient` of
  its own that sends no cookies; `App.axaml.cs` passes it in through `ArcadiaParts.Artwork` (the tests' in their folder).
  It also makes the `SteamGridDbKey`: DPAPI for the current user on Windows, memory only elsewhere.
- Category "Datenquellen" (#489): `Settings/DataSourcesSettingsViewModel` switches its two tabs (#538, `IDataSourcesTab`;
  `DataSourcesHeadViewModel` above the rows, LT/RT) and shows only the chosen tab's rows. `ArtworkSourcesTab` holds
  `SteamGridDbKeyRows` (key, paste, key page, test, delete; the first usable key searches), `ArtworkSourceRows` (the sources as `ArtworkSourceRowViewModel`
  switches with L3 lifting, missing images with progress and cancel; every change calls
  `ArtworkService.SettingsChanged`, #506) and
  `ArtworkReloadRows` ("Alle Artworks neu laden" with its question, the overwrite switch); `MetadataSourcesTab` holds
  `IgdbAccessRows` (#494: client id, secret, paste, Twitch's console, test, delete), `MetadataSourceRows` (the metadata
  order, a box per launcher in "Launcher-Daten"'s info; `SourceOrder<T>` lifts and moves for both orders), `MetadataRows`
  (#493: "Fehlende Metadaten" with "Jetzt suchen", "Alle Metadaten neu laden") and "Eigene Tags". Each tab has its own
  `SeveralHitsRows` (by `WaitingKind`). It reaches the
  window through `IDataSourcesHost` (`MainViewModel.DataSources.cs`: toasts, keyboard, `IClipboardText` and
  `IWebBrowser` from `Services/ExternalApps`); the window drags a source as a launcher, through `IDraggableRows`. `StatusBarViewModel.WatchSteamGridDb`/`WatchIgdb` show a rejected key or access in the chip.
- `LibrarySession` hands every read library to the artwork service and the shown entries to `MetadataService` (#493;
  `Services/MetadataFolder`: its own client without cookies or redirects, the `IgdbAccess` with DPAPI on Windows; `MainViewModel.Metadata.cs`: its errors,
  the reload's warning, `MetadataOf`, `MetadataChanged`); `MainViewModel` gives each new tile its `GameArtwork` (with
  the install and run state, `ShowStates`) and, on `Changed`, the shown tiles theirs. `GameTileViewModel.Cover`/`Hero`.
- `Controls/ArtworkImage`: draws one image cropped centred with rounded corners. It loads only when it is drawn (a hidden
  slot decodes nothing), at the size it has on the screen (layout size × the `Viewbox`'s scale × the screen's scaling); a render transform alone draws nothing anew, so `Controls/DepthSlot` redraws its cover's images when the cover arrives at its slot (#532),
  drops the image at once when its slot shows another game, and says `IsShowing`, which hides the placeholder's
  monogram and title. Badges and the shade lie above it in the views; on the detail page the background fills the
  screen under the scrims `DetailScrimSide`/`Top`/`Bottom` (`Theme.axaml`), which keep title and buttons readable.
- `Services/ArtworkLoader`: decodes off the UI thread, a few at a time; an image it can't decode is logged and reported
  to the service as broken, which loads it again at the next scan. `Decoded` and `Loaded` let tests count.

Measured with `--demo --demo-games 5000` (Release, Linux container, made-up images): start and page turns as without
images. Paging through the library raises the resident memory on Linux by glibc's allocator keeping the decoder's freed
buffers (`MALLOC_MMAP_THRESHOLD_=131072` keeps it flat); no managed or native leak. The budget is measured on Windows.

"Artwork ändern" (#490): `ViewModels/ChangeArtworkViewModel` (tabs, grid focus with `GridFocus`, tiles
`ArtworkTileViewModel`, the notice `ArtworkNoticeViewModel`) over `ArtworkService.ByHand`; `Views/ChangeArtworkView`.
`MainViewModel.ChangeArtwork.cs` opens it from the game action (`IGameActionHost.ChangeArtwork`) as a dialog over the
page and gives it the file dialog (`ISystemDialogs`, mouse mode meanwhile) and the warning toast. `IsLoading` and
`LoadingText` (a timer of the App's `TimeProvider`) drive the loading state; the preview is a `Viewbox` over a canvas
in `GameDetailView`'s 1920 × 1080 places, so it follows the page's layout. The launcher's image in use is offered as a
copy among the offers waiting in the cache: taking another tile overwrites the file in use (#532).

"Warten auf deine Auswahl" (#491, metadata entries #494): `Settings/SeveralHitsRows` ("Bei mehreren Treffern", the
waiting row with its count) once per tab of "Datenquellen"; `ViewModels/WaitingListViewModel` (the games of one
`WaitingKind`, and the two levels) and `WaitingHitsViewModel` (one entry's hits: the grid, "Eigener
Suchbegriff" with the settings' on-screen keyboard, tile images off the UI thread once the focus rests), with
`ArtworkHitsViewModel` (what is sought, the hit checks, the tabs "Cover" and "Hintergrund" with a pick each, #539) over
`ArtworkService.ByName` (`CheckWaitingHitAsync`, `CheckHitAsync`, `SearchByTermAsync`, `HitImageAsync`, `WaitingMatchNow`;
`NameMatch.HeroGameId` keeps a background from another game) and `MetadataHitsViewModel` over `MetadataService.Waiting.cs`
(a list beside `WaitingHitFacts`, no images); `Views/WaitingListView` (click marks, double click takes). `MainViewModel.WaitingList.cs` opens
it over the settings; while open it takes every press before the settings, and real keys go to its field
(`MainViewModel.OpenTextEditor`).
