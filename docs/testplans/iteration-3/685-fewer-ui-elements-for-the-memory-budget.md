# #685 Lower the number of UI elements built at the same time to meet the memory budget

**Status:** Automated: T685-1, T685-2 · QE: T685-3, T685-4 · Open for the owner: T685-5

- [x] **T685-1** A page of 128 covers builds only the covers in sight and a row around it (at most 7 rows); going down to the last cover builds those there and lets the top ones go; going back up builds the top again · E2E: no (window guard; counts built controls)
- [x] **T685-2** A cover builds its update, running, starting and install badges only when one shows; a cover with an update shows its badge · E2E: no (window guard)
- [ ] **T685-3** Library with 128 per page, search, detail page, a cover with an update, an install in progress and a running game look as on `main` (pixel comparison); scrolling a page of 128 with the controller down and up shows every cover with its image, no empty place · E2E: no (window)
- [ ] **T685-4** Release, `--demo --demo-games 5000` and 905 with 128 per page: private memory after the start and after a session (scroll the page down and up, next page, search, settings) under 400 MB; a focus move down a row stays under 50 ms (`Bild:` lines) · E2E: no (memory of a real process)
- [ ] **T685-5** **Owner:** real PC, Release, the real library at 128 per page: private memory after the start, after scrolling a page down and up, and on the detail page after 5 minutes without input; under 400 MB · E2E: no (real library, Windows memory) · RC

Automated tests:

| Test cases | Tests |
|---|---|
| T685-1, T685-2 | `Guards/CoversInSightTests`; T24-5 (`Guards/LibrarySpeedTests`) adjusted to the built covers |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| Managed heap, 905 demo games, 128 per page, 40 s after start | 2026-11-18 | implementation | `main` 1277ef1, Release, Linux cloud container (Xvfb) | 218 MB managed, anonymous RSS 418 MB; 128 covers built; same as the owner's dump (226 MB, 7,843 styled elements) | the 128 covers are the cause |
| Same | 2026-11-18 | implementation | PR branch (covers in sight), Release, Linux cloud container | 124 MB managed, anonymous RSS 292 MB; 40 covers built | – |
| Session (scroll 128 down and up, next page, search, settings, 40 s idle) | 2026-11-18 | implementation | `main` 1277ef1 vs PR branch (covers in sight and badges when shown), Release, Linux cloud container | anonymous RSS after start main 444 → PR 280 MB, peak while scrolling 583 → 435 MB (short-lived garbage, given back by the idle trim), after the session 472 → 343 MB; managed live 256 → 121 MB | – |
| Focus move, 5,000 demo games, 128 per page, 15 × down, 15 × up, 3 × next page (`Bild:` lines) | 2026-11-18 | implementation | `main` 1277ef1 vs PR branch, Release, Linux cloud container | first PR build (rows near sight built during layout): down up to 73 ms, up up to 115 ms ❌; after building near rows after the frame: down max 25 ms (main 22), up max 9 ms (main 12), next page 28–60 ms (main 38–69) | rows beside sight are built after the frame, one cover per job |
| Session as above, after the change | 2026-11-18 | implementation | PR branch, Release, Linux cloud container | anonymous RSS after start 277 MB, peak 408 MB, after 40 s idle 315 MB; managed live 121 MB | – |
| T685-3 (pixel comparison, 7 states: library, page scrolled down, down and up, next page, search, hits moved, game menu) | 2026-11-18 | implementation | PR branch vs `main` 1277ef1, Debug, `--demo --demo-games 905`, 128 per page, Xvfb | ✅ identical below the header, apart from up to 30 anti-aliased corner pixels of the focused cover's image (timing of its decode; also seen between two runs) | – |
| T685-1, T685-2, T24-5, all window guards and tests | 2026-11-18 | implementation | PR branch, Release and Debug, Linux | ✅ (T24-5 red on the first build as expected: it counted 128 built covers) | T24-5 counts the built covers |
