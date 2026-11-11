# #477 Opening the search from the library takes half a second or more

**Status:** Automated: T477-1, T477-2 · QE: T477-3 · Open for the owner: T477-4

- [x] **T477-1** Library with 1,000 games, X opens the search, a term is typed, B twice closes it, X opens it again and a new term is typed: the on-screen keyboard's keys and the hits are the same controls as in the first search, bound to the new hits · E2E: yes
- [x] **T477-2** A search opened again starts as a new one: nothing typed, the keyboard out (controller) with the focus on its first key and shift off, its keys type into the new search · E2E: yes
- [x] **T477-3** Release build with `--demo --demo-games 1000`, F and Esc seven times: every opening after the first logs `Bild: Search` under 50 ms · E2E: no (real rendering and the log's frame times; the window test checks the kept controls instead)
- [ ] **T477-4** **Owner:** own library (700+ games), controller: X from the library several times, also after a game was started from the search: the search appears without a visible pause; `arcadia.log` has `Bild: Search nach` under 50 ms from the second opening on · E2E: no (owner's PC and library, real frame times)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T477-3 (before the fix) | 2026-11-11 | Claude | `main` 0cb2b7c, Release, Xvfb, Linux cloud container, 1,031 demo games | ❌ `Bild: Search` 170–560 ms, of it 6–16 ms handling; profile: building the keyboard's key controls (and the hits' places) for every new search | fixed |
| T477-1 (before the fix) | 2026-11-11 | Claude | PR branch without the fix, Release, Linux cloud container | ❌ the keys are other controls after reopening | fixed |
| T477-1, T477-2 | 2026-11-11 | Claude | PR branch, Release, Linux cloud container | ✅ | – |
| T477-3 | 2026-11-11 | Claude | PR branch, Release, Xvfb, Linux cloud container, 1,031 demo games | ✅ `Bild: Search` 12–31 ms from the second opening on; the first after the start ~980 ms (first build of the controls and JIT) | first opening: see PR |
| T477-3 (QE review) | 2026-11-11 | Claude (QE) | PR branch 57fd0fb, Release, Xvfb, Linux cloud container, 1,031 demo games | ✅ `Bild: Search` 18–27 ms on the 2nd to 8th opening, the first 541 ms; `Back` 5–14 ms; a reopened search with another term shows only its own hits | – |
