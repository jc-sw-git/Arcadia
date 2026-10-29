# Product decisions: Library

Agreed with the owner; don't change without asking. Read this before changing this area
(index in `CLAUDE.md` → Product decisions).

- Page sizes 10/25/50/100, default 25. A size is selectable if the library has *more* games
  than the next smaller size (`PageSizes.IsAllowed`).
- Sorting: name / install date / last played, each ascending or descending; games without a
  date always last.
- Sort order and preferred page size are global, not per tab, and are saved; Arcadia opens in the
  tab the user last chose, always on page 1 with the first game focused (owner, 2026-10-17).
- The header's tab strip sits in the middle between logo and status, as wide as its tabs need, LB/RB right beside
  them. At its widest it keeps 40 px to both, with a divider in each gap; the selected tab stands in its middle and
  it fades out where more tabs follow. Dividers also stand between a status message and the search chip and
  between the settings chip and the date (owner, 2026-10-21).
- Entry types (#141, owner 2026-10-22 and 2026-10-26): Spiel, Software, Werkzeug, Soundtrack (`Game.Type`). A launcher
  says which it delivers and offers a switch per kind on its page (`docs/product/launchers.md`); games are always shown.
  Software, tools and soundtracks carry their icon (Software a gear with `</>`, Werkzeug a wrench, Soundtrack a note) in
  a 26 px rounded square directly above the title on a tile and a search hit, a line with icon and name under the
  preview's title and a chip after the launcher chip on the detail page; games get none. Round = state (top right),
  rounded square = kind.
- Count line beside the tab's name (#141, replaces "331 Spiele · 1 installiert · Seite 1 von 14"): one part per type in
  the order Spiele, Software, Werkzeuge, Soundtracks, each total + type icon, then "(installed + drive-with-check icon)"
  unless all are installed; types without entries left out; parts split by a 1 × 24 px divider; a tooltip says it in
  words ("331 Spiele, davon 1 installiert · 2 Werkzeuge"). No page number (the pager shows it); an empty tab keeps its
  text. The tab's number counts every entry shown. The filter (#44) will reuse the icons.
