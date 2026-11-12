# #506 A Detail page and info box: fast turning, value chips, one box per value, note on top, scrolling

Part A of #506 (scenarios 1–5 and the failure case "info box changes while scrolled"). Part B (when artwork is
searched) and part C (waiting list) have their own files; part A uses the IDs T506-1 to T506-39.

**Status:** Automated: – · QE: – · Open for the owner: T506-38, T506-39

Every case runs with the gamepad alone; keys follow `docs/product/controls.md` (WASD, Space = A, Esc = B, Q/E = LT/RT,
Shift+W/S = RS). "A jump" is the animation of one carousel turn: the cover moving out of its stack, the background
blending, title, info line and rows fading to the new game. "The info box" is the box beside the rows of "Controller"
and "Verwalten" on the detail page, and the info panel beside the rows on the settings page. "Value boxes" are the
small boxes in it, one per value of a value row. "The blue note" is the note with the round blue i. Automated cases
use the demo or made-up Steam and Epic fixtures (`ArcadiaDriver`) and a fake Steam Controller, window cases
`ArcadiaWindow`. Open questions Q1 to Q3 (bottom) are answered with their recommendation unless the owner says
otherwise; the cases follow the recommendation (the implementation follows all three).

**Fast turning (scenario 1)**

- [ ] **T506-1** One RT, then nothing: one jump to the next game as today (T499-18); area and focus kept. · E2E: yes
- [ ] **T506-2** RT, and RT again while that jump runs: the running jump stops, title, info line, background, cover
  and rows show the game after next at once, without a jump of their own; nothing moves afterwards (no jump replayed
  1 s later). · E2E: yes
- [ ] **T506-3** Five presses of E within one jump's time: after the fifth press the fifth game after the start is
  shown, at most the first press jumped; when the presses end, the page stays on that game and no jump follows, also
  after waiting 2 s. · E2E: yes
- [ ] **T506-4** Mixed quick presses (RT, RT, LT): the page ends on the game after the start, never shows the
  games in between afterwards; over the end of the tab's games it goes round as a single turn does. · E2E: yes
- [ ] **T506-5** Mouse wheel turned quickly over the page (outside the info box, Q1): the same as T506-3, one notch is
  one game; one notch alone jumps as RT does. · E2E: yes (window test)
- [ ] **T506-6** A press after a jump has finished jumps again normally: fast turning leaves no "instant" state
  behind. RT held, released, then RT pressed quickly twice: held behaves as T499-19 (only covers run, the page follows
  on release), the quick presses as T506-2. · E2E: yes
- [ ] **T506-7** Fast turning with the focus on the second row of "Controller": every game shown keeps "Controller"
  open and the focus on that row, greyed or not (T499-18), its value chip and info box show the newest game's values;
  the button hints match the newest game. · E2E: yes
- [ ] **T506-8** Fast turning with only two games, or one: with two it alternates correctly and ends on the right game;
  with one LT/RT and the wheel do nothing (T499-20). · E2E: yes
- [ ] **T506-9** While the artwork dialog or the uninstall question is open, quick LT/RT and the wheel do nothing and
  queue nothing: after B the page shows the game it showed before, no jump runs (T499-13). · E2E: yes
- [ ] **T506-10** Budget, measured mechanically: from each press of a quick series to the visible title of the newest
  game < 50 ms; a cancelled jump frees the UI thread at once (no frame waits for the stopped animation).
  · E2E: no (timing; T506-38 confirms on the owner's PC)

**Value chip (scenario 2)**

- [ ] **T506-11** "Controller" on the detail page: "Steam-Controller-Modus" and "Individueller Mausmodus" show their value
  in the same chip as the value rows of the settings page (same size, surface, ◀ ▶ beside it); a greyed row shows a
  greyed chip, the same as a greyed settings row. · E2E: yes (window test)
- [ ] **T506-12** ◀ ▶ on each of the two rows: the chip shows the new value at once, going round at the ends; the rail's
  value line for "Controller" follows; a click on the arrows beside the chip does the same (T499-23). · E2E: yes
- [ ] **T506-13** A long value (e.g. "Standard (Steam-Eingabe)") fits the chip or ends with "…" in one line; the focus
  ring around the row is whole, the row keeps its height. No colour, brush or shadow of the chip outside
  `Theme/Theme.axaml`. · E2E: yes (window test), theme part checked on the diff

**One box per value (scenario 3)**

- [ ] **T506-14** Focus on "Steam-Controller-Modus": the info box shows, from the top, the row's name, then one value box
  per mode saying what it does, the box of the current mode highlighted, then the rest of the row's text. The same on
  "Individueller Mausmodus" with its values. · E2E: yes
- [ ] **T506-15** ◀ ▶ on a value row: the highlight moves to the new value's box in the same step as the chip changes,
  both ways and going round; held ◀ ▶ (T476-9) ends with the highlight on the value reached. · E2E: yes
- [ ] **T506-16** Settings page, every category: each value row with named values gets the value boxes the same way,
  e.g. "Mausmodus", "Beenden des Mausmodus in Spielen erzwingen", "Standardeinstellung bei „Individuell“", a launcher
  page's "Steam Controller", "Zuschneiden erlaubt", "Bei mehreren Treffern", and the switches (a box "Aus", then a box "An", in
  the order ◀ ▶ go through them, Q2). ◀ ▶ moves the highlight as in T506-15. · E2E: yes
- [ ] **T506-17** Rows with a plain range of numbers get no value boxes: "Haltedauer" (mouse mode and "Mit ⧉ zurück zu
  Arcadia"), "Zeigergeschwindigkeit"; their info box reads as before. Action and info rows ("Artwork ändern",
  "Deinstallieren …", "Jetzt suchen", "Letzte Meldungen") get none either. The logging switches ("Info", "Warnungen", "Fehler") keep showing
  "Letzte Meldungen" instead of value boxes, since their info box is that list. · E2E: yes
- [ ] **T506-18** A value changed elsewhere while the row is shown (a click on the arrows, the library's context menu
  for the same game, Pan switched off or on): the highlight shows the value now in use. Opening the detail page of a
  game whose mode was set in the context menu: the right box is highlighted at once. · E2E: yes
- [ ] **T506-19** A greyed value row (no Steam Controller): the value boxes are shown, the current one highlighted,
  at half opacity like the row; ◀ ▶ move neither chip nor highlight. · E2E: yes
- [ ] **T506-20** A launcher page's row "Steam Controller": its value boxes replace the mode cards "Arcadia" and
  "Steam", current one highlighted, never both. "Controller-Wächter (Pan)" keeps the mode cards in its section "Steam
  Controller" (they aren't the row's values) and gets its own boxes "Aus" and "An" above them. · E2E: yes
- [ ] **T506-21** Look: the value boxes and the highlight are the same on the detail page and the settings page (one
  template), the current box has a 2 px border in the accent blue and a brighter title, so it is
  visible without colour alone (Q3), and its colours are resources in
  `Theme/Theme.axaml`. · E2E: yes (one template, window test), look no (visual judgement)
- [ ] **T506-22** Value boxes don't change the width of the rows or the box, and the mouse over a value box focuses
  nothing and changes nothing (only the row's arrows change a value). · E2E: yes (window test)

**Note on top (scenario 4)**

- [ ] **T506-23** A game that isn't installed: on every row of "Controller" and "Verwalten" the blue note "Einstellbar,
  sobald das Spiel installiert ist." (or the row's own reason) stands at the very top of the info box, above the name.
  · E2E: yes
- [ ] **T506-24** "Individueller Mausmodus" while the mouse mode in games isn't set per game: its reason as blue note at
  the top, then name, value boxes and text. · E2E: yes
- [ ] **T506-25** No Steam Controller: "Steam-Controller-Modus" shows its reason as blue note at the top; the controller
  connects with the focus on the row: the note goes, the row is active, the box starts with the name; it disconnects:
  the note is back at the top. The focus stays on the row throughout (T499-14). · E2E: yes
- [ ] **T506-26** Every other greyed state of T499-15 (game running, "Deinstallieren …" during an installation, while the
  game runs, without a way to uninstall; Pan's emulation not running) puts its reason as blue note at the top. An
  active row whose box has a note ("Deinstallieren …" that opens the launcher) shows that note at the top too.
  · E2E: yes
- [ ] **T506-27** Settings page: the blue note stands at the very top only where a row has a reason text: a launcher
  page's "Steam Controller" while it can't be changed, and "Zurücksetzen" while Pan is off. A row without a reason or
  note, on either page, shows no empty note and no gap above the name. · E2E: yes (gap as window test)

**Scrolling (scenario 5 and the failure case)**

- [ ] **T506-28** Focus on a row whose info box doesn't fit (e.g. a long text with value boxes and a note; at 1280×720
  if 1920×1080 fits everything): the box fades at the cut edge; RS down (Shift+S) scrolls it, a fade appears at the top,
  the bottom fade goes at the end and RS stops there (no wrap); RS up back to the start. The focus stays on the row and
  ◀ ▶ still change its value. · E2E: yes
- [ ] **T506-29** A box that fits: no fade; RS and Shift+W/S do nothing at all (no scroll, no focus move, no turn of the
  carousel, no other action). · E2E: yes
- [ ] **T506-30** Footer: it names RS only while the focused row's box overflows; moving the focus from an overflowing
  row to a fitting one and back adds and removes the hint at once; in the button row, the rail and "Übersicht" RS
  does nothing and has no hint. · E2E: yes
- [ ] **T506-31** Scrolled down, then ◀ ▶ change the value: the box is at the top again (note and name visible), on the
  detail page and on the settings page. · E2E: yes
- [ ] **T506-32** Scrolled down, then the row greys or becomes active (Steam Controller disconnects or connects, the
  game starts or ends): the box is at the top again. Scrolled down, then ▲ ▼ to another row or LT/RT to another game:
  the new box starts at the top. · E2E: yes
- [ ] **T506-33** Settings page regression: RS, Shift+W/S and the wheel still scroll the info panel with its fades
  (#15, #433), "Letzte Meldungen" still scrolls its list; a value change scrolls the panel back to the top.
  · E2E: yes
- [ ] **T506-34** Mouse on the detail page: the wheel over an overflowing info box scrolls the box and doesn't turn the
  carousel; over a fitting box or anywhere else it turns the carousel (Q1). · E2E: yes (window test)
- [ ] **T506-35** The fades of the info box cut no line in half at the start and end positions, and the text under a
  fade stays readable over bright, dark and no artwork; the fade's colours are resources in `Theme/Theme.axaml`.
  · E2E: yes (fade at the edge only, window test), look no (visual judgement)

**QE run of the app**

- [ ] **T506-36** Xvfb 1920×1080 and 1280×720, `--demo`, keys only: quick E/Q series and the wheel on the detail page,
  "Controller" with value boxes and note on a game not installed and one installed, a box scrolled with Shift+S and
  reset by a value change; the settings categories "Controller", "Datenquellen" and a launcher page with value boxes.
  Nothing cut, one ring at a time, highlight readable. · E2E: no (visual judgement)
- [ ] **T506-37** Red once: each new acceptance test failed once against a build that replays the queued jumps, gives
  no highlight, puts the note under the name, ignores RS or keeps the scroll offset after a value change.
  · E2E: no (process check of the PR's note)

**For the owner (own PC, real controllers and artwork)**

- [ ] **T506-38** **Owner:** on the real PC with the Steam Controller and an Xbox pad, detail page of a real game: tap
  RT and LT quickly several times, then turn the mouse wheel quickly: the newest game is there at once, nothing jumps
  afterwards; a single press still turns smoothly; it feels instant (< 50 ms). · E2E: no (real controllers, feel on the
  owner's PC)
- [ ] **T506-39** **Owner:** in "Controller" of a real game change "Steam-Controller-Modus" with ◀ ▶: chip and
  highlighted value box follow at once; switch the Steam Controller off: the blue note is at the top; scroll a long info
  box with the right stick of both controllers and change the value: back at the top. In the settings a category with
  value boxes looks the same. · E2E: no (real controllers and artwork)

**Touches:** T499-16 (info box content, now with note, value boxes and text in this order), T499-17 (long info text,
now scrolled with RS, T506-28 to -35), T499-18 and T499-19 (turning, extended by T506-1 to -9).

**Open questions (answered with the recommendation unless the owner objects)**

- **Q1** (confirmed by the implementation) The mouse wheel turns the carousel and scrolls the settings' info panel; recommended: over an overflowing
  info box on the detail page it scrolls the box, elsewhere it turns the carousel.
- **Q2** (confirmed, boxes "Aus" then "An") Switches are value rows with two named values; recommended they get the two value boxes like every choice,
  so the layout is the same everywhere a box explains value rows; only plain number ranges have none.
- **Q3** (confirmed: 2 px accent-blue border and a brighter title) The highlight needs a mark besides colour (border or check), so it is readable for players who can't tell
  the colours apart; recommended.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
