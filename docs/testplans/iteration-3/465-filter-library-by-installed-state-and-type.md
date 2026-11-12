# #465 Filter the library by installed state and type

Part A of #44: the modal "Filter" with "Installiert" and "Typ", the funnel beside "Ansicht", the no-hits message and
the filter per tab (designs: artboards "#44" T44F01–T44F03, T44F09–T44F12, T44Library, T44Modal, T44Bausteine).
Controller only unless a case says otherwise; B goes back. The issue had no test cases from the quality engineer
before implementation: these are #44's scenarios 1–10 and failure cases 17–19 one to one, plus #44's visual
checklist as far as part A has it, for the quality engineer to check in the review. Automated tests use
`ArcadiaDriver` (fake launchers, fake time) in `LibraryFilterTests`, the filter's logic `GameFilterTests`.

**Status:** Automated: T465-1 to T465-9, T465-11 to T465-13 · QE: T465-10, T465-14 · Open for the owner: –

**Modal**

- [x] **T465-1** In a tab with installed and uninstalled entries of several types, R3 (G; a click on the funnel) opens
      "Filter": categories "Installiert" and "Typ" left, the values of the focused one right, on top "Mit dieser
      Auswahl 7 von 7 Einträgen". · E2E: yes
- [x] **T465-2** "Nur installierte" chosen: the number on top changes at once, the library doesn't; B (back to the
      categories) and B again, or R3 from anywhere, closes the modal, the filter applies, page 1, first hit focused, page
      sizes count only the hits. · E2E: yes
- [x] **T465-3** "Nur nicht installierte" and type "Werkzeuge": only uninstalled tools; "Werkzeuge" and "Software": both.
      The category's line reads "Software, Werkzeuge" (or "Mehrere" where that doesn't fit). · E2E: yes
- [x] **T465-4** A tab with games only has no "Typ"; a tab where everything is installed has no "Installiert"; a tab
      with neither shows the funnel alone, without R3, and R3 does nothing. · E2E: yes
- [x] **T465-5** Without a filter the funnel is neutral; with one the chip stays neutral, the funnel is filled and "3 von 7" shows, both in the soft accent;
      the count line beside the tab's name still counts the whole tab. · E2E: yes
- [x] **T465-6** A filter without hits: "Keine Einträge passen zu deinen Filtern.", below "<Launcher> hat <M> Einträge,
      der Filter blendet gerade alle aus." and "[R3] Filter ändern" (R3 or A opens the modal); the funnel reads
      "0 von M"; the footer shows Launcher and Ansicht. · E2E: yes
- [x] **T465-7** "Alle Filter zurücksetzen" is skipped while nothing is chosen; with a filter it is the first row, ▶ on it
      does nothing, A clears everything, the focus goes to the first category; closing leaves the tab unfiltered. · E2E: yes
- [x] **T465-8** A filter in one tab: the next tab is unfiltered; after a restart the first tab has its filter again.
      · E2E: yes
- [x] **T465-9** The detail page turns through the hits only (LT/RT); the search (X) finds entries the filter hides.
      · E2E: yes
- [x] **T465-10** With the mouse: click the funnel, a category, a value, "Alle Filter zurücksetzen"; the ✕ top right and
      a click beside the modal close and apply; the hints show the keyboard's keys (G, C, Leertaste, Esc). · E2E: no
      (layout and real clicks: window test or the quality engineer's run)

**Failure cases**

- [x] **T465-11** A saved value that is gone or unknown (a kind switched off on the launcher page, a rescan, a
      hand-edited `settings.json` with "vielleicht", "7" or a type of a newer version) drops out silently; nothing left,
      the category is unfiltered; the saved choice comes back with the entries, also after opening and closing the
      modal meanwhile, while "Alle Filter zurücksetzen" clears it. · E2E: yes
- [x] **T465-12** A rescan while the modal is open: the counts stay those from opening; closing filters the new
      library. · E2E: yes
- [x] **T465-13** A launcher switched off keeps its filter in `settings.json`; switched on again, it applies.
      · E2E: yes

**Look**

- [x] **T465-14** Focus ring visible on "Alle Filter zurücksetzen", on the last category, on the first and last type in
      the 3-column grid (right edge); margins to the modal's edge top, bottom and beside the footer; "1.204 von 5.000"
      at the funnel (`--demo-games 5000` with a filter); funnel in its three states; hints for Xbox, PlayStation and
      keyboard. · E2E: no (look, the quality engineer's run)

Automated tests:

| Test cases | Tests |
|---|---|
| T465-1 to -9, -11 to -13 | `LibraryFilterTests`, variants in `GameFilterTests` (Core) |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T465-1 to T465-9, T465-11 to T465-13 | 2026-11-04 | Claude (implementation) | branch of the PR, `dotnet test`, Linux container | ✅ | – |
| T465-1 to T465-9, T465-11 to T465-13 | 2026-11-04 | Claude (implementation) | after the QE findings B1–B5, `dotnet test`, Linux container | ✅ | – |
| T465-10, T465-14 | 2026-11-04 | Claude (QE review) | branch at 5738853, Debug build under Xvfb, `--demo --demo-games 5000`, GOG with tools, software and soundtracks; keyboard and mouse, no gamepad | ✅ after fixes | first run found B1 (▶ on the reset row: two focus rings) and B2 (mouse reset left the focus in hidden values), fixed in 5738853; PlayStation hints checked only in code (R3/L3 glyphs are the same) |
