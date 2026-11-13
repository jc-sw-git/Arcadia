# Product decisions: First start

## Launcher page (#139)

- Arcadia never takes a launcher on just because it is installed. While `settings.json` has no `ActiveLaunchers` (also
  after an update from a version without it), a page "Welche Launcher soll Arcadia nutzen?" stands in place of the
  library, pattern "Leiste".
- "Gefunden" switched on with their games, "Nicht gefunden" greyed out and not switchable; ◀ ▶ (and A, a click on the
  switch) switch. The info panel shows above the preview "Launcher-Reihenfolge" as in the launcher settings, below the
  focused launcher (games, location, keyword), "Später änderbar" and "Fertig"; the header has date and clock like
  every page.
- ≡, Enter or a click on "Fertig" lead on to "Datenquellen"; B does nothing. Only on/off is chosen there, order and
  looks in the settings.
- `--demo` skips the first start (all launchers active, nothing saved); `--first-run` shows it again (with `--demo` the
  answer holds for the session only).

## Step "Datenquellen" (#497)

- After the launcher page, skippable: the SteamGridDB key and the IGDB access with the same rows, texts and info as
  the settings' "Datenquellen", then "Überspringen" ("Fertig" once a key or access is there). It is the settings page
  with that one category, the header says "Einrichtung", no ✕.
- While it shows, the library already holds only the launchers chosen, so a key taken there starts its fetch at once,
  in the background, and only for those; ending the step waits for no request and starts nothing again.
- B goes back to the launcher page, switches as left; ≡, Enter or the last row end the first start: only then is
  `ActiveLaunchers` saved. Skipped, Arcadia sends SteamGridDB and IGDB nothing.

## Continuing where it stopped (#497)

- The first start continues where it stopped, also after closing or a crash: the launcher switches, the page it was
  on and the launchers chosen are kept in `first-run.json` beside `settings.json` (written to a temporary file and
  renamed); keys are saved by their own stores the moment they are taken.
- A record that is missing or unreadable starts the first start again on the launcher page with the launchers found
  switched on (one log line); keys stored before show as stored. The record is deleted when the first start ends, and
  ignored once `ActiveLaunchers` is saved. `--demo` writes none.
