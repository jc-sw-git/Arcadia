# Architecture: the detail page in `Arcadia.App`

The rest of the App: [app.md](app.md); the product rules: `docs/product/detail-page.md`.

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
- While any dialog is open (`MainViewModel.IsDialogOpen`) the page behind draws no focus ring.
