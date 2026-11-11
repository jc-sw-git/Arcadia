# #499 Detail page: new layout with areas, carousel into depth and info box

Derived from the specification in #498 (scenarios 1–11, the failure cases of slice 1, the visual checklist without the
quick selection) and the approved artboard "Detailseite neu: Mischung aus A, B und C". Every case runs with the
gamepad alone; keys follow `docs/product/controls.md` (WASD, Space = A, Esc = B, Q/E = LT/RT, Shift+Q/E = LB/RB).
"The rail" is the list of areas ("Übersicht", "Controller", "Verwalten"); "the rows" are an area's content.
Automated cases use the demo or made-up Steam and Epic fixtures (`ArcadiaDriver`), window cases `ArcadiaWindow`.
Replaces T11-7 (three rows of buttons) and the layout parts of T11-1 and T11-8; T476-5's "greyed-out items are passed
over" no longer holds for the rows of an area (T499-14). The owner's cases are collected for the owner's test at the
end of the detail page work.

**Status:** Automated: – · QE: – · Open for the owner: T499-30, T499-31

Page and button row

- [ ] **T499-1** Open a game's detail page from the library (A held 1 s) and from the search: the game's artwork fills
      the screen under a scrim, the launcher tabs stay on top; title, info line and the row "Spielen", "Zu Favoriten",
      "In <Launcher> öffnen" stand on the artwork; the focus is on "Spielen"; the rail shows "Übersicht", "Controller"
      and "Verwalten", each with its value line, "Übersicht" open with the game's facts in full width and no info box.
      · E2E: yes
- [ ] **T499-2** Button row: ◀ ▶ move between the buttons and go round at the ends; a disabled button (e.g. "In … öffnen"
      without a launcher target) is passed over and tells why in its tooltip; A on each does what it does today. A
      says "Tüfteln" for software, "Abrocken" for a soundtrack, "Installieren" when not installed, shows the progress
      during an installation and "Zurück ins Spiel" / "Weiter tüfteln" while it runs (regression). · E2E: yes
- [ ] **T499-3** The quiet note beside the buttons: with an update "Update verfügbar" and "<Launcher> installiert es
      eventuell vor dem Start."; for a soundtrack "Steam spielt den Soundtrack ab."; the installation note as today.
      The focus never lands on it (▶ on the last button goes round to "Spielen", ▼ goes to the rail). An update
      Steam's install monitor reports appears within a poll, without a rescan or toast, and goes again when Steam drops
      it (#288). · E2E: yes
- [ ] **T499-4** Leave the page on "Verwalten" with the focus on a row, open it again (same or other game): it starts on
      "Spielen" with "Übersicht" open. · E2E: yes

Rail and rows

- [ ] **T499-5** From any button ▼ puts the focus on the open area in the rail. ▲ ▼ move through the areas and each
      focused area shows its content at once; ▼ on "Verwalten" goes round to "Übersicht"; ▲ on "Übersicht" returns to
      "Spielen" (also when ▼ came from another button). ◀ ▶ in the rail do nothing. · E2E: yes
- [ ] **T499-6** A on "Controller" (and on "Verwalten") in the rail: the focus is on the area's first row; ▲ ▼ move through
      the rows and go round at the ends; B returns to that area in the rail; B in the rail or on the button row returns
      to where the page was opened from (T499-11). · E2E: yes
- [ ] **T499-7** A on "Übersicht" in the rail does nothing and moves no focus (no rows in slice 1; open question 1).
      · E2E: yes
- [ ] **T499-8** Focus on "Steam-Controller-Modus": ▶ changes the value at once to the next, ◀ to the previous, going round
      at the ends; the rail's line for "Controller" and the row show the new value; it holds for this game only, also
      after leaving and reopening the page and after a restart of Arcadia. The same for "Individueller Mausmodus".
      A held ◀ ▶ stops at the end of the range and doesn't flip a switch back and forth (T476-9). · E2E: yes
- [ ] **T499-9** "Artwork ändern" in "Verwalten", A: the artwork dialog opens as today; after B the focus is on "Artwork
      ändern" again and "Verwalten" is still open. "Deinstallieren …", A: the same question as in the context menu;
      "Abbrechen" or B returns to that row, confirming hands over to the launcher as today. · E2E: yes
- [ ] **T499-10** Button hints follow the focus: on the button row A names the button and B "Zurück"; in the rail A
      opens the area's rows (none on "Übersicht") and B "Zurück"; on a value row ◀ ▶ "Ändern", on an action row A
      names it, and B leads back to the rail. LT/RT "Spiel" and LB/RB "Launcher" show only where they act. · E2E: yes
- [ ] **T499-11** B from the button row or the rail: back in the library on the page of the game shown last, the focus
      on it; opened from the search, back in the search as today. · E2E: yes
- [ ] **T499-12** LB/RB switch the launcher tab and stay on the detail page with the tab's first game; L3 opens "Ansicht"
      and a new sort keeps the shown game; X opens the search over the page and B returns to it; Y opens the status
      messages and B closes them. After each the focus and the open area are where they were (open question 2).
      · E2E: yes
- [ ] **T499-13** While the artwork dialog or the uninstall question is open, LT/RT and LB/RB do nothing and the page
      behind keeps its game, area and focus. · E2E: yes

Settings that don't apply now, info box

- [ ] **T499-14** No Steam Controller connected: "Controller" shows "Steam-Controller-Modus" greyed out; it still takes the
      focus (▲ ▼ stop on it), ◀ ▶ and A do nothing, the info box says why. The controller connects while the focus is
      on it: the row becomes active, the focus stays; it disconnects: greyed again, the focus stays. · E2E: yes
- [ ] **T499-15** Every other state where a row doesn't apply now shows it greyed with its reason in the info box, never
      hidden: "Steam-Controller-Modus" while the game runs (applies at the next start); "Individueller Mausmodus" while
      the mouse mode in games isn't set per game; "Deinstallieren …" during an installation, while the game runs, and
      when only the launcher can uninstall and Arcadia can't open it. "Deinstallieren …" that opens the launcher
      instead stays active and its info box says so. Pan's emulation not running (#242): the mode row says so and A
      leads to the row in the settings that says why, as today. · E2E: yes
- [ ] **T499-16** Info box: with the focus on a row of "Controller" or "Verwalten" the box beside the rows shows the
      row's name and what it does, and follows the focus (▲ ▼, mouse over a row). "Übersicht" has no box. · E2E: yes
- [ ] **T499-17** A long info text scrolls or fades inside the box; the rows keep their width whatever the box shows.
      · E2E: yes (window test)

Carousel into depth

- [ ] **T499-18** Focus on the second row of "Controller", RT: the next game's cover comes from the right stack to the
      front, the game shown goes into the left stack, the background blends, title, info line, button row and rows
      show the new game; "Controller" stays open and the focus stays on the second row, also when that row is greyed
      for the new game. LT the other way round. From the button row and from the rail the focus stays on its button
      or area. · E2E: yes
- [ ] **T499-19** Hold RT: only the covers run, title, info line, background and rows keep the game until release, then
      show the game reached; area and focus are kept; held past the last game it goes round without a stop (T476-16).
      · E2E: yes
- [ ] **T499-20** Stacks: with three or more games each side shows a stack; with two the other game on both sides; with one
      no stacks, LT/RT do nothing and their hint is gone. · E2E: yes
- [ ] **T499-21** A game without artwork: the background is the launcher's colour as today and title, info line, buttons,
      rail and rows stay readable. · E2E: yes (backdrop), visual part in T499-27

State changes, mouse, regression

- [ ] **T499-22** While the page is open: an installation finishes ("Installieren" becomes "Spielen", "Deinstallieren …"
      active), an update appears or goes, a game starts or ends, a rescan changes the tab's games: buttons, note, rows
      and value lines update in place; the page stays on its game and the focus on its button, area or row. A rescan
      that removes the shown game behaves as today. · E2E: yes
- [ ] **T499-23** Mouse: a click on an area opens it, a click on a row focuses it, the mouse over a row focuses it, the
      arrows beside a value change it, a click on a stack turns the carousel, the buttons click as today, the dialogs
      show ✕. · E2E: yes (window test)
- [ ] **T499-24** The library's context menu offers the same actions as the detail page (incl. "Artwork ändern",
      "Steam-Controller-Modus", "Individueller Mausmodus", "Deinstallieren …") with the same results. · E2E: yes

Look (QE run of the app, visual checklist of #498)

- [ ] **T499-25** Focus ring fully visible, not clipped, on "Spielen", the last button of the row, the first and last area of
      the rail and the first and last row of "Controller" and "Verwalten"; exactly one ring on the page at any time,
      none behind an open dialog; the note beside the buttons has no surface, border or ring. · E2E: yes (ring and
      clipping as window test), look no (visual judgement)
- [ ] **T499-26** A long title takes two lines, then ends with "…", and pushes neither the buttons under the panel nor the
      info line off the artwork; a long launcher name in "In … öffnen" and long values in the rail end with "…" in one
      line. · E2E: yes (window test)
- [ ] **T499-27** Text stays readable over a bright, a dark and no background image (scrim); the stacks' inner edges face the
      middle, the second cover is smaller and darker, nothing overlaps the panel or the footer hints; the turn, the
      blend and the fade of title and rows finish without a jump. · E2E: no (visual judgement, motion)
- [ ] **T499-28** Margins match the artboard at 1920×1080; at 200 % scaling and at 1280×720 nothing is cut or overlaps.
      No colour, brush or shadow value outside `Theme/Theme.axaml` (scrim, surface under rail and rows). · E2E: no
      (comparison with the artboard; the theme part is checked on the diff)
- [ ] **T499-29** Budgets, measured mechanically in the PR: gamepad input to visible focus move < 50 ms for ▲ ▼, ◀ ▶ and a
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

Open questions (answer before the PR, then fix the case):

1. A on "Übersicht" in the rail (T499-7): proposed "does nothing", since slice 1 has no rows there.
2. LB/RB, X/B, L3 and Y (T499-12): proposed that the open area and the focus stay, as for a carousel turn; only opening
   the page anew starts on "Spielen".

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
