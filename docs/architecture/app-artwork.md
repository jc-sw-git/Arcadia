# Architecture: artwork in the App

Covers and backgrounds (#13, #487); the cache and its run are Core's (`core.md` → Artwork). The other parts of the App:
[app.md](app.md).

- `Services/ArtworkFolder`: the cache in `%LOCALAPPDATA%\Arcadia\artwork` and the `ArtworkService` with an `HttpClient` of
  its own that sends no cookies; `App.axaml.cs` passes it in through `ArcadiaParts.Artwork` (the tests' in their folder).
- `LibrarySession` hands every read library to the service; `MainViewModel` gives each new tile its `GameArtwork` (with
  the install and run state, `ShowStates`) and, on `Changed`, the shown tiles theirs. `GameTileViewModel.Cover`/`Hero`.
- `Controls/ArtworkImage`: draws one image cropped centred with rounded corners. It loads only when it is drawn (a hidden
  slot decodes nothing), at the size it has on the screen (layout size × the `Viewbox`'s scale × the screen's scaling),
  drops the image at once when its slot shows another game, and says `IsShowing`, which hides the placeholder's
  monogram and title. Badges and the shade lie above it in the views; on the detail page `Border.heroScrim`
  (`HeroScrim` in `Theme.axaml`) keeps the name readable.
- `Services/ArtworkLoader`: decodes off the UI thread, a few at a time; an image it can't decode is logged and reported
  to the service as broken, which loads it again at the next scan. `Decoded` and `Loaded` let tests count.

Measured with `--demo --demo-games 5000` (Release, Linux container, made-up images): start and page turns as without
images. Paging through the library raises the resident memory on Linux by glibc's allocator keeping the decoder's freed
buffers (`MALLOC_MMAP_THRESHOLD_=131072` keeps it flat); no managed or native leak. The budget is measured on Windows.

"Artwork ändern" (#490): `ViewModels/ChangeArtworkViewModel` (tabs, grid focus with `GridFocus`, tiles
`ArtworkTileViewModel`, the notice `ArtworkNoticeViewModel`) over `ArtworkService.ByHand`; `Views/ChangeArtworkView`.
`MainViewModel.ChangeArtwork.cs` opens it from the game action (`IGameActionHost.ChangeArtwork`) as a dialog over the
page and gives it the file dialog (`ISystemDialogs`, mouse mode meanwhile) and the warning toast.
