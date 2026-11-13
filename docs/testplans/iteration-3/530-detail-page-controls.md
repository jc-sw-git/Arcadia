# #530 Detail page controls: focus glyphs, scrolling the info box, switching games and the overview dialog

Findings of the owner's hand test of #43 (detail page from #498/#499/#500), changes 1 to 6 of the issue. The cases were written by the implementing thread from the issue's
changes, as the issue had none; the quality engineer checks them in the review.

**Status:** Automated: T530-1 to -4, T530-8, T530-9 · QE: T530-5 · Open for the owner: T530-6, T530-7, T530-10

Every case runs with the gamepad alone unless it says otherwise; keys follow `docs/product/controls.md` (WASD,
Space = A, Esc = B, Q/E = LT/RT, Shift+W/S = RS). "A jump" is the animation of one carousel turn: title, info line and
the panel's content fading in and rising a little.

- [x] **T530-1** "Übersicht anpassen" (five ticked): ◀ ▶ move the focus to the other column at the same height,
  passing over greyed entries to the nearest one there; from the last "Verwalten" entry ◀ lands on the last
  "Controller" entry; a fresh ◀ on the left goes round to the right, a held ▶ on the right stops there; on "Für alle
  Spiele übernehmen" ◀ ▶ do nothing; in its question ◀ ▶ still choose "Abbrechen"/"Ersetzen". The footer shows the
  D-pad for "Wählen". · E2E: yes
- [x] **T530-2** Hold RT for a second on the detail page, then let go: the first turn jumps, the covers then turn without
  a jump, the release jumps once more to the game the covers reached; nothing jumps after that. · E2E: yes
- [x] **T530-3** Down the rail: "Übersicht", "Controller" and "Verwalten" show A's glyph (Space on the keyboard) inside
  their focus ring while they have it, "Infos" shows none and the footer offers no A there; on the buttons and in the
  rows no area shows the glyph. · E2E: yes
- [x] **T530-4** "Infos" in the rail with a long description: RS (Shift+W/S) scrolls description and fields together
  without A, the footer names RS; A does nothing, the focus stays in the rail; a short text ignores RS; a turn keeps
  "Infos" readable the same way. · E2E: yes
- [x] **T530-5** Mouse wheel: over the panel (quick selection, description, "Infos", an info box) it never switches the
  game, it scrolls the text under it where there is more; over the background (artwork, stacks) one notch turns one
  game as before (T506-5). · E2E: no (window: the wheel's target is the drawn layout)
- [ ] **T530-6** **Owner:** On your PC with the Steam Controller, hold RT (and LT) for about two seconds on the detail
  page and let go: the title jumps at the first turn and again when you let go, never in between. `arcadia.log` says
  "Detailseite: LT/RT losgelassen, Seite folgt mit Sprung" for each release. · E2E: no (real controller and real
  rendering: in the container and the tests both jumps already came before this change, and the owner's log of the
  test shows ordinary holds)
- [x] **T530-8** Detail page opened from a tab: R3 (G) opens the tab's filter over the page, the funnel chip beside the
  sort at the foot does the same with a click; the filter's choice is the tab's filter in the library as well; B
  closes it and the carousel turns through the hits only. · E2E: yes
- [x] **T530-9** The shown game drops out of the filter: the page shows the next hit after it in the order before (not
  the game at its old index), area and focus kept; a filter that keeps it keeps it shown; with no hit left the page
  closes to the library. · E2E: yes
- [ ] **T530-7** **Owner:** With the controller, the glyph inside the rail's focus ring reads as "press A to enter" and
  doesn't crowd the value line. · E2E: no (look and feel)
- [ ] **T530-10** **Owner:** A renamed game: ⧉ shows and hides "Original-Titel" (the footer says "[⧉] Original-Titel"),
  R3 opens the filter instead. · E2E: no (the button's new place is the owner's choice to confirm in use)

Earlier cases this changes: T493-2, T493-14 and T493-28 entered "Infos" with A and scrolled it with ▲ ▼; "Infos" is now
read from the rail with RS (T530-4). T496-11 and the other "Original-Titel" cases toggle with ⧉ now, not R3 (T530-10).
Their tests follow.

Automated tests:

| Test cases | Tests |
|---|---|
| T530-1 to -4, -8, -9 | `DetailControlsTests`; `CarouselTests` (Core) for the next hit; T493-2, -14, -28 in `DetailMetadataTests` follow the new reading of "Infos", the #496 tests in `ChangeTitleTests` and `ChangeTitleAcrossTheAppTests` press ⧉ |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T530-1 to -4 | 2026-11-13 | Claude | PR branch, Debug, Linux cloud container, deliberately broken builds | ✅ each red once except T530-2: ◀ ▶ ignored in the dialog (T530-1), glyph never shown (T530-3), "Infos" entered with A (T530-4), the filter refused over the detail page (T530-8), the old index kept instead of the next hit (T530-9). T530-2 was green on `main` too: the view model already jumped on release | owner's run on his PC (T530-6) |
| T530-2, -3, -4, -5, dialog | 2026-11-13 | Claude | PR branch, Debug, Linux cloud container (Xvfb), keys and mouse, `--demo` | ✅ held E: the release fades the title in; glyph in the ring of "Übersicht"/"Controller", none on "Infos", footer "⇧W ⇧S / Mausrad Infotext"; Shift+S scrolls "Infos" from the rail; wheel over the panel keeps the game, over the background turns it; dialog: D moves to "Verwalten", footer "WASD Wählen" | – |
| T530-9 (no hit left) | 2026-11-13 | QE (Claude) | PR branch, Release, Linux cloud container | ✅ new acceptance test red once with `Carousel.FindOrNext` returning 0 for an empty list (the page stayed open with nothing to show), green on the branch | – |
| T530-3, -4, -5, -8, -9 | 2026-11-13 | QE (Claude) | PR branch, Release, Linux cloud container (Xvfb), keys and mouse, `--demo` | ✅ G opens the filter over the page, the chip at the foot shows "13 von 31" after "Nur nicht installierte", the installed game gave way to the next hit with the focus kept in the rail; glyph in the ring of "Übersicht", none on "Infos"; Shift+S and the wheel over "Infos" scroll it without turning, the wheel over the background turns. Keyboard: the "Leertaste" glyph shortens the value line of "Übersicht" ("Vor 1 Woche · 84 St…") | owner's look T530-7 |
