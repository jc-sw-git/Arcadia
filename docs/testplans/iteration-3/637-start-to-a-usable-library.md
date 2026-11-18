# #637 Start to a usable library takes 3.2 s with 905 games

**Status:** Automated: T637-1, T637-2, T637-3, T637-4 · Implementation and QE: T637-5 · Open for the owner: T637-6, T637-7

- [x] **T637-1** Reading the libraries never waits for the thread that started it (at start the UI thread builds the window for a second or more) · E2E: no (Core, threading)
- [x] **T637-2** The reading started before the window is built is the one the library shows: one scan, the games are there, a later load reads again · E2E: no (session logic, no page flow)
- [x] **T637-3** A button hint on a hidden page is built only when the page shows; a hint hidden for having no key for the input in use comes back when the input changes; a shown hint follows a changed text · E2E: no (control)
- [x] **T637-4** The log line `Start: …` lists the steps of the start in the order they happened, in ms since the process started · E2E: no (log format)
- [x] **T637-5** Library (hints, badges), game menu, "Titel ändern" with its keyboard sliding in, search keyboard: as before; the keyboards are built right after the first frame (`Tastatur … vorbereitet` after `Start: …`) · E2E: no (window)
- [ ] **T637-6** **Owner:** real PC, the installed release candidate (ReadyToRun), the real library (~900 games, Steam signed in), cold start: `arcadia.log` line `Start: …` ends with `Bibliothek im Bild` under 2,000 ms; do it twice · E2E: no (real library, Windows) · RC
- [ ] **T637-7** **Owner:** same PC and build, page size 128: 20 page turns forward and back, a few tab switches (LB/RB) and focus moves; the `Bild:` lines stay under 50 ms in 19 of 20 (p95) · E2E: no (real library, frame times on the owner's PC) · RC

Automated tests:

| Test cases | Tests |
|---|---|
| T637-1 | `LibraryServiceTests.A_load_never_waits_for_the_thread_that_started_it` |
| T637-2 | `LibrarySessionTests.The_reading_started_before_the_window_is_the_one_the_load_shows` |
| T637-3, T637-4 | `HintsBuiltWhenShownTests` |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| Owner's log of v0.3.0-rc.1 (905 games) read | 2026-11-18 | implementation | v0.3.0-rc.1, Windows 11, owner's PC | process start → end of the window setup 1.6 s; the scan's first file read 1.1 s later; library at 3.2 s | the scan waited for the UI thread |
| Start, `--demo --demo-games 1000`, 3 runs each, `Bibliothek im Bild` | 2026-11-18 | implementation | `main` dea0e25 vs PR branch, published self-contained, Linux cloud container (Xvfb, software rendering) | main 3.1–3.5 s (`Bibliothek:` line); PR without ReadyToRun 3.4–3.6 s, with ReadyToRun 1.76–1.85 s; 5,000 games 2.0 s | – |
| Profile of the start (`dotnet-trace`) | 2026-11-18 | implementation | PR branch, Release, Linux | before: hints rebuilt at each property 1.3 s, keyboards before the first frame 0.5 s, "Titel ändern"'s keyboard in the first layout 0.6 s, JIT about half of the rest | each fixed as named in the PR |
| T637-1 | 2026-11-18 | implementation | `main`'s `LibraryService`, Release, Linux | ❌ times out after 10 s | the check goes red on the bug |
| T637-3 | 2026-11-18 | implementation | `main`'s `KeyHint`/`PadGlyph`, Release, Linux | ❌ the hidden page's hint is built at once | the check goes red on the bug |
| T637-1 … T637-4 | 2026-11-18 | implementation | PR branch, Release, Linux | ✅ | – |
| Page turns, page size 128, 48 turns | 2026-11-18 | implementation | PR branch, Release, Linux cloud container (Xvfb, software rendering) | median 35–45 ms, p90 45–75 ms, spread too large between runs to compare | T637-7 needs the owner's PC |
| T637-5 | 2026-11-18 | implementation | PR branch, Debug, demo data, Xvfb | ✅ library, game menu, "Change title" with the keyboard (screenshots); keyboards built after `Start: …` | – |
