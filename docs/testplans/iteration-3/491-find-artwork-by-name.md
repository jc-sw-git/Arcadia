# #491 Find artwork by name and let the player pick when several games match

**Status:** Automated: T491-1 to -26 · QE: T491-27 · Open for the owner: T491-28, T491-29

Automated tests use made-up games, names, ids and images and a fake SteamGridDB that records every request; nothing
reaches the real one. "Shown everywhere" means library tile, preview, detail page and search hit. Decisions D1 to D7
(in the issue) are proposed and wait for the owner's OK; the cases follow them.

**Matching: launcher id first, then the name**

- [x] **T491-1** Given a valid key and an Epic game, when the background search runs, then SteamGridDB is asked by the game's Epic id on platform `egs` (the `AppName`, recorded in `docs/launchers/epic.md`), not by name; #13's scenario 2 holds: Epic's 3 : 4 cover cropped to 2 : 3, SteamGridDB's hero as background (tolerance "Normal"), shown everywhere. The request carries only `egs`, the id and the key. · E2E: yes
- [x] **T491-2** An Epic id with characters outside what Epic writes (a manipulated catalog: `/`, `..`, `?`, spaces, over the length limit) is never put into a request by id; the game counts as one without a launcher id (T491-3). · E2E: no (unit test)
- [x] **T491-3** A game without a launcher id (e.g. a demo game of another launcher) and a Steam or Epic game whose id SteamGridDB doesn't know (D2) are searched by name. A result with exactly one matching game (D1) is taken without a question: cover and background of that SteamGridDB game by order and "Zuschneiden erlaubt", as for an id match, shown everywhere, nothing on the waiting list. · E2E: yes
- [x] **T491-4** A Steam or Epic game SteamGridDB knows by its id is never searched by name, however many namesakes it has. · E2E: no (unit test)
- [x] **T491-5** Matching (D1): case, punctuation and ™ ® © are ignored; "Celeste" against "Celeste" and "Celeste Classic" → exactly one equal → taken; two equal ("Celeste" 2018 and "Celeste" 2016) → waiting list; none equal but several similar → waiting list; exactly one result of any name → taken; no result → launcher image or placeholder, no message, searched again at the next scan. At most 8 hits are kept per game, in SteamGridDB's order. · E2E: no (unit test)
- [x] **T491-6** The name sent is the one Arcadia shows, ™ ® © removed and trimmed, as a single encoded path segment: names with `/`, `\`, `?`, `#`, `%`, `&`, `..`, quotes, umlauts, Japanese characters, emoji or 300 characters never change host, path or query; an empty name after cleaning sends nothing. HTTPS to SteamGridDB's API host only, the key only in the `Authorization` header, nothing but the name and the key. · E2E: no (unit test with the recording fake)
- [x] **T491-7** No key or a rejected key: no search by id or name, nothing lands on the waiting list (the fake counts 0 requests, as T489-12). · E2E: no (unit test)
- [x] **T491-8** Throttling, no answer within 10 s, no network, a server error or broken JSON during a name search: as T489-21 (throttling waits, the rest stops SteamGridDB for the run); the game is neither put on the waiting list nor counted as skipped and is asked again at the next scan. One name request per game and run at most. · E2E: no (unit test with fake time)

**"Bei mehreren Treffern" and the row "Warten auf deine Auswahl"**

- [x] **T491-9** Settings → "Datenquellen" → group "ARTWORK": row "Bei mehreren Treffern" ("Wenn ein Spiel nur über den Namen gefunden wird") is "Selbst auswählen" on a fresh install, A switches to "Ersten nehmen" and back, kept after a restart; info text (RS) with the boxes "SELBST AUSWÄHLEN" and "ERSTEN NEHMEN". Indented below, always shown, "Warten auf deine Auswahl": with n games waiting badge n, "Bis dahin zeigen sie das Launcher-Bild oder den Platzhalter", "Auswählen ›", info box "n SPIELE" listing "<Name>" and "<Launcher> · k Treffer"; none waiting greyed out with "Kein Spiel wartet"; with "Ersten nehmen" greyed out with "Nur bei „Selbst auswählen“". Greyed rows stay focusable, the info text matches the state. Layout at 1920 × 1080, colours from `Theme.axaml`. · E2E: yes (window test: layout and texts)
- [x] **T491-10** Given "Selbst auswählen" and a game with three matching games, when the search runs, then it lands on the waiting list, keeps its launcher image or placeholder everywhere (#13's scenario 5), no image of any hit is downloaded and the row counts it. A further scan, "Jetzt suchen" or a restart neither adds it twice nor searches its name again while it waits. · E2E: yes
- [x] **T491-11** Given "Ersten nehmen", the same game takes the first hit in SteamGridDB's order, images by order and tolerance; after a run with 20 such games the waiting list is empty and the row greyed out (acceptance criterion 2). · E2E: yes
- [x] **T491-12** Switching to "Ersten nehmen" while n games wait: each takes its first hit at once, the list is empty, info toast "n Spiele: Arcadia hat jeweils den ersten Treffer genommen."; no toast when none waited. Switching back to "Selbst auswählen" puts none of them back on the list (D3). · E2E: no (view model test)

**The waiting list**

- [x] **T491-13** A on "Warten auf deine Auswahl" opens the screen as in the prototype: head "Warten auf deine Auswahl", "Welches Spiel ist gemeint?", "Spiel 1 von n"; left "n SPIELE" (one game: "1 SPIEL"), each game with initial, name, launcher and "k Treffer", the current one marked; right its name, "SteamGridDB kennt k Spiele mit diesem Namen. Nach deiner Wahl lädt Arcadia Cover und Hintergrund dieses Spiels.", the hits as tiles (cover 2 : 3, name, year), the note "Keins davon passt? Mit Y überspringst du das Spiel. …". Footer "D-Pad Spiel wählen", "Y Überspringen", "B Schließen", "A Übernehmen, weiter zum nächsten". · E2E: yes (window test: layout and texts)
- [x] **T491-14** Navigation (D4): D-pad and left stick left/right move the focus through the hits, up/down change the current game (focus on its first hit), both going round at the ends; held they repeat as `controls.md`. A hit without a release date shows no year; a hit without a cover shows the placeholder with its name. · E2E: no (view model test)
- [x] **T491-15** A takes the focused hit: the game leaves the list, its cover and background come from that SteamGridDB game by order and tolerance and are shown everywhere without a rescan; the next game becomes current ("Spiel 1 von n−1"), focus on its first hit. The images are automatic (D5): "Artwork ändern" shows "Quelle: SteamGridDB", "Gesetzt: automatisch (Standard)"; an image set by hand for that game stays. · E2E: yes
- [x] **T491-16** Y skips the current game: it leaves the list, keeps its launcher image or placeholder and is never searched by name again: not at the next scan, "Jetzt suchen", "Alle Artworks neu laden" or after a restart. "Artwork ändern" still offers "Eigene Datei" and the launcher image for it (D6). · E2E: yes
- [x] **T491-17** After the last game: "Alles erledigt. Kein Spiel wartet mehr auf deine Auswahl.", footer only "B Schließen"; B returns to the settings, focus on the row, now greyed out with "Kein Spiel wartet". · E2E: yes
- [x] **T491-18** B at any time closes the screen and returns to the row; undecided games stay on the list in their order, reopening starts at the first of them. · E2E: no (view model test)
- [x] **T491-19** Hit covers load only for the game being shown (one request per hit, image host only, never the key) in the background: the screen opens and moves at once, tiles show a placeholder with name and year until their cover is there; decoded at tile size. No network: placeholders stay, A still takes the hit and its images come at the next scan. · E2E: no (view model test with fake server and fake time)
- [x] **T491-20** Mouse and keyboard: the screen shows the ✕; a click on a game makes it current, a click on a hit focuses it, a click on a hint does what its button does. Keyboard as elsewhere (WASD/arrows, Space = A, Esc = B, V = Y). · E2E: yes (window test: real clicks)
- [x] **T491-21** The list changes while open: a scan ending with new waiting games adds them at the end without moving the focus; a game leaving the library disappears from the list (also the current one: the next becomes current), no crash. · E2E: no (view model test)

**Kept across restarts, failures**

- [x] **T491-22** Per game (by game key) Arcadia keeps the chosen hit, a skip and the waiting list with its hits across a restart: a chosen or first-taken game is asked by its SteamGridDB game at later scans and at "Alle Artworks neu laden", never by name again and never back on the list; a skipped one is never asked; a waiting one is still on the list with the same hits, without a new name search. · E2E: yes (acceptance test across a restart)
- [x] **T491-23** A game leaving the library and coming back keeps its choice or skip (D7). · E2E: no (unit test)
- [x] **T491-24** The file holding the choices is written as a temporary file, then renamed; unreadable, damaged or missing: one log line, Arcadia keeps running, images set by hand untouched, the games are treated as not yet searched. Values from it are checked: a SteamGridDB game id that isn't a plain number is ignored, nothing from it reaches a path or a URL unchecked. · E2E: no (unit test)
- [x] **T491-25** Arcadia closed during a name search or with the waiting list open: it closes without waiting for a request; at the next start the list is as before and the search continues with the games still missing. · E2E: no (unit test)

**Controller and performance**

- [x] **T491-26** Controller only, B back: Settings → "Datenquellen" → "Bei mehreren Treffern" A twice → "Warten auf deine Auswahl" A → D-pad right and down → A → Y → B; no mouse or keyboard needed (acceptance criterion 1). · E2E: yes
- [ ] **T491-27** Release build, made-up data: 100 games waiting with 8 hits each: opening the screen and every focus move < 50 ms (`Bild:` lines), the UI thread never blocked; a name search over 1,000 games without id, each answer taking 2 s, keeps the library usable (as T489-24). · E2E: no (real rendering, Release timings; quality engineer's run)

**Owner** (real SteamGridDB, own library; together with T489-25 to T489-27, T490-34 and T490-35)

- [ ] **T491-28** **Owner:** with a key, "Jetzt suchen" on the own library: Epic games get SteamGridDB backgrounds instead of the cropped 16 : 9 image, covers fit, none shows the wrong game. Also checks that SteamGridDB's `egs` ids are Epic's `AppName` (an assumption, see `docs/launchers/epic.md`). · E2E: no (real SteamGridDB, own library)
- [ ] **T491-29** **Owner:** if "Warten auf deine Auswahl" then shows games: open it with the controller, take the right hit for one game, skip another, close and start Arcadia: the first keeps its new images, the second its old one, neither is on the list again; after "Jetzt suchen" still the same. If no game waits, note that; T491-10 to T491-22 cover the list with made-up data. · E2E: no (real SteamGridDB, own library)

Notes on the automated cases: launchers without images of their own (a demo launcher, a plugin) now get SteamGridDB's
images by name when a key is set; the tests use such a launcher. The choices are kept in `artwork/name-matches.json`.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T491-1 to -8, -10 to -12, -15, -16, -19, -22 to -25 | 2026-11-12 | Claude | PR branch, Debug, Linux cloud container; made-up games and images, fake SteamGridDB and image server | ✅ `ArtworkServiceByNameTests`, `SteamGridDbNameTests`, `EpicArtworkTests` (Core) | – |
| T491-9, -12, -14 to -19, -21, -26 | 2026-11-12 | Claude | PR branch, Debug, Linux cloud container, fake SteamGridDB | ✅ `WaitingListTests`; red once: presses went to the settings under the open list, a game leaving the library stayed on it | – |
| T491-9, -13, -17, -20 | 2026-11-12 | Claude | PR branch, Debug, headless window, Linux cloud container | ✅ `Window/WaitingListViewTests` | – |
| T491-9, -13, -14, -17 (look) | 2026-11-12 | Claude | PR branch, Debug, Xvfb, Linux cloud container, `--demo`, three made-up games waiting in `name-matches.json`, no key | ✅ row with badge "3" and info box, the screen with games, hits (placeholders), position, D-pad, Y up to "Alles erledigt."; a hit without a year sat lower than the others and the initial stood in a corner: fixed | hit covers from the real SteamGridDB: T491-29 |
| T491-13, -27 (part) | 2026-11-12 | Claude | PR branch after the QE review, Release, Xvfb (software rendering), `--demo`, 107 made-up games waiting with 8 hits each, no key | ✅ 8 hits in two rows, the note and the hints in the modal; Y 9–11 ms processing, 58–68 ms press to frame (the first 217 ms); A 9–12 ms processing, 46–62 ms to frame (the first 450 ms); D-pad 27–29 ms. Before: the list rebuilt per press (QE: Y up to 3.2 s) | measure on the owner's PC (QE run) |
