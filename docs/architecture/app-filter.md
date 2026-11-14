# App: the library's filter

The modal "Filter" (#44, part A #465, part B #466) over the library; what it does: `docs/product/library.md`.

- `Filter/FilterModalViewModel` (R3): the categories left, a `FilterPane` per category right, the choice so far
  (`Chosen`, applied when it closes) and the hits on top. Main hosts it; the library opens it with a
  `FilterModalSetup` (the tab's entries, their `FilterFacts`, the `FilterOffer`, this year, whether a metadata run goes
  on: `MetadataService.IsBusy`, which also covers reading the launchers' own files, unlike `Progress.Running`) and saves
  the choice when it closes.
- Counting runs off the UI thread (`Task.Run`), the latest run wins and is shown on the UI thread; `Counting` is that
  task (the test driver waits on it). The first count runs at once, so the modal opens with its numbers.
- Panes, each with its focus, its values and its footer hints (`MoveText`, `AcceptText`, `SearchText`, `PageText`, …):
  - `FilterOptionsPane`: "Installiert" (radios) and "Typ" (a grid of three).
  - `FilterListPane`: genre, tags, developer, publisher. Rows of three in a window of 9 rows (4 while writing) that the
    view model moves (no virtualizing panel; `VisibleRows` changes row by row, so a step builds at most the rows
    scrolled in); own tags first under their heading; "Häufigste zuerst" or A–Z with
    letter leads and a letter rail; the order is set when the list is arranged (opening, L3, search), not at each
    choice. The value search writes with the settings' on-screen keyboard (`IFilterPaneHost.Write`).
  - `FilterYearPane`: the bar chart, the quick picks (`YearPicks`), "von" and "bis".
  - `FilterRatingPane`: the system chips, its ratings as tiles; another system clears the ratings.
- `FilterFactsSource` (Main): each game's `GameFacts`, cached by key, renewed when metadata or own tags change; the
  library re-filters then (`LibraryViewModel.FactsChanged`), keeping the focused game or going to the next hit.
- `Views/FilterModalView`: a `DataTemplate` per pane; the wheel over the list moves its window. Styles `filter…` in
  `Theme/Theme.axaml`.
