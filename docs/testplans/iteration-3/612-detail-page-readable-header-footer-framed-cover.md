# #612 Detail page: readable header and footer, background to the window's edges, framed cover

**Status:** Automated: T612-3 (window area) · QE: T612-1 to T612-6 · Open for the owner: T612-7

- [x] **T612-1** Detail page open over a bright artwork: header (logo, tabs, clock) sits on a dark see-through band, the
  artwork visibly shows through, all text reads clearly. · E2E: no (window)
- [x] **T612-2** The same for the footer (position, sort, button hints). · E2E: no (window)
- [x] **T612-3** Window wider than 16 : 9 (e.g. 2547 × 1360) and narrower (e.g. 1200 × 1000): the artwork reaches every
  edge of the window, no black bars, no hard edge; header and footer bands reach the window's sides. · E2E: no (window)
- [x] **T612-4** Titles of the games before and after sit on a soft cloud like the note under "Spielen" and read clearly
  on a bright image. · E2E: no (window)
- [x] **T612-5** The front cover and the stack covers have a subtle light frame that sets them off from the artwork; the
  preview in "Artwork ändern" shows the bands and the frame too. · E2E: no (window)
- [x] **T612-6** Library page: header and footer look as before (no band). · E2E: no (window)
- [ ] **T612-7** **Owner:** On your monitor with your own games, open the detail page of a game with a bright and one with
  a dark artwork: header and footer read well, the artwork shows through their bands, nothing looks cut off at the
  window's edges, the cover stands out. · E2E: no (real artwork on a real monitor)

Automated tests:

| Test cases | Tests |
|---|---|
| T612-3 | `WindowAreaTests` (the area the background fills, for a wide, a tall and a 16 : 9 window) |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T612-1, T612-2, T612-4, T612-5 | 2026-11-14 | Claude | PR branch, Linux Xvfb, `--demo --demo-games 40` with bright made-up backgrounds, 1920 × 1080 | ✅ (the clock's date in soft grey is the faintest text over a very bright image) | – |
| T612-3 | 2026-11-14 | Claude | as above, windows 2547 × 1360 and 1200 × 1000 | ✅ | – |
| T612-5 | 2026-11-14 | Claude | as above, "Artwork ändern" preview | ✅ | – |
| T612-6 | 2026-11-14 | Claude | as above, library at 2547 × 1360 | ✅ | – |
