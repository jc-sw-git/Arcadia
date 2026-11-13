# #560 Final test of #43: glyphs, scrolling the hit info, row order and quick selection alignment

**Status:** Automated: T560-2, T560-3, T560-4 · QE: T560-5, T560-6 · Open for the owner: none (T560-1, T560-7 passed)

- [x] **T560-1** **Owner:** `--first-run` with the Steam Controller, step "Datenquellen": the focus on "Fertig" shows ≡'s
  glyph whole (lines on the dark glyph), not an empty dark circle. · E2E: no (real controller: the keyboard shows the
  key "Enter" as text, which was never broken)
- [x] **T560-2** Detail page: "Spielen" shows A's glyph while it has the focus; with the focus in the rail or the other
  buttons it shows its play icon ("Installieren": a download arrow) and no button shows A. · E2E: yes
- [x] **T560-3** "Warten auf deine Auswahl" (metadata): RS (Shift+W/S) scrolls the marked hit's facts up and down
  while they overflow their box, and only then the footer names RS; facts that fit ignore RS; the list's game stays and
  the list stays open. · E2E: yes
- [x] **T560-4** Settings, "Artwork": "Von Hand gesetzte Artworks überschreiben" sits right under "Alle Artworks neu
  laden", indented; "Zuschneiden erlaubt" follows. · E2E: yes
- [x] **T560-5** Detail page, quick selection: the note under the fifth place has its icon centred on the first line and
  sits about a third of the way down to "Übersicht anpassen". · E2E: no (window: layout)
- [x] **T560-6** Detail page, quick selection: the ◀ ▶ of "Steam-Controller-Modus" and "Individueller Mausmodus" stand
  in the same columns, the values centred between them. · E2E: no (window: layout)
- [x] **T560-7** **Owner:** on your PC, a metadata entry with a long description: RS scrolls the info box to its end,
  nothing is cut off. · E2E: no (real IGDB descriptions)

Automated tests:

| Test cases | Tests |
|---|---|
| T560-2 | `DetailControlsTests.The_play_button_shows_As_glyph_only_while_it_has_the_focus` |
| T560-3 | `WaitingListMetadataTests.Rs_scrolls_the_facts_of_a_metadata_hit_only_while_they_overflow_and_nothing_else_on_the_list` |
| T560-4 | `DataSourcesTests` (the rows of both tabs, with their indents) |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T560-2, -3 | 2026-11-13 | Claude | branch, Linux, headless tests | ✅ (red once each with the change undone) | – |
| T560-4 | 2026-11-13 | Claude | branch, Linux/Xvfb, `--demo` | ✅ screenshot: the switch indented under "Neu laden" | – |
| T560-5, -6 | 2026-11-13 | Claude | branch, Linux/Xvfb, `--demo`, keys | ✅ note icon on its line, arrows in columns; "Spielen" with play icon while the focus is in the quick selection | – |
| T560-1 | 2026-11-13 | Claude | branch, code only | cause found: the focused pill's style painted every path inside it, also ≡'s icon, in the glyph's own dark colour; now only the pill's own spinner | owner checks with the controller |
| T560-2, -4, -5, -6 | 2026-11-13 | QE (Claude) | `01c420c`, Linux/Xvfb, `--demo`, keys | ✅ A only on the focused "Spielen" (download arrow on "Installieren"); note icon on its line, about a third of the way down; ◀ ▶ in columns; switch indented under "Neu laden" | – |
| all | 2026-11-13 | QE (Claude) | `01c420c`, Linux, `tools/ci/run-tests.sh 4` | ✅ 777 App test methods, Core and Pan green | – |
| T560-1, T560-7 | 2026-11-13 | Owner | `main` after #561, Release, his PC, TV, Steam Controller, his libraries | ✅ final test of #43; findings went into #560, fixed by PR #561 | – |
