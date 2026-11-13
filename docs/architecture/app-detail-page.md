# Architecture: the detail page in `Arcadia.App`

The rest of the App: [app.md](app.md); the product rules: `docs/product/detail-page/`.

- `GameDetailViewModel`: the carousel, its `DetailCoverViewModel`s placed by `Controls/DepthSlot`, and the shown
  `GameDetailPageViewModel` (buttons, rail of `DetailAreaViewModel`s with `DetailRowViewModel`s from `DetailAreas`,
  focus as `DetailFocus`, which a turn of the carousel carries over), `Views/GameDetailView.axaml`; covers in the grid
  are `GameTileViewModel`s.
- "Übersicht" (`Areas[0]`) shows the game's facts and the quick selection (#500): its rows are rows of the other areas
  built anew on the same game actions, chosen by `DetailAreas.QuickRows` from the ids in
  `QuickSelectionSettings` (Core), in area order, at most 5. A row and its entry change the same action, so their values
  follow each other. Below the entries "Übersicht anpassen" takes the focus as one more row (`DetailFocus.OnCustomize`).
- `CustomizeOverviewViewModel`: the dialog "Übersicht anpassen" (ticks, the limit of 5, the question before
  "Für alle Spiele übernehmen"), drawn by `Views/CustomizeOverviewView`. Main hosts it
  (`MainViewModel.CustomizeOverview`, `ICustomizeOverviewHost`), stores each tick at once and rebuilds the page behind.
- Own titles (#496): `ChangeTitleViewModel` ("Titel ändern", a `TextEditorViewModel` on the settings' keyboard, drawn by
  `Views/ChangeTitleView`, which `MainWindow` keeps like the search so it opens without being built), hosted by
  `MainViewModel.Titles.cs`, which also follows `user-data.json` (status message, failed write) and keeps the library's
  focus on a game renamed from its menu.
  `GameDetailPageViewModel.OriginalTitle`/`ShowsOriginalTitle`: ⧉ in `GameDetailViewModel`; R3 there opens the
  library's filter (`LibraryViewModel.OpenFilterCommand`), and `Carousel.FindOrNext` keeps the page on a hit (#530).
- Own tags (#495): `TagWindowViewModel` ("Tags" of one game) and `TagListViewModel` ("Eigene Tags" in "Datenquellen",
  with `TagGamesViewModel` for a tag's games) share `OwnTagsDialogViewModel` (rows, name on the settings' keyboard,
  questions), drawn by `Views/TagWindowView`, `Views/TagListView`, `Views/TagDialogPartsView`. `MainViewModel.OwnTags.cs`
  hosts both (`IOwnTagsHost`), refreshes every page on `OwnTags.Changed`, and keeps the tag list behind a detail page
  opened from it for B. `DetailMetadata` puts own tags (`DetailTag.IsOwn`) before the providers'.
- While any dialog is open (`MainViewModel.IsDialogOpen`) the page behind draws no focus ring.
- Metadata (#493): `DetailMetadata` turns Core's `MergedMetadata` (`MainViewModel.MetadataOf`) into the info line,
  "Übersicht"'s description and tags and the facts of "Infos" (`DetailAreaViewModel.InfosKey`, no rows).
  `MainViewModel` hands `MetadataService.Changed` to `GameDetailViewModel.ShowMetadata`. "Infos" is never entered:
  while it has the focus in the rail (`GameDetailPageViewModel.IsReadingInfos`) RS raises `InfoScrollRequested` and the
  view scrolls `InfosScroller`, elsewhere the focused row's info box (#530).
  An age rating's colour is `AgeRatingTones.Of` (a UI matter, not Core's); `Controls/AgeRatingBadge` draws it with the
  brush `AgeRating{Tone}` from `Theme.axaml` (#541).
