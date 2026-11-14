# #466 Filter the library by metadata (genre, tags, developer, publisher, year, age rating)

Part B of #44: the metadata categories in the modal "Filter" that #465 built (designs: artboards "#44"). Controller
only unless a case says otherwise; B goes back one level. Test cases derived by the quality engineer from #466's
scenarios 1–19 and #44's visual checklist, before implementation. The age rating flow is provisional: the owner tries
it in T466-16 and may change it within this issue. The default rating system is the region's (#557).

**Status:** Automated: T466-1 to -15, -17 to -22 · QE: T466-23, -25 · Open for the owner: T466-16, T466-24

**Long value lists (genre, tags, developer, publisher)**

- [x] **T466-1** In "Tags", every value shows the hits it would bring, counted with the other categories' choice;
      chosen values on top, values with 0 hits greyed at the end and still selectable (choosing one gives the
      no-hits state of #465); the first entry "Alle Tags" clears the category. The same for "Genre", "Entwickler",
      "Publisher" ("Alle <Kategorie>"). · E2E: yes
- [x] **T466-2** L3 switches the list between "Häufigste zuerst" and "A–Z", remembered per category while the modal is
      open; "Genre" starts with A–Z grouped by letter, tags, developer and publisher with "Häufigste zuerst". In A–Z
      LT/RT jump to the first value of the next / previous letter, in "Häufigste zuerst" they page. · E2E: yes
- [x] **T466-3** X opens "Werte durchsuchen" with the on-screen keyboard; typing narrows the list (chosen values
      included), A on a hit chooses it; while the keyboard is open R3 is its layout menu and doesn't close the modal;
      leaving the category (B to the categories, another category) clears the search. · E2E: yes
- [x] **T466-4** With own tags in the tab, "Tags" starts with their own group, holding only own tags a game in this
      tab has. Own tag "Horror" (on game A) and provider tag "Horror" (on game B) are two values; the own one hits
      only A, the provider one only B. · E2E: yes
- [x] **T466-5** Developer "CD PROJEKT RED" on 2 games and "CD Projekt Red " on 1: one value "CD PROJEKT RED" with
      3 hits; "Ubisoft Montreal" and "Ubisoft" stay two values. The same merging for genre (by key), tags and
      publisher. · E2E: yes
- [x] **T466-6** An Epic game with only English IGDB tags: the filter offers exactly the tags its detail page shows,
      untranslated and untrimmed. · E2E: yes

**Filtering and "Ohne Angabe einbeziehen"**

- [x] **T466-7** Genre "Shooter" chosen and closed: only shooters; "Shooter" and "Rollenspiel": entries with either;
      genre "Shooter" and developer "X": only X's shooters; with "Nur installierte" (part A) too, only installed
      ones. · E2E: yes
- [x] **T466-8** Before a value is chosen, no switch "Ohne Angabe einbeziehen"; after one, it appears off with
      "Zeigt zusätzlich Einträge ohne Genre." below; on, entries without a genre are added and the left column reads
      "Shooter & o. Angabe". The same in all six categories, explanation "… ohne Tags / Entwickler / Publisher /
      Erscheinungsjahr / USK-Freigabe" (the chosen system's name). · E2E: yes
- [x] **T466-9** Chosen values that don't fit the left column's line read "Mehrere" (also with "& o. Angabe").
      · E2E: yes
- [x] **T466-10** Saving: `settings.json` holds a chosen genre by its key, a provider tag, developer and publisher by
      name, an own tag by its identity; after a restart the filter applies again. Once #557 lets the UI language
      change, a chosen genre still applies after the switch and shows in the new language. · E2E: yes

**Release year**

- [x] **T466-11** The range runs from the earliest to the latest year in the tab, a pre-order's future year
      included; ◀▶ change the focused end by one year, LT/RT by ten, stopping at the tab's earliest / latest year,
      "von" never after "bis"; closing shows only entries in the range. · E2E: yes
- [x] **T466-12** Quick picks with the fake date in 2026: "Letzte 3 Jahre" = 2024–2026, "2020er" = 2020–2029,
      "2010er", "2000er", "Vor 2000" = up to 1999, "Alle Jahre" clears; a pick without hits is greyed, still
      selectable, and gives the no-hits state. · E2E: yes

**Age rating (provisional)**

- [x] **T466-13** Only the systems the tab has ratings for are offered; default USK if present, else the first present
      one; after choosing the system, one or more of its ratings (USK 0/6/12/16/18, PEGI 3/7/12/16/18,
      ESRB E/E10+/T/M/AO) filter with "or". · E2E: yes
- [x] **T466-14** USK 16 chosen, then the system switched to PEGI: the USK choice is gone, PEGI starts empty. Under
      USK a game with only a PEGI rating is "ohne Angabe" (hidden; shown with the switch on). · E2E: yes
- [x] **T466-15** A tab without any age rating has no "Altersfreigabe"; a tab with only PEGI offers only PEGI, chosen
      at once. · E2E: yes
- [ ] **T466-16** **Owner:** On your PC, in a tab with rated games, open the filter, go to the age rating, choose a
      system and a few ratings, switch the system, close and look at the library. Does the flow feel right, is
      anything missing or confusing? Changes go into this issue. · E2E: no (the owner's acceptance of a provisional
      design)

**What the tab doesn't supply**

- [x] **T466-17** A tab where no entry has a developer has no "Entwickler" (same for every metadata category); a tab
      without any category (part A's or metadata) shows the funnel alone and R3 does nothing. · E2E: yes

**Metadata still loading**

- [x] **T466-18** Filter "Shooter" active, a metadata run after a scan delivers genres one by one: new shooters appear
      without a key press, the focus stays on the focused game (its page may change); if that game no longer
      matches, the focus goes to the next hit. · E2E: yes
- [x] **T466-19** The modal opened during a metadata run: the counts stay those from opening, on top "Metadaten
      werden noch geladen, die Zahlen können sich ändern." while the run goes (gone when it ends); closing filters
      with the new data. · E2E: yes

**Failure cases**

- [x] **T466-20** Own tag "Grusel" chosen: renamed to "Horror", the filter still holds it as "Horror" (also in
      `settings.json`); merged into another own tag, the filter holds the kept tag; deleted, it drops out, and with
      nothing left chosen "Tags" is unfiltered. · E2E: yes
- [x] **T466-21** Values only IGDB supplies chosen, then IGDB switched off in "Datenquellen": those values are gone
      from the modal and the library is filtered without them; the saved choice stays and applies again once IGDB is
      back. Once #557 lands, the same for provider tags of another UI language. · E2E: yes

**Controls**

- [x] **T466-22** Gamepad alone through every metadata category: B from the value search goes to the list, from the
      list to the categories, from there closes and applies; no step needs a mouse or keyboard. · E2E: yes
- [x] **T466-23** With the mouse: click a category, a value, the sort switch, the search field, the switch "Ohne
      Angabe einbeziehen", the year range and a quick pick, a rating system and a rating; the wheel scrolls long lists;
      hints show the keyboard's keys (C for L3, the key for X). · E2E: no (real clicks and wheel, the quality engineer's
      run)

**Performance and look**

- [ ] **T466-24** **Owner:** Release build, `--demo-games 5000` with metadata (thousands of tag values): every press in "Tags"
      (move, choose, L3, a letter jump) moves the focus within 50 ms, opening the modal and a choice don't block the
      UI (counts computed off the UI thread). · E2E: no (budget measured by hand, CI asserts no timing)
- [x] **T466-25** #44's visual checklist for part B: focus ring on the first and last value of a long list, on the
      own-tags group, the switch, both year ends, the quick picks and ratings; fade top and bottom, scroll bar and
      letter bar in A–Z; 0-hit values greyed but readable; developer names over 30 characters and 200 % scaling
      truncate with "…" while hit counts stay; bar chart readable; the keyboard never covers the search field; the
      loading line fits; hints for Xbox, PlayStation and keyboard. · E2E: no (look, the quality engineer's run)

Automated tests:

| Test cases | Tests |
|---|---|
| T466-1 to -5, -7 to -15, -17 to -20, -22 | `LibraryMetadataFilterTests` |
| T466-1, -4, -5, -7, -8, -10 to -15, -20, -21, -24 (counting cost) | `GameFilterMetadataTests` (Core) |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T466-1 to -5, -7 to -15, -17 to -20, -22 | 2026-11-13 | Claude (implementation) | branch of the PR, `dotnet test`, Linux container | ✅ | – |
| T466-25 (rings only) | 2026-11-13 | Claude (implementation) | `RingGeometryTests` (window guard), Linux container | ✅ | – |
| T466-25 (look, part) | 2026-11-13 | Claude (implementation) | Debug build under Xvfb, `--demo`, keyboard only | ✅ after fixes | ring of the first value was clipped by the pane, the categories didn't fit beside the keyboard, the match note didn't follow the search: fixed; with `--demo-games 5000` the demo's metadata took about 2 min to arrive here, before that the tab has only "Installiert" |
| All tests of the solution | 2026-11-14 | Claude (QE) | Release build of 6b91cb7, `dotnet test`, Linux container | ✅ | App 966/966, Pan 438/438, Core 3,273/3,274: `MetadataServiceTests.Requests_keep_the_stores_gap_and_http_429_…` red once under full load, green in 3 reruns; not touched by this PR (flaky) |
| T466-23 | 2026-11-14 | Claude (QE) | Release build under Xvfb, `--demo`, real clicks and wheel (xdotool) | ❌ | every element clickable, the wheel scrolls the list; a click on "A–Z" / "Häufigste zuerst" leaves the hint "Reihenfolge: …" on the old order until the next key; a click on a system moves the focus ring to another system (QE finding 1 and 2) |
| T466-24 | 2026-11-14 | Claude (QE) | Release build under Xvfb, `--demo --demo-games 5000`, demo metadata patched locally to about 1,000 tags and 1,500 developers (the demo has 7 tags) | ❌ | a move in "Tags" 54–133 ms press to frame (one 374 ms), L3 69–97 ms, LT/RT 58–114 ms, a choice 1–5 ms; opening the modal 0.37–1.0 s; the library beside it 1.4 ms median. Measured on the container, not the owner's PC (QE finding 3) |
| T466-25 | 2026-11-14 | Claude (QE) | Release build under Xvfb, `--demo` and `--demo-games 5000`, keyboard and mouse | ❌ | rings on first and last value, switch, year ends, quick picks, systems and ratings, fades, scroll and letter bar, greyed values, long developer names cut with "…" and counts kept, bar chart, keyboard below the search field: ✅; a single long chosen value reads "Mehrere"; while typing the search, the hint "Wert" stays; no loading line while the launchers' own metadata is read (QE finding 4, 5, 6). Not checked: Xbox/PlayStation glyphs (no gamepad here), 200 % scaling (the Big Picture is laid out in a Viewbox) |
| T466-24 (fixes) | 2026-11-14 | Claude (implementation) | Debug build under Xvfb, `--demo --demo-games 5000` with the demo's numbered topic tags (about 640 tags in the GOG tab), keyboard | ✅ | a move in "Tags" 10–17 ms press to frame while the list scrolls, under 1 ms without scrolling; opening the filter 42–75 ms after the first (2.3 s cold), it was 0.4–0.6 s with the keyboard built at each opening |
| QE findings 1, 2, 4, 5, 6 | 2026-11-14 | Claude (implementation) | `dotnet test`, Linux container; Xvfb for the hints while typing and the loading line | ✅ | each new or changed acceptance test red on the code before the fix; the loading line shows while the launchers' files are read |
| T466-23, T466-25 (re-check) | 2026-11-14 | Claude (QE) | Release build of cb0f930 under Xvfb, `--demo` and `--demo-games 5000`, keyboard and mouse | ✅ | QE findings 1, 2, 4, 5 and 6 fixed: the ring stays on the chosen system (key and click), the hint follows a click on the order, one long developer name is cut with "…", typing shows only "Tippen" and "Tastatur ausblenden", the loading line shows while the launchers' metadata is read. Not checked: Xbox/PlayStation glyphs (no gamepad here) |
| T466-24 (re-check) | 2026-11-14 | Claude (QE) | Release build of cb0f930 under Xvfb, `--demo --demo-games 5000` (about 640 tags in the GOG tab), keyboard | ❌ | opening 43 ms; a move inside the window under 1 ms, a move that scrolls 8–13 ms, a choice 15 ms, a letter jump in A–Z under 3 ms: ✅; LT/RT paging in "Häufigste zuerst" 27–172 ms (6 of 12 over 50 ms) and L3 58–128 ms: over the budget in the container (QE finding 7) |
| T466-24 (paging, L3) | 2026-11-14 | Claude (implementation) | Debug build under Xvfb, `--demo --demo-games 5000` (about 640 tags in the GOG tab), keyboard | ✅ | with fixed row slots: LT/RT paging 3–6 ms press to frame, L3 12–21 ms, a scrolling move 4 ms; `RingGeometryTests` caught a clipped ring on the slots' values first (fixed) |
| T466-6, T466-21 | 2026-11-14 | Claude (implementation) | `LibraryIgdbFilterTests`, `dotnet test`, Linux container | ✅ | the App tests deferred so far, with IGDB's fake: the filter's tags equal the detail page's, IGDB off drops them and keeps the saved choice |
| T466-24 (QE, container) | 2026-11-14 | Claude (QE) | Release build of 997c4ba under Xvfb, `--demo --demo-games 5000` (about 640 tags in the GOG tab), keyboard | ✅ | LT/RT paging 0.4–12.8 ms, L3 12–32 ms, moves 0.2–11.5 ms press to frame; rings on first and last value in A–Z and on the last column fine. The owner's PC run stays open |
