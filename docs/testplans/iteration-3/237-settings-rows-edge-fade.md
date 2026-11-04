# #237 Fade out the settings rows where the list can still scroll, with one shared edge fade

All cases are rendering, so none is an acceptance test through the view models; the window tests named below
(`Arcadia.App.Tests`, Avalonia headless) cover what can be checked drawn, the quality engineer checks the rest with
screenshots.

- [ ] **T237-1** Category whose rows fit: no fade. · E2E: no (rendering; window test `SettingsRowsFadeTests`)
- [ ] **T237-2** Controller category: bottom fade on open; ▼ to the end and ▲ back: fades per scenarios, focused row
  never under a fade, group heading visible. · E2E: no (rendering; window test `SettingsRowsFadeTests`)
- [ ] **T237-3** Wheel to the middle: both fades; mouse over a row under a fade: focused, no jump, click works.
  · E2E: no (rendering; window test `SettingsRowsFadeTests`)
- [ ] **T237-4** Category switch, rows changing, window resize: fades follow. · E2E: no (rendering; window test
  `SettingsRowsFadeTests` for switch and resize; rows changing by screenshot)
- [ ] **T237-5** 1280×720 and 1920×1080: no unfaded line at the edges (settings rows, grid, search, detail).
  · E2E: no (rendering; window tests `SettingsRowsFadeTests`, `GridFadeTests` T102-4, `SearchViewTests`)
- [ ] **T237-6** First run with many launchers: same as T237-2. · E2E: no (rendering; window test `FirstRunViewTests`
  with less room than rows, as the built-in launchers fit)
- [ ] **T237-7** Regression: library grid, tab strips, info panel, "Controller zuordnen", search hits, detail
  neighbours look and behave as before; focus-move latency in the library measured. · E2E: no (rendering; existing
  window tests T102-*, T127-*, T15-*, T238-13, search and detail tests)
- [ ] **T237-8** (owner) Controller category on the owner's PC scrolled with the Steam Controller. · E2E: no (real
  Steam Controller)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T237-1 to T237-6 window tests | 2026-11-04 | Claude | PR branch, Debug, Linux cloud container, Avalonia headless | ✅ 8 passed | – |
| Red runs: fade ignores the scroll position / fade without the scroller's padding / fade not click-through | 2026-11-04 | Claude | same | ❌ T237-1, -2, -4, -6 / T237-5 (all sizes) / T237-3 red | Reverted, tests kept |
| T237-7 existing window tests (grid, header, settings, "Controller zuordnen", first run, search, detail) | 2026-11-04 | Claude | same | ✅ | – |
| T237-2, T237-7 looked at | 2026-11-04 | Claude | PR branch, Debug, `--demo`, Xvfb 1920×1080 | ✅ Controller rows at top and end, library grid, tab strip, detail neighbours | – |
| T237-7 latency | 2026-11-04 | Claude | PR branch, Release, `--demo` (31 games), Xvfb, keys W/S/D | ✅ 32 moves: median 1.9 ms, p95 9.4 ms, max 33.7 ms (`Bild:` in `arcadia.log`) | Owner's PC not measured |
| All tests (`tools/ci/run-tests.sh 4`) | 2026-11-04 | Claude | PR branch, Release, Linux cloud container | ✅ 3,272 passed, 30 skipped | – |
| T237-4 rows going and coming (new window test `Rows_that_go_or_come_…`) | 2026-11-04 | QE (Claude) | PR branch, Debug, Linux cloud container, Avalonia headless | ✅; red with a fade that follows only scroll offsets | – |
| T237-7 search opened with F, keyboard in (new window test `Opened_without_the_keyboard_…`) | 2026-11-04 | QE (Claude) | same | ❌ grid of hits not faded above and below until the keyboard went out once | Fix in this PR |
| T237-7 search, settings rows "Controller", info panel looked at | 2026-11-04 | QE (Claude) | PR branch, Release, `--demo`, Xvfb 1920×1080 | ❌ search with F and "e": third row of hits cut hard at the bottom; ✅ rows and info panel fade at the bottom | Fix in this PR |
| All tests (`tools/ci/run-tests.sh 4`) | 2026-11-04 | QE (Claude) | PR branch + QE tests, Release, Linux cloud container | ❌ only the new search test, rest ✅ | Fix in this PR |
| T237-7 search opened with F (fix: fades set up when shown) | 2026-11-04 | Claude | PR branch, Debug, Linux cloud container, Avalonia headless | ✅ `Opened_without_the_keyboard_…` and all search tests | – |
| T237-4 LB/RB starts a category at its first row (owner's decision) | 2026-11-04 | Claude | same | ✅ `SettingsRowsFadeTests`; T137-8 test adapted (back on "Launcher" at the first launcher) | – |
| All tests (`tools/ci/run-tests.sh 4`) | 2026-11-04 | Claude | PR branch, Release, Linux cloud container | ✅ 3,274 passed, 30 skipped | – |
| T237-4 and T137-8: LB/RB back to "Launcher" from a launcher page keeps that launcher (owner's decision), other categories start at their first row | 2026-11-04 | Claude | PR branch, Release, Linux cloud container | ✅ all tests (`run-tests.sh 4`) | – |
