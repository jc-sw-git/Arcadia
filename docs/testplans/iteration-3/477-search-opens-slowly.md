# #477 Opening the search from the library takes half a second or more

**Status:** Automated: T477-1, T477-2, T477-5, T477-6 · QE: T477-3, T477-7 · Open for the owner: T477-4, T477-8

- [x] **T477-1** Library with 1,000 games, X opens the search, a term is typed, B twice closes it, X opens it again and a new term is typed: the on-screen keyboard's keys and the hits are the same controls as in the first search, bound to the new hits · E2E: yes
- [x] **T477-2** A search opened again starts as a new one: nothing typed, the keyboard out (controller) with the focus on its first key and shift off, its keys type into the new search · E2E: yes
- [x] **T477-3** Release build with `--demo --demo-games 1000`, F and Esc seven times: every opening after the first logs `Bild: Search` under 50 ms · E2E: no (real rendering and the log's frame times; the window test checks the kept controls instead)
- [ ] **T477-4** **Owner:** own library (700+ games), controller: start Arcadia, wait a few seconds, then X from the library several times, also after a game was started from the search: the search appears without a visible pause, the first time too; `arcadia.log` has `Bild: Search nach` under 50 ms from the second opening on · E2E: no (owner's PC and library, real frame times)
- [x] **T477-5** Once the library shows, the search's keyboard is built before the first X: its keys exist while the search is closed, and the first search shows those same controls · E2E: yes
- [x] **T477-6** A text of the settings (launcher name) written, dropped with B and written again: the keyboard's keys are the same controls, the focus on the first key, shift off, the text the launcher's name again, and the keys type into the new text · E2E: yes
- [x] **T477-7** Release build with `--demo --demo-games 1000`, a few seconds after the start F: `arcadia.log` has both lines `Tastatur … vorbereitet` after the line `Bibliothek:`, and the first `Bild: Search` is under 150 ms · E2E: no (real rendering and start-up timing)
- [ ] **T477-8** **Owner:** controller, right after the start: the library shows as fast as before; a few seconds later the settings, a launcher, "Name": the keyboard comes without a visible pause, also the second time · E2E: no (owner's PC, real timing)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T477-3 (before the fix) | 2026-11-11 | Claude | `main` 0cb2b7c, Release, Xvfb, Linux cloud container, 1,031 demo games | ❌ `Bild: Search` 170–560 ms, of it 6–16 ms handling; profile: building the keyboard's key controls (and the hits' places) for every new search | fixed |
| T477-1 (before the fix) | 2026-11-11 | Claude | PR branch without the fix, Release, Linux cloud container | ❌ the keys are other controls after reopening | fixed |
| T477-1, T477-2 | 2026-11-11 | Claude | PR branch, Release, Linux cloud container | ✅ | – |
| T477-3 | 2026-11-11 | Claude | PR branch, Release, Xvfb, Linux cloud container, 1,031 demo games | ✅ `Bild: Search` 12–31 ms from the second opening on; the first after the start ~980 ms (first build of the controls and JIT) | first opening: see PR |
| T477-3 (QE review) | 2026-11-11 | Claude (QE) | PR branch 57fd0fb, Release, Xvfb, Linux cloud container, 1,031 demo games | ✅ `Bild: Search` 18–27 ms on the 2nd to 8th opening, the first 541 ms; `Back` 5–14 ms; a reopened search with another term shows only its own hits | – |
| T477-7 | 2026-11-11 | Claude | PR branch, Release, Xvfb, Linux cloud container, 1,031 demo games | ✅ library 1.8–2.1 s after the start as before; keyboards built after it in 784–824 ms (search) and 669–684 ms (settings), each its own job; first `Bild: Search` 78 ms, then 24 ms; F pressed while they are built waits (814 ms) | – |
| T477-5 (before the fix) | 2026-11-11 | Claude | PR branch without building the keyboards after the library, Release, Linux cloud container | ❌ no keys while the search is closed | fixed |
| T477-6 (before the fix) | 2026-11-11 | Claude | PR branch with the settings' keyboard bound to the editor again, Release, Linux cloud container | ❌ the keys are other controls when writing again | fixed |
| T477-5, T477-6 | 2026-11-11 | Claude | PR branch, Release, Linux cloud container | ✅ | – |
