# #546 Softer, centred hints on the detail page and a separate "Fertig" group at the first start

Findings of the owner's hand test of #43 after #541 and #497, changes 1 to 4 of the issue. The cases were written by the
implementing thread from the issue's changes, as the issue had none; the quality engineer checks them in the review.

**Status:** Automated: T546-4 · QE: T546-1, T546-2, T546-3, T546-4 · Open for the owner: T546-5

Every case runs with the gamepad alone unless it says otherwise; keys follow `docs/product/controls.md`.

- [ ] **T546-1** A game that isn't installed and one with an update, over a bright artwork: the dark cloud behind "Die
  Installation wird in … geöffnet" and "Update verfügbar …" is about half as dark as before (the image shows through), still
  without a hard border, and the text stays readable. · E2E: no (window: drawn)
- [ ] **T546-2** The same two games: the cloud reaches out by the same amount left and right of the note, so the text sits
  in its middle; the note's left edge stays aligned with "Spielen"/"Installieren". · E2E: no (window: layout)
- [ ] **T546-3** The same two games, and one with a long title that wraps: the note lies in the middle between the lower
  edge of the button row and the upper edge of the panel; the button row and the panel don't move. · E2E: no (window: layout)
- [x] **T546-4** First start, step "Datenquellen": "Überspringen" (or "Fertig" once a key or access is there) is in its
  own group "ABSCHLIESSEN" below the IGDB rows, with the group's heading and spacing; ≡, Enter and the row still end the
  first start, B still goes back. · E2E: yes (the group is automated; its look is checked in the window)
- [ ] **T546-5** **Owner:** On the TV, two or three of your own games with bright artworks, one not installed and one with
  an update: the note under the button reads at a glance, the cloud looks calmer than before and the text sits in its
  middle. · E2E: no (look and feel)

Automated tests:

| Test cases | Tests |
|---|---|
| T546-4 | `FirstRunDataSourcesTests` |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T546-4 | 2026-11-13 | Claude | branch, Linux, `dotnet test` (red once with the group removed) | ✅ | – |
| T546-1 to T546-4 | 2026-11-13 | Claude | branch, Debug, Linux Xvfb 1920 × 1080, demo data ("Burning Kingdom" not installed, "Frozen Outpost" with update); for T546-2/3 the cloud was tinted red for one run to see its extent; first start with `--first-run` | ✅ cloud symmetric around both notes, note in the middle between buttons and panel, "ABSCHLIESSEN" group with heading and gap; T546-1 only on the demo's dark artwork, the long wrapping title not looked at | – |
