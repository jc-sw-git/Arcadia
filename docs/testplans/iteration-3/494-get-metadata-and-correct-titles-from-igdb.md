# #494 Get metadata and correct titles from IGDB

Derived from the specification in #43 (decisions, scenarios 2, 4, 5, 12, failure cases) and #494's scope before any
code. Automated tests use made-up games, names, ids and access values and a fake Twitch token service and fake IGDB
(`FakeIgdb`) that record every request; nothing reaches the real ones. Window cases (layout, real clicks) are
`E2E: no (window)`: since #517 there are no window tests per feature, the quality engineer checks them in the own run.
"Shown everywhere" means library tile, detail page and search hit. The cases follow the proposals D1 to D9 in #494; a
changed decision changes its case. #494 comes in two PRs: this one (access, matching, order, titles), then the waiting
list entries "Metadaten" (T494-16 to T494-20, struck here until that PR takes them up). The owner's cases are collected
by the closing thread of #43.

**Status:** Automated: T494-2 to T494-15, T494-21, T494-23, T494-24, T494-26 to T494-30, T494-32 · QE: T494-1, T494-22, T494-31, T494-33 · Open for the owner: T494-34, T494-35, T494-36

**IGDB access**

- [ ] **T494-1** Settings → "Datenquellen" → group "IGDB": rows "Client-ID" and "Client-Secret" (A "Eingeben"/
  "Bearbeiten": on-screen keyboard, field starts empty), each with an indented "Aus der Zwischenablage einfügen", then
  "Zugang anlegen", "Verbindung testen", "Zugang löschen" (greyed out without access). The rows show only the last four
  characters. The info text names IGDB's limit (4 requests per second) and what IGDB gets (launcher ids or game names
  and the access, nothing else). Greyed rows stay focusable. Layout at 1920 × 1080, colours from `Theme.axaml`. ·
  E2E: no (window)
- [x] **T494-2** Controller only: A on "Client-ID", type a valid id, confirm; same for "Client-Secret" → checked at
  once (one token request): "● Verbunden · endet auf …"; B or an empty field taken keeps the previous value, no
  request. · E2E: yes
- [x] **T494-3** "Einfügen" with spaces or a line break around a valid value: trimmed and taken. Clipboard empty,
  holding an image or an invalid value: the previous value stays, a warning toast says so, no request. · E2E: no (view
  model test)
- [x] **T494-4** A value with anything but letters and digits (control characters, CR/LF, spaces inside, quotes, `&`,
  `=`) or outside 20 to 64 characters (D2) is refused before any request and never reaches a header, a URL, a request
  body, a path or a log. · E2E: no (unit test)
- [x] **T494-5** "Zugang anlegen" by controller opens Twitch's developer console (HTTPS, fixed in code, no user text in
  it) in the default browser with an info toast; the mouse mode is on while the browser is in front, also with
  "Mausmodus: Aus", and off again back in Arcadia. · E2E: no (view model test with fake browser)
- [x] **T494-6** "Verbindung testen": valid → "Geprüft um hh:mm · Verbunden"; rejected → T494-9; no answer within
  10 s or no network → "Keine Verbindung", access kept, no warning, no status message; a second A while a test runs
  sends no second request. · E2E: no (view model test with fake time)
- [x] **T494-7** Windows: id and secret stored with DPAPI for the current user; neither `settings.json` nor any other
  file under `%APPDATA%\Arcadia` or `%LOCALAPPDATA%\Arcadia` holds them in plain text; after closing and starting
  Arcadia they are there. A stored value that can't be decrypted counts as no access, no crash. Linux: works until
  Arcadia closes, nothing written to disk. · E2E: no (unit test; DPAPI on the Windows CI runner)
- [x] **T494-8** Client id, secret and access token never appear in `arcadia.log`, toasts, "Letzte Meldungen",
  exception texts or the status modal: every access flow plus a rejected, a timed-out and a throttled request with
  made-up values, then search all of them for the values. · E2E: no (unit test)
- [x] **T494-9** Invalid access (rejected at entry, at "Verbindung testen" or during a fetch): a warning at the tab
  "Datenquellen", at the row ("Zugang ungültig · IGDB wird übersprungen") and in the header's status chip, whose entry
  opens the row; no further request with it until the access changes; valid new access or "Zugang löschen" ends the
  warning; launcher data keep working. · E2E: yes
- [x] **T494-10** No access (fresh settings, after "Zugang löschen", only one of the two values): start, scan, rescan
  and "Alle Metadaten neu laden" send Twitch and IGDB not one request (the fakes count 0); no message, no symbol at
  the tab; IGDB in the order greyed out ("Wird übersprungen …"); an Epic game shows only its developer (scenario 3). ·
  E2E: yes
- [x] **T494-11** Requests go over HTTPS to Twitch's token host and IGDB's API host only (fixed in code), no cookies;
  the secret only in the token request, client id and token only in headers; an IGDB request carries only the Steam
  AppID or the game name. A name with `"`, `;`, `\`, `*`, a line break, emoji or 300 characters never changes the
  query beyond the name. The token is kept in memory only; a rejected token is renewed once, then the access is
  invalid (T494-9). · E2E: no (unit test with recording fakes)
- [x] **T494-12** Never more than 4 requests per second to IGDB (fake time, 1,000 games). Throttling (429): waits and
  continues with the same game, no toast. No network, 10 s timeout, 5xx, broken JSON: IGDB is left out silently for the
  rest of the run (no request per game), one log line, retried at the next scan; no status message. · E2E: no (unit
  test with fake time)

**Matching**

- [x] **T494-13** A Steam game is asked by its AppID (`external_games`, source 1), never by name, however many namesakes
  it has; an AppID IGDB doesn't know gives no IGDB data and no name search (D3). · E2E: no (unit test)
- [x] **T494-14** An Epic game (and a game of another launcher) is searched by name, once per game and run; the name
  sent is the shown launcher title without ™ ® ©, trimmed; empty after cleaning: nothing sent. DLC, mods, bundles and
  the other types of D4 are dropped first: "Nordwind" against "Nordwind", "Nordwind – Season Pass" (DLC) and "Nordwind
  Remix" (mod) → taken without a question. · E2E: yes
- [x] **T494-15** Name match ignores case, punctuation and ™ ® ©; `?` stands for one character (D5): "K?ln" against
  "Köln" and "Köln Stories" → "Köln" taken. One main game left of any name → taken; two equal ("Nordwind" 2015 and
  2021) or none equal but several → several hits (at most 8, IGDB's order); none → launcher data, no message, searched
  again at the next scan. · E2E: no (unit test)

**"Bei mehreren Treffern" and the waiting list** (part 2 of #494, own PR; until then several hits keep the launcher
data and are asked once per session, `Several_hits_with_selbst_auswaehlen_keep_the_launcher_data_and_are_asked_once`)

- [ ] ~~**T494-16** Scenario 4: "Selbst auswählen" and an Epic game with three main-game hits → on "Warten auf deine
  Auswahl" marked "Metadaten", the row counts it, the game keeps its launcher data (developer, doubtful title)
  everywhere; a rescan, "Alle Metadaten neu laden" or a restart neither adds it twice nor searches its name again while
  it waits. · E2E: yes~~ (part 2)
- [ ] ~~**T494-17** "Ersten nehmen": the first hit in IGDB's order is taken, its fields merged, a doubtful title replaced.
  Switching to "Ersten nehmen" while metadata and artwork entries wait: each takes its first hit at once, one toast for
  all (D7); switching back puts none back. · E2E: no (view model test)~~ (part 2; the first half is covered here by
  `Several_hits_with_ersten_nehmen_take_the_first_in_igdbs_order_and_replace_a_doubtful_title`)
- [ ] ~~**T494-18** The waiting screen with a metadata entry (D6): the games list marks each entry "Artwork" or
  "Metadaten", a game waiting for both is there twice; the hits side reads "Gesucht: Metadaten", tiles with IGDB's
  cover or the placeholder, name and year (none without a date); "Eigener Suchbegriff" searches IGDB's main games.
  Layout at 1920 × 1080; a click on an entry and on a hit work. · E2E: no (window)~~ (part 2)
- [ ] ~~**T494-19** A on a metadata hit: the entry leaves the list, the game's IGDB fields and corrected title are shown
  everywhere without a rescan, its artwork entry (if any) stays. Y skips: launcher data stay, never searched by name on
  IGDB again, also not at the next scan, "Alle Metadaten neu laden" or a restart (D8). B closes, undecided entries stay
  in their order. · E2E: yes~~ (part 2)
- [ ] ~~**T494-20** Choice, skip and waiting metadata entries are kept per game across a restart and a game leaving the
  library and coming back; the file is written as a temporary file and renamed; damaged or unreadable: one log line,
  games treated as not yet searched, Arcadia keeps running; an IGDB game id that isn't a plain number is ignored. ·
  E2E: no (unit test)~~ (part 2)

**Order and merging**

- [x] **T494-21** Group "REIHENFOLGE FÜR METADATEN": "1. Launcher-Daten", "2. IGDB" (default), each a switch An/Aus
  (◀ ▶ or A, default on). L3 lifts, ▲ ▼ move (no wrap), L3 drops, B puts it back without leaving the page; saved in
  `settings.json`, kept after a restart. · E2E: yes
- [ ] **T494-22** Mouse: dragging a source moves it, a click switches it, as in the artwork order. · E2E: no (window)
- [x] **T494-23** Scenario 5: a Steam game with Steam's German description and genres and IGDB's English description,
  genres and release date; moving IGDB first changes the fields at once with 0 requests (merged from the cache): genres
  and date from IGDB, a field IGDB lacks from Steam, the description stays Steam's German one (language rule). · E2E: yes
- [x] **T494-24** IGDB switched off: its fields and corrected titles disappear everywhere at once (the doubtful launcher
  title is back), no request to IGDB; the cache stays, switching on brings them back without a request. Both sources
  off: no metadata, launcher titles. · E2E: no (view model test)
- [ ] **T494-25** Scenario 2's title: Epic "K?ln" and IGDB's "Köln", with launcher data first and with IGDB first →
  "Köln" in library, detail page, search (searching "Köln" finds it) and sorting; "K?ln" is listed only among the
  sources in "Infos". A title without `?` stays the launcher's in both orders (D9). · E2E: yes (the title in "Infos" and
  the merge in both orders are tested here; library, search and sorting take it through #496's `GameTitles`, wired by
  whichever of the two PRs merges later, which ticks this case)
- [x] **T494-26** Scenario 2's fields: an Epic game with access shows IGDB's English description (no provider has a
  German one), genres on the German list, the year and the USK badge in the info line; "Infos" names "IGDB" as the
  source of each of these fields and "Epic" for the developer. · E2E: yes
- [x] **T494-27** IGDB's fields: genres mapped onto the German list (e.g. "Role-playing (RPG)" → "Rollenspiel",
  "Adventure" → "Abenteuer"), unmapped ones dropped; first release date; age ratings by organization and category
  (USK = organization 4, categories 18–22 → USK 0/6/12/16/18; PEGI and ESRB likewise), unknown ones dropped; themes and
  keywords as provider tags as delivered. · E2E: no (unit test)
- [x] **T494-28** IGDB's answers are checked: broken JSON counts as no data, overlong strings are cut, an image URL
  only over HTTPS to IGDB's image host; nothing from an answer reaches a path or a process start. Raw answers are
  cached per game under `%LOCALAPPDATA%\Arcadia\metadata` (temporary file, renamed), stay visible offline; a broken
  cache file is discarded and fetched again. · E2E: no (unit test)
- [x] **T494-29** Arcadia closed during a fetch: it closes without waiting for a request; the next scan continues with
  the games still missing; no half-written file. · E2E: no (unit test)
- [x] **T494-30** The progress row "Fehlende Metadaten: n von m, noch etwa x Minuten" counts the games IGDB still has to
  answer, its estimate follows the 4 per second. · E2E: no (view model test)

**Controller and performance**

- [ ] **T494-31** Controller only, B back (scenario 12): "Datenquellen" → "Client-ID" and "Client-Secret" typed →
  "Verbindung testen" → IGDB lifted to first and back → "Warten auf deine Auswahl" → a metadata entry → A on a hit → B;
  no mouse or keyboard needed. Only Twitch's developer console needs the mouse (named in the PR). · E2E: yes (the steps
  up to the order are in `IgdbSettingsTests`; the waiting list steps come with part 2)
- [x] **T494-32** Without access no request reaches IGDB or Twitch over a whole session with 1,000 games (start, scans,
  every page opened): the fakes count 0 (acceptance criterion of #43). · E2E: no (unit test over the composition)
- [ ] **T494-33** Release build, made-up data: 1,000 Epic games, each IGDB answer taking 2 s: library usable at once,
  every focus move < 50 ms (`Bild:` lines), the UI thread never blocked, the settings page operable; idle CPU after the
  run as before. · E2E: no (real rendering, Release timings; quality engineer's run)

**Owner** (real Twitch and IGDB, own library)

- [ ] **T494-34** **Owner:** "Zugang anlegen" with the controller, create the access in Twitch's console by mouse, paste
  id and secret with "Einfügen" → "Verbunden", mouse mode off again in Arcadia. Close and start Arcadia: still
  "Verbunden". Change one character of the secret: warning at the tab, the row and the status chip; pasting the right
  one clears it. · E2E: no (real Twitch account and browser)
- [ ] **T494-35** **Owner:** with access, the own Epic games get description, genres, year and age rating; titles with
  broken umlauts are correct in library, search and detail page; none shows another game's data; the library stays
  fluid while it loads. · E2E: no (real IGDB, own library)
- [ ] **T494-36** **Owner:** move IGDB first and back: the Steam games' fields change at once, German descriptions stay.
  If "Warten auf deine Auswahl" shows "Metadaten" entries: take the right hit for one, skip another, restart Arcadia:
  both stay decided. If none waits, note that; T494-16 to T494-20 cover it with made-up data. · E2E: no (real IGDB, own
  library)

Automated tests:

| Test cases | Tests |
|---|---|
| T494-2, -3, -5, -6, -9, -10, -21, -26 | `IgdbSettingsTests` (App); access rules in `IgdbAccessTests` (Core) |
| T494-4, -11, -13 to -15, -27, -28 | `IgdbRulesTests`, `IgdbAccessTests`, `MetadataServiceIgdbTests` (Core) |
| T494-7, -8 | `IgdbAccessTests` (Core), `IgdbSettingsTests.The_access_is_there_again_after_a_restart` (App) |
| T494-12, -23, -24, -29, -30, -32 | `MetadataServiceIgdbTests` (Core); T494-32 also `IgdbSettingsTests` over the composition |
| T494-25 (merge and "Infos") | `MetadataServiceIgdbTests.A_doubtful_title_is_replaced_in_both_orders_and_a_title_without_a_question_mark_stays`, `IgdbSettingsTests.Infos_names_igdb_for_its_fields_and_lists_the_broken_launcher_title_only_among_the_sources` |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T494-2 to -15, -21, -23 to -30, -32 | 2026-11-12 | Claude | branch of PR, Debug, Linux container, made-up data | ✅ | – |
| T494-9 (red once) | 2026-11-12 | Claude | branch of PR, the IGDB warning switched off on purpose | ❌ as expected, ✅ again | – |
| T494-1, -21 (look only) | 2026-11-12 | Claude | branch of PR, Debug, Linux Xvfb 1920 × 1080, `--demo`, keys | ✅ group IGDB with greyed rows, order with "Wird übersprungen …", a lifted source; QE's own run still checks T494-1 | – |
