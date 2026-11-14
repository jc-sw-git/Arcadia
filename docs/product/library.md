# Product decisions: Library

- Page sizes 24/64/128, default 24: 3, 8 and 16 full rows of 8, so a full page never ends in a half row (the last page
  of a tab may). A size is selectable if the library has *more* games than the next smaller size
  (`PageSizes.IsAllowed`). A saved size Arcadia doesn't offer (an older version's 10/25/50/100, one edited by hand)
  becomes 24 at the start and is saved so.
- The focus moves through all games of the tab, not only the page (#476): right on a page's last game goes to the
  next page's first, down in its bottom row to the next page's top row, same column (its last game if that row is
  shorter), left and up the same way back; from the last page round to the first and back. A held stick or D-pad
  stops at the page's edge; only a fresh press crosses onto the next page. LT/RT (Q/E, Page Up/Down,
  the buttons below the grid) turn the page and go round the same way; a page number goes straight to its page.
- Covers are 2 : 3 wherever one is shown: library tile 146 × 219 (its height follows the column's width), search hit
  190 × 285, detail page 180 × 270.
- Covers and backgrounds (#13, #487): the cover fills the tile, the search hit and the detail page's cover; the background
  (hero) fills the detail page's header and the preview's top (400 × 180), each cropped centred, never stretched. Until
  an image is there the placeholder (hue, monogram, title) shows; with it, monogram and title hide, badges and the
  "not installed" shade stay on top. A launcher's images are checked again at every scan, so an image changed in the
  launcher (e.g. an own cover set in Steam) shows after the next scan; an image set by hand is never replaced by that.
  A damaged image counts as missing and is loaded again, also where the metadata is unreadable.
- Sorting: name / install date / last played, each ascending or descending; games without a
  date always last.
- Sort order and preferred page size are global, not per tab, and are saved; Arcadia opens in the
  tab the user last chose, always on page 1 with the first game focused.
- Filter (#44, part A #465): R3 (G, a click on the funnel right of "Ansicht") opens "Filter" over the library: categories
  left ("Alle Filter zurücksetzen" first, focusable only while something is chosen), the values of the focused one right,
  on top "Mit dieser Auswahl N von M Einträgen". AND between categories, OR within one. A choice applies only when the
  modal closes (B on the categories, R3, ✕; no cancel), then page 1, first hit focused; page sizes count the hits.
  "Installiert": Alle / Nur installierte / Nur nicht installierte; "Typ": Alle Typen and the tab's types (several),
  each value with the hits it would bring. A category shows only where it tells the tab's entries apart; with none,
  the funnel stands alone and R3 does nothing. Per tab, saved; a value the tab no longer holds drops out, the saved
  choice stays (back with the entries, also for a switched-off launcher and after closing the modal meanwhile;
  "Alle Filter zurücksetzen" clears it too). The funnel is neutral without a filter,
  with one the chip stays neutral, only the funnel fills and "19 von 412" shows in the soft accent;
  the count line keeps counting the whole tab. No hits: "Keine
  Einträge passen zu deinen Filtern." with "[R3] Filter ändern" (A too). The detail page turns through the hits only and opens the filter too
  (`detail-page/`).
- Filter by metadata (#466): "Genre", "Tags", "Entwickler", "Publisher", "Erscheinungsjahr", "Altersfreigabe", each only
  where an entry of the tab has it. Lists: "Alle …" first, every value with its hits; "Häufigste zuerst" (chosen first,
  then hits, 0 hits greyed last) or A–Z with letters (genre starts A–Z, the others by frequency), L3 switches, kept per
  category while the modal is open; LT/RT a page or, in A–Z, a letter; X searches the values with the on-screen keyboard
  (cleared when leaving the category). Names count as one value ignoring case and outer spaces, shown in their most
  frequent spelling; genres are saved by key. Own tags come first under "EIGENE TAGS", apart from a provider tag of the
  same name; a provider tag the game also has as an own one shows only as the own one (as on the detail page); a
  renamed or merged own tag follows into the filter, a deleted one drops out. Year: chart, quick picks ("Letzte 3
  Jahre", decades, "Vor 2000"), "von"/"bis" (◀▶ one year, LT/RT ten, within the tab's years, "von" never after
  "bis"); the tab's whole range is "Alle Jahre". Age rating (provisional): one system (the region's, USK until #557),
  then one or more of its ratings; no conversion between systems, another system clears the ratings. Once a value is
  chosen, "Ohne Angabe einbeziehen" adds the entries without that metadata. Metadata arriving later re-filters the
  library; a modal opened during a metadata run (reading the launchers' files included) says so and keeps its counts and categories.
- The header's tab strip sits in the middle between logo and status, as wide as its tabs need, LB/RB right beside
  them. At its widest it keeps 40 px to both, with a divider in each gap; the selected tab stands in its middle and
  it fades out where more tabs follow: the cut-off tab itself turns transparent, nothing is laid over it, so over the
  detail page's artwork no dark block shows (#531). Dividers also stand between a status message and the search chip and
  between the settings chip and the date.
- Entry types (#141): Spiel, Software, Werkzeug, Soundtrack (`Game.Type`). A launcher
  says which it delivers and offers a switch per kind on its page (`docs/product/launchers.md`); games are always shown.
  Software, tools and soundtracks carry their icon (Software a gear with `</>`, Werkzeug a wrench, Soundtrack a note) in
  a 26 px rounded square directly above the title on a tile and a search hit, a line with icon and name under the
  preview's title and a chip after the launcher chip on the detail page; games get none. Round = state (top right),
  rounded square = kind.
- Count line beside the tab's name (#141): one part per type in
  the order Spiele, Software, Werkzeuge, Soundtracks, each total + type icon, then "(installed + drive-with-check icon)"
  unless all are installed; types without entries left out; parts split by a 1 × 24 px divider; a tooltip says it in
  words ("331 Spiele, davon 1 installiert · 2 Werkzeuge"). No page number (the pager shows it); an empty tab keeps its
  text. The tab's number counts every entry shown. The filter (#44) will reuse the icons.
