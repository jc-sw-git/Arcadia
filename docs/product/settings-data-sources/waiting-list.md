# Product decisions: "Datenquellen", search by name and the waiting list

Index and the category's frame: [README.md](README.md).

## Search by name and "Warten auf deine Auswahl" (#491)

- The name sent is the shown title without ™ ® ©, trimmed, once per game and run. A hit "matches" when its name is the
  same ignoring case, punctuation and ™ ® ©. One matching hit, or one result at all, is taken without a question;
  several matching, or none matching but several results, are "several hits" (at most 8, SteamGridDB's order). No
  result: the launcher image or the placeholder, searched again at the next "Jetzt suchen".
- "Bei mehreren Treffern" (group "ARTWORK" of the tab "Artwork"; the tab "Metadaten" has its own, #538): "Selbst
  auswählen" (default) puts the game on the waiting list, "Ersten nehmen" takes the first hit. Switching to "Ersten
  nehmen" while games wait gives each of that kind its first hit at once (one toast); switching back puts none back.
- Before a hit is offered or taken, SteamGridDB is asked whether it has an image of the kind sought (#506); a hit
  without one is dropped, and the rules above count only the hits left (one left: taken; none: no result). "Sought" is
  what the game is missing or has that doesn't fit and wasn't set by hand: cover, background or both; with both a hit
  needs a cover. A failed check stops SteamGridDB for the run (one request, not one per hit).
- Indented below, always shown, "Warten auf deine Auswahl" with the count; greyed out with no game waiting or with
  "Ersten nehmen". A opens the screen with two levels, only with the games of its tab's kind. Games list (left; each
  game's name and launcher, no hit count): ▲ ▼ choose a game, round at the ends; A goes into its hits; B closes (undecided games stay in their order). Hits (right): "Gesucht: Cover und Hintergrund" /
  "Gesucht: Cover" / "Gesucht: Hintergrund" (decided again when shown), the field "Eigener Suchbegriff" with
  "Zurücksetzen", and a grid of the hits' images of that kind (cover when both; five covers or three backgrounds a row; names wrap to two lines, the focused hit shows its name in full, a mouse hover shows it as a tooltip).
  ◀ ▶ ▲ ▼ go round in rows and columns, the field's row counts as each column's top; A takes the hit and goes back to
  the games list on the next waiting game, B goes back without; Y skips the game on both levels. Until chosen a game
  keeps its launcher image or placeholder.
- Hits not yet checked (a waiting list from before #506, a failed check) are checked when the game is shown, never in
  the background: "Treffer werden geprüft …" while they are, "Keine Treffer" when none is left (the game stays until
  skipped). Checks and images start only once the focus rests on a game (150 ms); moving on cancels them.
- "Eigener Suchbegriff" shows the game's search name; A opens the on-screen keyboard prefilled, ↵ searches, B keeps the
  term. Its results (the first 25 with an image of the sought kind) replace the hits; "Zurücksetzen" (greyed out with
  the search name in the field) brings back the search name and its hits. The term isn't stored. Without network
  "SteamGridDB nicht erreichbar", with a rejected key the key notice.
- The games list and the grid show a scroll bar and fade at the edge where more follow.
- A chosen hit is the game's SteamGridDB game from then on; its images are automatic (order, "Zuschneiden erlaubt" and
  "Alle Artworks neu laden" apply), an image set by hand stays. A skipped game is never searched by name again.
  "Artwork ändern" offers the chosen game's images; for a waiting or skipped game none from SteamGridDB.
- The choice, a skip and the waiting list are kept per game across restarts and a game leaving the library and coming
  back.

## Metadata entries (#494, #538)

- The tab "Metadaten" has its own "Bei mehreren Treffern" and "Warten auf deine Auswahl" (group "METADATEN"); a
  `settings.json` from before keeps the artwork's choice for both until one is changed. A game IGDB finds only by name
  with several hits waits there, with its launcher data until chosen; a game waiting for both kinds is on both lists,
  each decided on its own. The metadata list shows its games also while IGDB is switched off or its access is gone (a
  choice needs no request and shows once IGDB is on), in the order they came.
- A metadata entry's hits: "Gesucht: Metadaten", IGDB's hits in its order with IGDB's cover (or the placeholder; only
  while the access is usable), name and year (none without a date). "Eigener Suchbegriff" searches IGDB's main games
  (the first 25). A takes the hit: its fields and a corrected title show everywhere at once, without a request; the game
  is asked by that IGDB game from then on. Y skips: launcher data stay, never searched by name on IGDB again, also not
  at "Alle Metadaten neu laden". While it waits it isn't searched again either.
- Choice, skip and waiting entries are kept in the game's metadata cache file, across restarts and a game leaving the
  library; a damaged file counts as not yet searched.
