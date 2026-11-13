# #530 Detail page controls: focus glyphs, scrolling the info box, switching games and the overview dialog

Findings 1 to 5 of the owner's hand test of #43 (detail page from #498/#499/#500). Change 6 (filtering from the detail
page) is open in the issue and not covered here yet. The cases were written by the implementing thread from the issue's
changes, as the issue had none; the quality engineer checks them in the review.

**Status:** Automated: T530-1 to -4 · QE: T530-5 · Open for the owner: T530-6, T530-7

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
  page and let go: the title jumps at the first turn and again when you let go, never in between. · E2E: no (real
  controller and real rendering: in the container and the tests both jumps already came before this change)
- [ ] **T530-7** **Owner:** With the controller, the glyph inside the rail's focus ring reads as "press A to enter" and
  doesn't crowd the value line. · E2E: no (look and feel)

Earlier cases this changes: T493-2, T493-14 and T493-28 entered "Infos" with A and scrolled it with ▲ ▼; "Infos" is now
read from the rail with RS (T530-4), and their tests follow.

Automated tests:

| Test cases | Tests |
|---|---|
| T530-1 to -4 | `DetailControlsTests`; T493-2, -14, -28 in `DetailMetadataTests` follow the new reading of "Infos" |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T530-1 to -4 | 2026-11-13 | Claude | PR branch, Debug, Linux cloud container, deliberately broken builds | ✅ each red once except T530-2: ◀ ▶ ignored in the dialog (T530-1), glyph never shown (T530-3), "Infos" entered with A (T530-4). T530-2 was green on `main` too: the view model already jumped on release | owner's run on his PC (T530-6) |
| T530-2, -3, -4, -5, dialog | 2026-11-13 | Claude | PR branch, Debug, Linux cloud container (Xvfb), keys and mouse, `--demo` | ✅ held E: the release fades the title in; glyph in the ring of "Übersicht"/"Controller", none on "Infos", footer "⇧W ⇧S / Mausrad Infotext"; Shift+S scrolls "Infos" from the rail; wheel over the panel keeps the game, over the background turns it; dialog: D moves to "Verwalten", footer "WASD Wählen" | – |
