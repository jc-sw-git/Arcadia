# #499 Detail page: new layout with areas, carousel into depth and info box

Derived from the specification in #498 (scenarios 1–11, the failure cases of slice 1, the visual checklist without the
quick selection) and the approved artboard "Detailseite neu: Mischung aus A, B und C". Every case runs with the
gamepad alone; keys follow `docs/product/controls.md` (WASD, Space = A, Esc = B, Q/E = LT/RT, Shift+Q/E = LB/RB).
"The rail" is the list of areas ("Übersicht", "Controller", "Verwalten"); "the rows" are an area's content.
Automated cases use the demo or made-up Steam and Epic fixtures (`ArcadiaDriver`), window cases `ArcadiaWindow`.
Replaces T11-7 (three rows of buttons) and the layout parts of T11-1 and T11-8; T476-5's "greyed-out items are passed
over" no longer holds for the rows of an area (T499-14). The owner's cases are collected for the owner's test at the
end of the detail page work.

**Status:** Automated: T499-1 to -26 · QE: T499-27 to -29 · Open for the owner: T499-30, T499-31

Page and button row

- [x] **T499-1** Open a game's detail page from the library (A held 1 s) and from the search: the game's artwork fills
      the screen under a scrim, the launcher tabs stay on top; title, info line and the row "Spielen", "Zu Favoriten",
      "In <Launcher> öffnen" stand on the artwork; the focus is on "Spielen"; the rail shows "Übersicht", "Controller"
      and "Verwalten", each with its value line, "Übersicht" open with the game's facts in full width and no info box.
      · E2E: yes
- [x] **T499-2** Button row: ◀ ▶ move between the buttons and go round at the ends; a disabled button (e.g. "In … öffnen"
      without a launcher target) is passed over and tells why in its tooltip; A on each does what it does today. A
      says "Tüfteln" for software, "Abrocken" for a soundtrack, "Installieren" when not installed, shows the progress
      during an installation and "Zurück ins Spiel" / "Weiter tüfteln" while it runs (regression). · E2E: yes
- [x] **T499-3** The quiet note beside the buttons: with an update "Update verfügbar" and "<Launcher> installiert es
      eventuell vor dem Start."; for a soundtrack "Steam spielt den Soundtrack ab."; the installation note as today.
      The focus never lands on it (▶ on the last button goes round to "Spielen", ▼ goes to the rail). An update
      Steam's install monitor reports appears within a poll, without a rescan or toast, and goes again when Steam drops
      it (#288). · E2E: yes
- [x] **T499-4** Leave the page on "Verwalten" with the focus on a row, open it again (same or other game): it starts on
      "Spielen" with "Übersicht" open. · E2E: yes

Rail and rows

- [x] **T499-5** From any button ▼ puts the focus on the open area in the rail. ▲ ▼ move through the areas and each
      focused area shows its content at once; ▼ on "Verwalten" goes round to "Übersicht"; ▲ on "Übersicht" returns to
      "Spielen" (also when ▼ came from another button). ◀ ▶ in the rail do nothing. · E2E: yes
- [x] **T499-6** A on "Controller" (and on "Verwalten") in the rail: the focus is on the area's first row; ▲ ▼ move through
      the rows and go round at the ends; B returns to that area in the rail; B in the rail or on the button row returns
      to where the page was opened from (T499-11). · E2E: yes
- [x] **T499-7** A on "Übersicht" in the rail does nothing and moves no focus (no rows in slice 1; open question 1).
      · E2E: yes
- [x] **T499-8** Focus on "Steam-Controller-Modus": ▶ changes the value at once to the next, ◀ to the previous, going round
      at the ends; the rail's line for "Controller" and the row show the new value; it holds for this game only, also
      after leaving and reopening the page and after a restart of Arcadia. The same for "Individueller Mausmodus".
      A held ◀ ▶ stops at the end of the range and doesn't flip a switch back and forth (T476-9). · E2E: yes
- [x] **T499-9** "Artwork ändern" in "Verwalten": greyed with "Folgt in einer späteren Version" until the artwork
      dialog exists (#490); then A opens it and after B the focus is on "Artwork ändern" again and "Verwalten" is
      still open. "Deinstallieren …", A: the same question as in the context menu;
      "Abbrechen" or B returns to that row, confirming hands over to the launcher as today. · E2E: yes
- [x] **T499-10** Button hints follow the focus: on the button row A names the button and B "Zurück"; in the rail A
      opens the area's rows (none on "Übersicht") and B "Zurück"; on a value row ◀ ▶ "Ändern", on an action row A
      names it, and B leads back to the rail. LT/RT "Spiel" and LB/RB "Launcher" show only where they act. · E2E: yes
- [x] **T499-11** B from the button row or the rail: back in the library on the page of the game shown last, the focus
      on it; opened from the search, back in the search as today. · E2E: yes
- [x] **T499-12** LB/RB switch the launcher tab and stay on the detail page with the tab's first game; L3 opens "Ansicht"
      and a new sort keeps the shown game; X opens the search over the page and B returns to it; Y opens the status
      messages and B closes them. After each the focus and the open area are where they were (open question 2).
      · E2E: yes
- [x] **T499-13** While the artwork dialog or the uninstall question is open, LT/RT and LB/RB do nothing and the page
      behind keeps its game, area and focus. · E2E: yes

Settings that don't apply now, info box

- [x] **T499-14** No Steam Controller connected: "Controller" shows "Steam-Controller-Modus" greyed out; it still takes the
      focus (▲ ▼ stop on it), ◀ ▶ and A do nothing, the info box says why. The controller connects while the focus is
      on it: the row becomes active, the focus stays; it disconnects: greyed again, the focus stays. · E2E: yes
- [x] **T499-15** Every other state where a row doesn't apply now shows it greyed with its reason in the info box, never
      hidden: "Steam-Controller-Modus" while the game runs (applies at the next start); "Individueller Mausmodus" while
      the mouse mode in games isn't set per game; "Deinstallieren …" during an installation, while the game runs, and
      when only the launcher can uninstall and Arcadia can't open it. "Deinstallieren …" that opens the launcher
      instead stays active and its info box says so. Pan's emulation not running (#242): the mode row says so and A
      leads to the row in the settings that says why, as today. · E2E: yes
- [x] **T499-16** Info box: with the focus on a row of "Controller" or "Verwalten" the box beside the rows shows the
      row's name and what it does, and follows the focus (▲ ▼, mouse over a row). "Übersicht" has no box. · E2E: yes
- [x] **T499-17** A long info text scrolls or fades inside the box; the rows keep their width whatever the box shows.
      · E2E: yes (window test)

Carousel into depth

- [x] **T499-18** Focus on the second row of "Controller", RT: the next game's cover comes from the right stack to the
      front, the game shown goes into the left stack, the background blends, title, info line, button row and rows
      show the new game; "Controller" stays open and the focus stays on the second row, also when that row is greyed
      for the new game. LT the other way round. From the button row and from the rail the focus stays on its button
      or area. · E2E: yes
- [x] **T499-19** Hold RT: only the covers run, title, info line, background and rows keep the game until release, then
      show the game reached; area and focus are kept; held past the last game it goes round without a stop (T476-16).
      · E2E: yes
- [x] **T499-20** Stacks: with three or more games each side shows a stack; with two the other game on both sides; with one
      no stacks, LT/RT do nothing and their hint is gone. · E2E: yes
- [x] **T499-21** A game without artwork: the background is the launcher's colour as today and title, info line, buttons,
      rail and rows stay readable. · E2E: yes (backdrop), visual part in T499-27

State changes, mouse, regression

- [x] **T499-22** While the page is open: an installation finishes ("Installieren" becomes "Spielen", "Deinstallieren …"
      active), an update appears or goes, a game starts or ends, a rescan changes the tab's games: buttons, note, rows
      and value lines update in place; the page stays on its game and the focus on its button, area or row. A rescan
      that removes the shown game behaves as today. · E2E: yes
- [x] **T499-23** Mouse: a click on an area opens it, a click on a row focuses it, the mouse over a row focuses it, the
      arrows beside a value change it, a click on a stack turns the carousel, the buttons click as today, the dialogs
      show ✕. · E2E: yes (window test)
- [x] **T499-24** The library's context menu offers the same actions as the detail page (incl. "Artwork ändern",
      "Steam-Controller-Modus", "Individueller Mausmodus", "Deinstallieren …") with the same results. · E2E: yes

Look (QE run of the app, visual checklist of #498)

- [x] **T499-25** Focus ring fully visible, not clipped, on "Spielen", the last button of the row, the first and last area of
      the rail and the first and last row of "Controller" and "Verwalten"; exactly one ring on the page at any time,
      none behind an open dialog; the note beside the buttons has no surface, border or ring. · E2E: yes (ring and
      clipping as window test), look no (visual judgement)
- [x] **T499-26** A long title takes two lines, then ends with "…", and pushes neither the buttons under the panel nor the
      info line off the artwork; a long launcher name in "In … öffnen" and long values in the rail end with "…" in one
      line. · E2E: yes (window test)
- [x] **T499-27** Text stays readable over a bright, a dark and no background image (scrim); the stacks' inner edges face the
      middle, the second cover is smaller and darker, nothing overlaps the panel or the footer hints; the turn, the
      blend and the fade of title and rows finish without a jump. · E2E: no (visual judgement, motion)
- [x] **T499-28** Margins match the artboard at 1920×1080; at 200 % scaling and at 1280×720 nothing is cut or overlaps.
      No colour, brush or shadow value outside `Theme/Theme.axaml` (scrim, surface under rail and rows). · E2E: no
      (comparison with the artboard; the theme part is checked on the diff)
- [x] **T499-29** Budgets, measured mechanically in the PR: gamepad input to visible focus move < 50 ms for ▲ ▼, ◀ ▶ and a
      carousel turn; start to usable library < 2 s with 1,000 games; < 400 MB RAM with 5,000 games; the page answers
      while covers and backgrounds load. · E2E: no (timing on the real machine; T499-31 confirms)

For the owner

- [ ] **T499-30** **Owner:** On the real PC with the Steam Controller and an Xbox pad: open a few real games, walk button row,
      rail and rows with the controller alone, change "Steam-Controller-Modus" and start the game (the mode holds),
      switch the Steam Controller off and on while "Controller" is open (row greys and comes back, focus stays). Turn
      the carousel single and held: it feels quick, nothing jumps, text is readable on your real artwork. · E2E: no
      (real controllers and artwork)
- [ ] **T499-31** **Owner:** Release build on the real PC: a carousel turn and ▲ ▼ in the rows feel instant (< 50 ms); if the
      blend stutters, the PR says it fell back to a plain cross-fade. · E2E: no (budget on the owner's PC)

Decided with the implementation (as proposed): A on "Übersicht" does nothing (T499-7); LB/RB, X/B, L3 and Y keep the
open area and the focus, only opening the page anew starts on "Spielen" (T499-12).

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T499-1, -2, -4 to -8, -10, -11, -14 to -20 | 2026-11-11 | Claude | PR branch, Debug, Linux cloud container; demo and made-up GOG, Steam and Epic libraries, fake Steam Controller | ✅ `DetailAreasTests`, `DetailPageTests`, `ListWrapTests`, `PanEmulationTests`, `LauncherControllerModeTests`, `RunningGameTests` | T499-9: "Artwork ändern" stays greyed until #490; T499-12: LB/RB automated, L3, X and Y left to the QE run |
| T499-1, -2, -16, -17, -23, -25, -26 | 2026-11-11 | Claude | PR branch, Debug, headless window 1920 × 1080, Linux cloud container | ✅ `DetailPageViewTests`, `ArtworkViewTests`, `ArtworkWindowTests`: one button row above the panel, rings whole on first and last button, area and row, one ring at a time, long title in two lines with "…", rows keep their width, click on area and stack, mouse over a row | clicking the ◀ ▶ arrows left to the QE run |
| T499-1, -3, -5, -6, -18, -27 (look) | 2026-11-11 | Claude | PR branch, Debug, Xvfb 1920 × 1080, `--demo`, keys by xdotool | ✅ screenshots: layout as the artboard, stacks' inner edges to the middle, greyed rows with the reason in the info box, RT keeps "Verwalten" and its second row | software rendering only; motion judged from single frames |
| T499-29 | 2026-11-11 | Claude | Release, Xvfb software rendering, `--demo --demo-games 5000`, `main` measured the same way | ⚠️ start to library 1.74 s (main 1.75 s), 271 MB at the library (main 266 MB), same RSS after the page; ▲ ▼ ◀ ▶ 5 to 37 ms; a carousel turn 29 to 94 ms to the frame (main 25 to 60 ms), of it 4 to 26 ms work | the frame wait is software rendering of the full-screen page while covers move; T499-31 on the owner's PC decides |
| All new tests in `DetailAreasTests`, `DetailPageViewTests` | 2026-11-11 | Claude | PR branch, Debug, Linux cloud container, deliberately broken builds | ✅ each red once: focus reset on a turn (T499-12, -18), the page following a held RT (T499-19), rows never greyed (T499-14, -15), ▲ on "Übersicht" ignored (T499-5), the button row stacked (T499-2, -25, -26), the page opening in the rail (T499-4), B in the rows closing the page (T499-6, -9), ◀ stepping forward (T499-8), no A hint (T499-10), two covers per stack from three games (T499-20), the click on an area ignored (T499-23), the stacks swapped (T11-2), no "▼ Bereiche" hint (T499-1), the info box without fixed width (T499-17) | – |
| T499-3, -12, -13, -21, -22, -24 | 2026-11-11 | Claude (QE) | PR branch, Debug, Linux cloud container, made-up GOG libraries | ✅ new `DetailPageStateTests`, each red once (L3 rebuilding the page on `DetailFocus.Start`; the confirmation letting LT/RT/LB/RB through; no `RebuildActions` on a tile change; no backdrop; uninstall row without its action) | T499-3: the existing poll test in `DetailPageTests` now carries its ID; Y not automated: Arcadia opens the status messages only with entries; covered by the #166 tests |
| T499-23 | 2026-11-11 | Claude (QE) | PR branch, Debug, headless window 1920 × 1080, fake Steam Controller | ❌ `The_arrows_beside_a_value_change_it_both_ways_and_the_rail_shows_the_new_value`: a click on ▶ changes the row's value, the rail still shows the old one | QE finding 1, fixed in this PR |
| T499-1, -5, -6, -14, -15, -18, -27 (look) | 2026-11-11 | Claude (QE) | PR branch, Debug, Xvfb 1920 × 1080, `--demo`, keys by xdotool | ✅ button row, rail, both areas with greyed rows and reasons, info box, RT keeping "Verwalten" and its row, one ring at a time; ⚠️ the front cover of a game not installed and without artwork has no visible edge on its backdrop (finding 2) | 1280 × 720 and 200 % not looked at; motion judged from single frames |
| T499-23 | 2026-11-11 | Claude | PR branch, Debug, headless window, Linux cloud container | ✅ `DetailPageViewTests.The_arrows_beside_a_value_change_it_both_ways_and_the_rail_shows_the_new_value` green after the fix (the area's value line follows every change of a row's value) | QE finding 1 fixed |
| T499-26, -28 | 2026-11-11 | Claude | Release, Xvfb 1280 × 720, `--demo` | ⚠️ then ✅ nothing cut or overlapping at 1280 × 720 (the page scales as one in its `Viewbox`, so 200 % draws the same); the title's line height (68 at 66 px) cut the descenders of "Broken Odyssey" | fixed: the title keeps the font's own line spacing; `A_long_title_…` now checks it (red with the old line height) |
