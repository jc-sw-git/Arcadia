# #506 B When Arcadia searches artwork: triggers, cancel, sources on and off, order

Part B of #506 (scenarios 6–14 and their failure cases). Part A (detail page, info box) and part C (waiting list)
have their own files; part B uses the IDs T506-40 to T506-79.

**Status:** Automated: T506-40 to -72 · QE: acceptance tests T506-40, -41, -50, -54, -60, -70, app run under Xvfb · Open for the owner: T506-73, T506-74, T506-75, T506-76, T506-77, T506-78, T506-79

Automated tests use made-up games, names, ids and images and a fake SteamGridDB that counts every request; nothing
reaches the real one. "Shown everywhere" means library tile, preview, detail page and search hit. "A run" is one pass
of the artwork service over a set of games; "known" means Arcadia has seen the game in its library before.
Decisions Q1 to Q7 (bottom) close gaps in the specification and are agreed with the owner; the cases
follow them.

**When a run starts (scenarios 6–8)**

- [x] **T506-40** Given a usable key and a library of known games, when a rescan brings one new Steam game, then
  SteamGridDB is asked only for that game (the fake counts requests for exactly its id, none for the others), silently:
  no toast, no status message; its images are shown everywhere without the focus moving. · E2E: yes
- [x] **T506-41** Given a usable key and no new game, when Arcadia's window becomes active again, a game ends, an
  install or an uninstall finishes (each triggers a rescan), then nothing is requested over the network: 0 requests to
  SteamGridDB, to Steam's image server and to Epic's image URLs, also for games still missing images. Local launcher
  files (Steam's `grid` folder and library cache) are read at every scan, so an image Steam newly cached is shown at
  once (Q7). · E2E: yes
- [x] **T506-42** A new game whose search found nothing, hit no network, a timeout, a 5xx or throttling that ended the
  run is never searched again on its own: not at the next rescans, not after a restart; "Jetzt suchen" asks it again.
  · E2E: no (unit test with fake time)
- [x] **T506-43** A new game without a launcher id, or one SteamGridDB doesn't know by id, is searched by name in the
  same one run (#491's rules: taken, waiting list or nothing); several hits put it on the waiting list without a toast.
  · E2E: no (unit test)
- [x] **T506-44** A game arriving while there is no key, a rejected key or SteamGridDB is off becomes known without a
  request (fake counts 0) and is not searched later on its own when the key or the source arrives, except through
  T506-46 or "Jetzt suchen" (Q1). · E2E: no (unit test)
- [x] **T506-45** A launcher switched on with 200 games Arcadia never saw: each is new and searched once in the
  background, the library stays usable (T489-24's budgets). The same launcher switched off and on again: its games are
  known, 0 requests, and come back with the images they had (a game leaving the library keeps all its images, set by
  hand or not). · E2E: no (view model test)
- [x] **T506-46** Given no key, when I enter a valid key (typed or pasted), then the toast reads "Schlüssel gespeichert.
  Fehlende Bilder werden gesucht." and one run over every game still missing a cover or background starts; the row
  shows "Suche läuft · n von N Spielen". The same from a rejected key to a valid one, and after "Schlüssel löschen" and
  entering a key again. · E2E: yes
- [x] **T506-47** Given a working key, when I enter another valid key, then the usual saved toast, no run, 0 requests
  besides the key check. Entering the same key again: no run either. · E2E: no (view model test)
- [x] **T506-48** A key entered without network (kept without a warning, #489) still starts the run of T506-46; the run
  stops SteamGridDB at the first failure as T489-21 does, silently, and the games stay missing until "Jetzt suchen"
  (Q2). · E2E: no (unit test)
- [x] **T506-49** Every run writes one line to `arcadia.log` at its start and end with its trigger (new games, first
  key, "Jetzt suchen", "Neu laden") and the number of games, and on cancel "abgebrochen"; never the key, never a game
  name. A rescan without a new game writes none. (So the owner can see on his PC that rescans search nothing.)
  · E2E: no (unit test on the log sink)

**Settings changes start nothing (scenario 9)**

- [x] **T506-50** Given launcher first and a launcher cover cutting 10 % plus a SteamGridDB cover on disk that fits,
  when I change "Zuschneiden erlaubt" from "Normal" to "Wenig", then the SteamGridDB cover is shown everywhere at once,
  0 requests; back to "Normal": the launcher cover again at once. · E2E: yes
- [x] **T506-51** A stricter "Zuschneiden erlaubt" that makes an image no longer fit, with no fitting image on disk:
  the first source's image stays, cropped (#13's fallback); 0 requests, also at the next rescans; the next "Jetzt
  suchen" and a new game use the new value. · E2E: no (unit test)
- [x] **T506-52** A changed order or a source switched on: images on disk and the launchers' own images are picked
  again at once in the new order, 0 requests; images set by hand stay. · E2E: no (unit test)
- [x] **T506-53** The info texts of "Zuschneiden erlaubt", the order and the switches no longer say "bei der nächsten
  Suche" but what T506-50 to -52 do (wording in the PR's text list, `docs/product/ui-texts.md`). · E2E: no (view model
  test on the texts)

**Cancel (scenario 10)**

- [x] **T506-54** Given a run started with "Jetzt suchen", then the row's button reads "Suche abbrechen"; A stops the
  run within one request (no further request after the one in flight, which is dropped); the row shows the counts of
  what is still missing; covers and backgrounds found so far stay everywhere; games put on the waiting list stay
  there. · E2E: yes
- [x] **T506-55** "Alle Artworks neu laden" while running reads "Neu laden abbrechen", A stops it the same way; images
  already replaced stay replaced, the rest keep their old image (never a placeholder in between). · E2E: no (view model
  test)
- [x] **T506-56** After a cancel the next "Jetzt suchen" asks the games still missing, and those already found in the
  cancelled run are not asked again unless still missing. · E2E: no (unit test)
- [x] **T506-57** A background run (new games, first key) also shows "Suche läuft" and "Suche abbrechen" and can be
  cancelled (Q3); new games it didn't reach are searched at the next "Jetzt suchen" only. · E2E: no (view model test)
- [x] **T506-58** A pressed twice fast, or "Suche abbrechen" pressed just as the run ends: no second run, no exception,
  the button reads "Jetzt suchen" again; controller only, B leaves the settings as usual. · E2E: no (view model test)

**Sources on and off, order (scenarios 11–13)**

- [x] **T506-59** "Reihenfolge für Artwork": ◀ ▶ on a source switch it "An"/"Aus" (held: no repeat flicker, one switch per
  press), saved in `settings.json`, kept after a restart; default both on. · E2E: yes
- [x] **T506-60** Launcher images off: the launchers' images disappear everywhere at once, SteamGridDB images on disk
  take their place, otherwise the placeholder; the cache files stay on disk; on again: the same images come back at
  once, 0 downloads. The same for SteamGridDB off and on. · E2E: yes
- [x] **T506-61** Both off: placeholders everywhere except images set by hand (suggestion or own file, also those that
  came from a source now off); 0 requests at rescans, new games and "Jetzt suchen". · E2E: no (unit test)
- [x] **T506-62** SteamGridDB off: no request by id or by name (new game, first key, "Jetzt suchen", "Neu laden"),
  "Warten auf deine Auswahl" greyed out with its count kept, the key stays stored and shown ("Verbunden · endet auf
  …"); "Jetzt suchen" greyed out with the reason in the info box (Q4). Switching it on: waiting games are back, no
  run starts. · E2E: yes
- [x] **T506-63** Entering a first valid key while SteamGridDB is off: the key is saved, no run starts, the toast
  doesn't say "Fehlende Bilder werden gesucht" (Q1). · E2E: no (view model test)
- [x] **T506-64** Reorder by controller: L3 (C) lifts a source, ▲ ▼ move it (no wrap at the ends, as in the launcher
  list, Q6), L3 drops it and saves; B while lifted puts it back where it was, saves nothing, and doesn't leave the page;
  the focus stays on the moved source; an off source keeps its place and can be moved. The images follow the new
  order at once (T506-52). · E2E: yes
- [x] **T506-65** Reorder by mouse: dragging a source onto the other's place moves it, as in the launcher list; a click
  no longer moves it (#489's "Verschieben ⇅" is gone). · E2E: yes (window test)
- [x] **T506-66** The button hints match the level: not lifted ◀ ▶ "An/Aus", L3 "Anheben"; lifted ▲ ▼ "Verschieben",
  L3 "Ablegen", B "Abbrechen" (texts as listed in the PR). · E2E: no (view model test)

**"Artwork ändern" with a source off (scenario 14)**

- [x] **T506-67** SteamGridDB off: "Artwork ändern" shows no SteamGridDB tile and makes no request; its notice says that
  SteamGridDB is switched off and "Zu den Datenquellen ›" opens "Datenquellen" on the SteamGridDB source row (not on
  "Schlüssel"); B goes back to the detail page. · E2E: yes
- [x] **T506-68** Launcher images off: no launcher tile; an image in use that is set by hand from the switched-off
  source stays shown and marked as in use (Q5). Own files are always offered. · E2E: no (view model test)

**Failure cases**

- [x] **T506-69** Arcadia closed during a run started by "Jetzt suchen": it closes without waiting for a request,
  found images stay; after the start 0 requests (no run continues on its own). Closed during a background run for new
  games: the new games it didn't reach are still new and searched once after the next start (Q3). · E2E: no (unit test)
- [x] **T506-70** First start with this version and an existing library (no stored known games yet): every game in the
  library counts as known, 0 requests, also for games missing images; a game that left the library before and comes
  back later is not new. The stored list of known games is missing, empty or damaged: one log line, all games now in
  the library count as known, nothing is searched, Arcadia keeps running; it is written as a temporary file, then
  renamed. · E2E: yes
- [x] **T506-71** A new game arriving while a run is going (any trigger): it is asked exactly once, in that run or
  right after it, never twice; a cancel of the run also covers it if it joined (then T506-57). · E2E: no (unit test)
- [x] **T506-72** SteamGridDB switched off during a run: no request after the one in flight; the run ends with what it
  found, the waiting list keeps what it had; switching it on again starts nothing. · E2E: no (unit test)

**For the owner (own PC, real SteamGridDB, own library)**

- [ ] **T506-73** **Owner:** with a key, bring Arcadia to the front a few times, start and end a game, install and
  uninstall a small game: no search (no "Suche läuft" in "Datenquellen", `arcadia.log` has no run line for these).
  The newly installed game, if it wasn't in the library before (e.g. a free game added to the account), gets its
  images on its own. · E2E: no (real launchers and SteamGridDB)
- [ ] **T506-74** **Owner:** "Schlüssel löschen", then enter the real key: toast "Schlüssel gespeichert. Fehlende Bilder
  werden gesucht.", a run over the missing games; "Suche abbrechen" midway: what was found stays; "Jetzt suchen"
  goes on with the rest. · E2E: no (real SteamGridDB)
- [ ] **T506-75** **Owner:** first start of this version on the own library: nothing is searched (log, "Datenquellen"
  row). · E2E: no (own library and stored state)
- [ ] **T506-76** **Owner:** switch SteamGridDB off and on, then the launcher images off and on: the library changes at
  once, back without waiting; images set by hand stay. · E2E: no (real images, how it looks)
- [ ] **T506-77** **Owner:** reorder the sources with the Steam Controller (L3, ▲ ▼, L3, once with B) and once with the
  mouse; the images follow. · E2E: no (real Steam Controller)
- [ ] **T506-78** **Owner:** change "Zuschneiden erlaubt" on the own library: images already there change at once,
  no search starts. · E2E: no (real images)
- [ ] **T506-79** **Owner:** close Arcadia during "Jetzt suchen", start it again: found images there, no search runs on
  its own. · E2E: no (real shutdown on Windows)

**Touches:** T489-14 (reorder by A, replaced by T506-64/-65), T489-17 (nothing changes at once, replaced by
T506-50 to -52), T489-19 (run after every scan, replaced by T506-40/-41; its texts stay), T489-23 and T491-25 (next
scan/start continues, replaced by T506-69), T491-8 ("asked again at the next scan", replaced by T506-42).

**Decisions on the gaps (agreed with the owner)**

- **Q1** A game that arrives while there is no key, a rejected key or SteamGridDB off: recommended it becomes known
  and is searched only by the first usable key's run (T506-46) or "Jetzt suchen"; switching SteamGridDB on starts
  nothing (scenario 9), and a first key while it is off starts no run either.
- **Q2** A key entered without network: recommended it still counts as the first usable key and starts the run, which
  ends silently at the first failure; the alternative (wait until a check succeeds) needs a pending state nobody sees.
- **Q3** Background runs (new games, first key) show progress and can be cancelled like "Jetzt suchen"; recommended
  yes, one run looks the same whatever started it. A new game a cancel didn't reach waits for "Jetzt suchen"; one a
  close of Arcadia didn't reach stays new and is searched once after the next start (the player didn't say stop).
- **Q4** With SteamGridDB off "Jetzt suchen" is greyed out with the reason; recommended, since A would do nothing.
  "Alle Artworks neu laden" stays usable (it re-picks launcher images and those on disk).
- **Q5** In "Artwork ändern" an image set by hand from a source now off stays shown as the one in use; the other
  images of that source are not offered.
- **Q6** Reordering doesn't wrap at the ends, as the launcher list.
- **Q7** The rule of scenarios 6 and 7 holds for every network request, not only SteamGridDB: Steam's image server and
  Epic's image URLs are asked only for new games and at "Jetzt suchen"/"Neu laden"; local launcher files at every scan.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T506-40 to -45, -49 to -52, -54 to -57, -60, -61, -68 to -72 | 2026-11-12 | Claude | PR branch, Debug, Linux cloud container; made-up games and images, fake SteamGridDB and image server | ✅ `ArtworkSearchTriggerTests` (Core); T506-60 red once: switching the launcher images off dropped the SteamGridDB cover kept aside | fixed in `ArtworkService.UpdateGameAsync` |
| T506-46 to -48, -53, -58, -59, -62 to -64, -66, -67 | 2026-11-12 | Claude | PR branch, Debug, Linux cloud container, fake SteamGridDB | ✅ `DataSourcesTests` (App) | – |
| T506-65 | 2026-11-12 | Claude | PR branch, Debug, headless window, Linux cloud container | ✅ `Window/DataSourcesViewTests` | – |
| T506-40, -41, -70 | 2026-11-12 | Claude (QE) | PR head c68358a, Debug, Linux cloud container; real Steam and Epic integrations on recreated folders, made-up ids, fake SteamGridDB and image server (Steam's server answers 404) | ✅ `ArtworkSearchTriggerAcceptanceTests`; red once: `GoesOnlineFor` always true (-40, -41), `KnownGames.HoldsArtwork` always false (-70) | – |
| T506-50, -54, -60 | 2026-11-12 | Claude (QE) | PR head c68358a, Debug, Linux cloud container, fake SteamGridDB and image server | ✅ `DataSourcesTests.Acceptance506.cs`; red once: `SettingsChanged` starting no run (-50, -60), `CancelSearch` not cancelling (-54) | – |
| Own app run | 2026-11-12 | Claude (QE) | PR head c68358a, Debug, `--demo` under Xvfb, keys for the controller | "Datenquellen": sources, a source lifted and moved (C, S), hints and info box | info box of a source row cut at the bottom (finding) |
