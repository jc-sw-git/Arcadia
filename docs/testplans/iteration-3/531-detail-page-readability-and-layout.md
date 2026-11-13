# #531 Detail page readability and layout: text shadows, raised buttons, long titles, value boxes, chips

Findings of the owner's hand test of #43 (detail page from #498/#499/#500), changes 1 to 8 of the issue. The cases were
written by the implementing thread from the issue's changes, as the issue had none; the quality engineer checks them in
the review.

**Status:** Automated: T531-6, T531-3 (the sizing) · QE: T531-1, T531-3, T531-4, T531-5, T531-7 · Owner passed: T531-2, T531-8, T531-9

Every case runs on the detail page with the gamepad alone unless it says otherwise; keys follow `docs/product/controls.md`.
"A bright artwork" is a game whose background is light behind the title and the button row.

- [x] **T531-1** A not installed game with a bright artwork: the title, the info line (genres · developer · year), the
  note beside the buttons ("Die Installation wird in … geöffnet"), "Original-Titel: …" (⧉) and the names under
  "Davor"/"Danach" each have a soft dark shadow and read clearly; text on a surface (chips, panel) has none.
  · E2E: no (window: the shadow is drawn)
- [x] **T531-2** **Owner:** Every button that acts when pressed looks raised: lighter at the top, a lit upper and a darker
  lower edge, a shadow beneath, its label set in (white text with a dark shadow, dark text on blue or white with a light
  edge below). Look at the detail page's buttons over a bright and a dark artwork, "Spielen"/"Optionen" in the library,
  a dialog ("Deinstallieren?", "Steam beenden?"), "Fertig" at the first start, Pan's held buttons and "Für alle Spiele
  übernehmen". A greyed button, rows, tabs, chips and value pills stay flat; the focus ring sits around the raised
  button. · E2E: no (look and feel; the owner approves the design)
- [x] **T531-3** A title too long for one line shrinks (down to half the size) until it fits one line; one still too long
  at half the size wraps into two lines and is cut with "…". With a short, a shrunk and a two-line title the button row
  stays at the same height. Rename a game with "Titel ändern" to try it. · E2E: no (window: the measured text; the
  choice of size is automated)
- [x] **T531-4** "Übersicht" shows the title (the own title, if any) above the short description, in one line cut with
  "…"; description, value boxes and tags still fit the panel without overlapping. · E2E: no (window: layout)
- [x] **T531-5** The boxes "Zuletzt gespielt", "Spielzeit", "Installiert am" and "Größe" are smaller than before; caption,
  value and source each still read in one line. · E2E: no (window: layout)
- [x] **T531-6** A game with metadata but no description: "Übersicht" and "Infos" say "Es liegt keine Beschreibung zu
  dem Spiel vor." where the description would be; once the store brings one, the description shows instead. A game
  without any metadata keeps "Noch keine Infos" in "Infos". · E2E: yes
- [x] **T531-7** The quick selection's entries have the colour of the value boxes; a choice ("Steam-Controller-Modus")
  shows ◀ value ▶ in its own, darker chip, darker still while the entry has the focus; ◀ ▶ and a click on the arrows
  still change it. The rows of "Controller" and "Verwalten" look as before. · E2E: no (window: colours)
- [x] **T531-8** **Owner:** On your PC with more launcher tabs than fit the header: open a game's detail page with a
  bright artwork. Where tabs are cut off at the edge of the strip, the cut-off tab's name fades out; there is no dark
  block and no hard edge. In the library the strip's edge looks as before. · E2E: no (real launchers: the demo has
  too few tabs to fill the strip)
- [x] **T531-9** **Owner:** Read the detail page on the TV with two or three of your own games with bright artworks:
  the title, the info line and the note read without effort. · E2E: no (look on the real screen)

Earlier cases this changes: T499 cases that check the title in two lines at full size now see it shrink first; T500
cases on the quick selection's look see the new chips. Their texts stay; the behaviour they check holds.

Automated tests:

| Test cases | Tests |
|---|---|
| T531-3, T531-6 | `DetailReadabilityTests` (the title's size in `ShrinkingTitle.FittingSize`, the line for a missing description) |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T531-1, -3, -4, -5, -7 | 2026-11-13 | Claude (implementer) | branch, Debug, Linux/Xvfb, `--demo --demo-games 60` with bright made-up artworks | ✅ looked at, title renamed to 66 characters | – |
| T531-8 | 2026-11-13 | Claude (implementer) | as above, tab spacing widened locally so the strip overflows | ✅ cut-off names fade, no block | the owner's case stays open for his tabs |
| T531-1, -3, -4, -5, -7, T500-25 (QE run) | 2026-11-13 | Claude (QE) | branch at 34ef9b7, Release, Linux/Xvfb 1920 × 1080, `--demo` and `--demo --first-run`, keys by xdotool; headless frames for Pan's firmware dialog, the status messages and three made-up titles (9, 41, 103 characters) | ✅ T531-1: shadow on title, info line and the stacks' names (demo artworks are dark; bright ones only in the implementer's run); T531-3: 66 pt, shrunk to 61 pt, two lines at 33 pt, button row at the same height in all three; T531-4/-5: title above the description, smaller boxes, nothing cut; T531-7: ◀ value ▶ in its own darker chip (demo greys both entries, so the darker chip with the focus only mechanically); raised buttons with whole rings in the detail page, library preview, „Fertig“, Pan's dialog, the status messages; T500-25: under „Ansicht“ the page's ring was visible at a1bed43, gone at 34ef9b7 | ring finding fixed in 34ef9b7 |
| T531-2, T531-8, T531-9 | 2026-11-13 | Owner | `main` after #561, Release, his PC, TV, Steam Controller, his libraries | ✅ final test of #43; findings went into #560, fixed by PR #561 | – |
