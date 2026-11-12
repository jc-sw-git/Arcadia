# #506 C "Warten auf deine Auswahl": two levels, what is sought, only hits with images, own search term

Part C of #506 (scenarios 15–20 and their failure cases). Part A (detail page, info box) and part B (when artwork is
searched) have their own files; part C uses the IDs T506-80 to T506-119.

**Status:** Automated: T506-80 to T506-108, T506-110, T506-111, T506-116 · QE: – (T506-109 on Windows) · Open for the owner: T506-112, T506-113, T506-114, T506-115

Automated tests use made-up games, names, ids and images and a fake SteamGridDB that records every request (search by
name, the images per hit, the image downloads) and can answer slowly, throttle, fail or reject the key; nothing
reaches the real one. "Sought" is what the game is missing or has that doesn't fit "Zuschneiden erlaubt": cover,
background or both. "Has an image" means SteamGridDB lists at least one image of the sought kind for that hit (cover
when both are sought). Open questions Q1 to Q7 (bottom) are answered with their recommendation unless the owner says
otherwise; the cases follow the recommendation.

**Two levels (scenario 15)**

- [x] **T506-80** Given three waiting games, when "Warten auf deine Auswahl" opens, then the focus is on the first game
  in the games list; right of it the focused game's hits show without a focus ring in them; the footer names
  ▲ ▼ (choose a game), A (its hits), Y (skip) and B (close). · E2E: yes
- [x] **T506-81** In the games list ▲ ▼ (D-pad, left stick, W/S) move one game and go round at both ends; held they
  repeat (400 ms, then every 90 ms) and stop at the end once, a fresh press goes round (`controls.md`). The hits on
  the right follow the focused game. ◀ ▶ do nothing in the games list. · E2E: no (view model test with fake time)
- [x] **T506-82** A on a game moves the focus to its first hit; the game stays marked in the list; the footer now names
  the D-pad (choose a hit), A (take), Y (skip) and B (back to the games). · E2E: yes
- [x] **T506-83** In the hits grid ◀ ▶ move within a row and ▲ ▼ within a column, each going round at its ends (a row's
  last hit → its first; a column's last → its first, also when the last row is short: ▼ from a hit with none below
  goes to the column's first); held they stop at the end once. ▲ from the grid's top row goes to the field "Eigener
  Suchbegriff" (Q1). · E2E: no (view model test)
- [x] **T506-84** A on a hit takes it: the game leaves the list, its sought images come from that SteamGridDB game by
  order and tolerance and are shown everywhere without a rescan; the focus goes back to the games list on the next
  waiting game (the one below, or the first when it was the last); "Spiel 1 von n−1". · E2E: yes
- [x] **T506-85** B in the hits returns to the games list with the focus on the same game, nothing taken or skipped;
  A again starts at its first hit (Q2). B in the games list closes the screen and returns to the row
  "Warten auf deine Auswahl"; undecided games stay in their order. · E2E: yes
- [x] **T506-86** Y skips the focused game in both levels with the same effect as T491-16 (never searched by name
  again); in the hits the focus then goes back to the games list on the next waiting game, as after A (Q3). · E2E: yes
- [x] **T506-87** After the last game is taken or skipped from either level: "Alles erledigt. …" (T491-17), footer
  only "B Schließen", B returns to the row, now greyed out. · E2E: no (view model test)
- [x] **T506-88** Mouse and keyboard: a click on a game focuses it in the games list, a click on a hit takes it (as A
  on it), a click on the field opens the on-screen keyboard, a click on a footer hint does what its button does, the
  ✕ closes; the wheel scrolls the list or grid under the pointer and stops at the ends. Keyboard as elsewhere
  (Space = A, Esc = B, V = Y). · E2E: yes (window test: real clicks)

**What is sought (scenario 16)**

- [x] **T506-89** Three waiting games: one missing both, one missing only the cover, one whose background doesn't fit
  the tolerance. Above their hits stands "Gesucht: Cover und Hintergrund", "Gesucht: Cover", "Gesucht: Hintergrund";
  the tiles show the hit's cover (2 : 3) for the first two and its background (16 : 9) for the third; the sentence
  names what Arcadia loads after the choice ("… lädt Arcadia Cover und Hintergrund …", "… das Cover …", "… den
  Hintergrund …"). · E2E: yes (window test: layout and texts)
- [x] **T506-90** Taking a hit loads only what is sought: a game missing only the background keeps its cover (launcher
  or set by hand) and gets the hit's background; an image set by hand is never replaced. · E2E: no (unit test)
- [x] **T506-91** What is sought is decided when the game is shown: a waiting game that got its cover from the launcher
  meanwhile shows "Gesucht: Hintergrund" and background tiles. · E2E: no (view model test)

**Only hits with images (scenario 17)**

- [x] **T506-92** A name search with five hits, of which SteamGridDB lists a cover for three and none for two, with the
  cover sought: the game waits with those three hits, in SteamGridDB's order; the other two never show. With the
  background sought the same rule uses backgrounds; with both sought a hit needs a cover (Q4). · E2E: no (unit test)
- [x] **T506-93** Exactly one hit left after the check: taken without a question as T491-3 (not on the list). None
  left: the game keeps its launcher image or placeholder, is not on the list and not counted as skipped; it is
  treated as "no result" (part B decides when it is searched again). · E2E: yes
- [x] **T506-94** "Ersten nehmen" takes the first hit that has an image of the sought kind, not the first of all.
  · E2E: no (unit test)
- [x] **T506-95** While the hits of the focused game are checked, the grid says "Treffer werden geprüft …" and fills
  in SteamGridDB's order as answers arrive; a hit arriving doesn't move the focus once it is in the grid; A on a hit
  already shown works before all are checked. When the check ends with none left the grid says "Keine Treffer"; Y and "Eigener Suchbegriff" still work (Q5). · E2E: no (view model test with a slow fake)
- [x] **T506-96** Games that were on the waiting list before this change (unchecked hits) are checked when they are
  first shown, never at the start or in the background; their checked hits are kept, so showing them again asks
  nothing. One left: it shows alone and waits for A, nothing is taken while the player looks (Q6). · E2E: no (unit test)
- [x] **T506-97** Checking a hit fails (no answer within 10 s, throttling, server error, broken JSON): that hit is left
  out for now and checked again the next time the game is shown; the others show. A rejected key or a service that
  is down stops the check after the first failing request: the fake counts one request, not one per hit, and the grid
  shows the key notice or "SteamGridDB nicht erreichbar". · E2E: no (unit test with fake time)

**Own search term (scenario 18)**

- [x] **T506-98** In the hits, the field "Eigener Suchbegriff" above the grid shows the game's search name (shown
  title without ™ ® ©, trimmed); "Zurücksetzen" beside it is greyed out. ▲ from the top row reaches the field, ◀ ▶ move
  between field and "Zurücksetzen" (passing over it while greyed out), ▼ goes back to the grid. · E2E: yes (window test:
  layout and texts)
- [x] **T506-99** A on the field opens the on-screen keyboard prefilled with the term, caret at its end; ↵ runs the
  search with the edited term and puts the keyboard away; B puts the keyboard away without searching and keeps the
  original term in the field (Q7). The keyboard's own navigation is as `on-screen-keyboard.md`. · E2E: yes
- [x] **T506-100** The own search replaces the hits with at most 25 results, each checked as in T506-92, in
  SteamGridDB's order: the fake returns 40 results of which 30 have images; the grid shows the first 25 with images
  and the fake got no image request for results past those (Q4). "Zurücksetzen" is now active. · E2E: no (unit test)
- [x] **T506-101** A on a result of the own search takes it as T506-84; the game is remembered by that SteamGridDB game
  (T491-22), never searched by name again, and the term isn't stored: reopening another waiting game shows its own
  search name, a restart shows the original term. · E2E: yes
- [x] **T506-102** "Zurücksetzen" restores the search name and its original hits without a new name search (the fake
  counts no request) and is greyed out again; the focus goes to the grid's first hit. · E2E: no (view model test)
- [x] **T506-103** The term is sent as T491-6: one encoded path segment, ™ ® © removed and trimmed; terms with `/`,
  `\`, `?`, `#`, `%`, `&`, `..`, quotes, umlauts, emoji or 300 characters never change host, path or query; an empty
  or only-space term sends nothing and the grid says "Keine Treffer". · E2E: no (unit test with the recording fake)
- [x] **T506-104** Own search without network: "SteamGridDB nicht erreichbar"; with a rejected key: the key notice; no
  results or none with images: "Keine Treffer". In each case "Zurücksetzen" brings back the original hits and Y still
  skips. · E2E: no (view model test)
- [x] **T506-105** A new own search while one is still running or checking: the older one is cancelled and its late
  answers never show; B or closing the screen cancels it too (no request after the screen closed). · E2E: no (unit test)

**Scroll bar and fade (scenario 19)**

- [x] **T506-106** With 30 waiting games and 25 hits, the games list and the hits grid each show the settings rows'
  scroll bar and fade at the edge where more follows, none where everything fits (3 games, 4 hits); the focused game or
  hit is scrolled out of the fade with its focus ring, also after going round. Colours from `Theme.axaml`.
  · E2E: yes (window test: layout, fade and focus ring)

**Long names (owner feedback during implementation)**

- [x] **T506-116** Hits with long names: five covers a row, each name on up to two lines, then cut with "…"; the
  focused hit shows its whole name; a mouse hover over a hit shows its whole name as a tooltip, over a game in the
  games list its title. · E2E: yes (window test: line heights, tooltip)

**Smooth (scenario 20)**

- [x] **T506-107** Moving through the games never waits for the network: with the fake answering after 2 s, every
  step in the games list moves the focus at once and the hits area shows "Treffer werden geprüft …" or the cached hits.
  · E2E: no (view model test with a slow fake)
- [x] **T506-108** Images load only for the game the focus rests on: holding ▼ through 50 games, the fake gets
  requests (check and image downloads) only for the game where the focus stops, none for the ones passed; requests of
  a game the focus left are cancelled. Tiles decode at tile size. · E2E: no (view model test with fake time)
- [ ] **T506-109** Release build, made-up data, 100 waiting games with 8 hits and one with 25: opening the screen, every
  step in the games list, held and fast, and every step in the grid < 50 ms from input to visible focus move (`Bild:`
  lines), no stutter while images arrive; measured on Windows, numbers in the PR. · E2E: no (real rendering, Release
  timings; quality engineer's run)

**Failure cases, controller only**

- [x] **T506-110** Arcadia closed while hits are checked or an own search runs: it closes without waiting for a
  request; at the next start the list is as before, the checked hits kept, the unchecked ones checked when shown.
  · E2E: no (unit test)
- [x] **T506-111** Controller only, B back on every level: Settings → "Datenquellen" → "Warten auf deine Auswahl" A →
  ▼ → A → ▶ ▼ (field) → ▲ ▲ (field) → A → type on the on-screen keyboard → ↵ (focus on the first result) → ▶ → A
  (taken, back in the games list on the next game) → A → Y → A → B → B → B; no mouse or keyboard needed. · E2E: yes

**For the owner (own PC, real SteamGridDB, own library, gamepad; on main with parts A and B)**

- [ ] **T506-112** **Owner:** open "Warten auf deine Auswahl" with games waiting (if none wait: "Alle Artworks neu
  laden" first). Each game says what is sought, the tiles show that kind of image, no tile is empty; taking a hit
  gives the game the right cover or background. · E2E: no (real SteamGridDB, own library)
- [ ] **T506-113** **Owner:** for a game whose hits don't contain the right one, change "Eigener Suchbegriff" with the
  on-screen keyboard to a better name, take the right result; "Zurücksetzen" on another game brings its hits back.
  · E2E: no (real SteamGridDB, real names)
- [ ] **T506-114** **Owner:** with the Steam Controller and an Xbox-style pad: hold and tap through the games list and
  the grid quickly; the focus follows without stutter or lag, nothing replays after letting go, scroll bar and fade
  look like the settings rows. · E2E: no (real controllers, how it feels)
- [ ] **T506-115** **Owner:** exploratory: switch between the two levels, skip, take, own search and close in any
  order; note anything that feels unclear. · E2E: no (exploratory)

**Touches:** T491-13 (layout: two levels, "Gesucht: …", field above the grid), T491-14 (navigation, replaced by
T506-81 to -83), T491-15 (after A back in the games list, replaced by T506-84), T491-5 and T491-11 (hits filtered by
images, T506-92 to -94), T491-19 (image loading, replaced by T506-107 and -108), T491-20 (mouse, replaced by
T506-88), T491-26 (controller path, replaced by T506-111). SteamGridDB off greys the row out: part B (scenario 12).

**Open questions (answered with the recommendation unless the owner objects)**

- **Q1** ▲ from the grid's top row goes to the field, ▼ from the field back to the grid; ▲ on the field goes round to
  the grid's last row. The field row counts as the column's top.
- **Q2** Re-entering a game's hits starts at its first hit, not where the focus was.
- **Q3** Y in the hits behaves like A afterwards: back in the games list on the next waiting game.
- **Q4** With both sought a hit counts with a cover alone (the cover is what the tile shows). The 25 of the own
  search are the first 25 with images, not 25 results filtered down.
- **Q5** A game whose hits all drop out when shown stays on the list with "Keine Treffer" until skipped or
  found by an own search; it isn't removed silently while the player looks at it.
- **Q6** Games from the old waiting list are checked only when shown (no network request on its own, as part B);
  one hit left is shown, not taken, because the player is looking.
- **Q7** B on the keyboard keeps the term that was there before; only ↵ searches.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T506-80 to T506-108, T506-110, T506-111, T506-116 | 2026-11-12 | Claude | branch of this PR, Linux container, .NET 10, Debug; view model, window (headless Avalonia) and Core tests with made-up games and a fake SteamGridDB | ✅ | – |
| T506-100, T506-110 (red once) | 2026-11-12 | Claude | same, with the fix undone on purpose (focus follows the first check to finish; closing doesn't dispose the open waiting list) | ❌ as intended | both fixed in this PR |
| T506-109 (part) | 2026-11-12 | Claude | same, Release, headless software rendering, 100 waiting games with 8 hits, 60 steps ▼: UI thread (press, jobs, layout) mean 3.4 ms; with the headless software frame mean 59 ms, settings rows 36 ms in the same harness; the gap is the software render of the bigger frame, not the UI thread | ✅ UI thread, frame time needs Windows | QE measures on Windows |
