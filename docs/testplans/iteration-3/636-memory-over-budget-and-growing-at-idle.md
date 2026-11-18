# #636 Arcadia's memory is over budget with 905 games and keeps growing at idle

**Status:** Automated: T636-1, T636-2, T636-3, T636-4, T636-7 · QE: T636-5 · Open for the owner: T636-6

- [x] **T636-1** The library with the three keyboards built: Avalonia's style records per element stay at most 50 (96 before) · E2E: no (window guard; counts Avalonia's internal records)
- [x] **T636-2** An on-screen key builds only the face of its own kind: at most 13 elements per key (35 before) · E2E: no (window guard)
- [x] **T636-3** A loading spinner turns while it is seen, stops while a panel around it is hidden or it left the window, and turns again when shown · E2E: no (control, no page flow)
- [x] **T636-4** After 64 MB of work and 10 s without a press, one full collection gives the memory back; a press puts it off; idle alone never triggers it; the next counts from the last · E2E: no (timer logic)
- [x] **T636-7** No trim while a game runs or Arcadia isn't in front (the pause would stall the pads' forwarding); a trim put off follows once Arcadia is back in front · E2E: no (timer logic)
- [ ] **T636-5** Every page, dialog and settings category looks as before (pixel comparison against `main`, the clock aside): library, detail page and its sections, game menu, view, filter, search, settings categories and tabs, change artwork, tags, change title with the keyboard · E2E: no (window)
- [ ] **T636-6** **Owner:** real PC, Release, the real library: private working set right after the artwork and metadata run, and after 5 minutes without input; it must be under 400 MB and flat (`Speicher: aufgeräumt …` lines in `arcadia.log` show each trim) · E2E: no (real library, Windows memory) · RC

Automated tests:

| Test cases | Tests |
|---|---|
| T636-1, T636-2 | `Guards/StyleCostTests` |
| T636-3 | `LoadingSpinnerTests` |
| T636-4, T636-7 | `MemoryTrimTests` |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| Memory, `--demo --demo-games 5000`, 30 s after start then 5 min idle | 2026-11-18 | implementation | `main` 673cc26, Release, Linux cloud container (Xvfb) | anonymous RSS 972 MB, working set 1,153 → 1,159 MB; managed heap 489 MB in gen2, 213 MB LOH garbage | baseline |
| Same | 2026-11-18 | implementation | PR branch, Release, Linux cloud container | anonymous RSS 251 MB, working set 411 → 420 MB; one trim at 30 s (213 ms, managed 245 → 134 MB); idle allocation 0.8 MB/min (3.7 before) | – |
| T636-1, T636-2 | 2026-11-18 | implementation | `main` 673cc26, Linux | ❌ 96 records per element, 35.2 elements per key | the check goes red on the bug |
| T636-1 … T636-4 | 2026-11-18 | implementation | PR branch, Debug, Linux | ✅ | – |
| T636-5 (≈35 states) | 2026-11-18 | implementation | PR branch vs `main`, Release, demo data, Xvfb | ✅ pixel-identical apart from the header clock (a minute's width shifts the header) | – |
| Memory after the QE fix (trim gated), 75 s idle | 2026-11-18 | implementation | PR branch + `main` merged, Release, Linux cloud container (Xvfb, window focused by `xdotool`) | anonymous RSS 262 MB; trim at 30 s (281 ms, managed 264 → 135 MB). Without focus no trim, as gated | – |
| T636-4, T636-7, T636-1 … T636-3 | 2026-11-18 | implementation | PR branch + `main` merged, Debug, Linux | ✅ | – |
