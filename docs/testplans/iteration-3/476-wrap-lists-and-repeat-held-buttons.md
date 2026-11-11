# #476 Wrap round at the ends of every list and repeat LT/RT and LB/RB while held

**Status:** Automated: T476-1 to T476-4, T476-6 to T476-12, T476-14 · QE: T476-8, T476-9 (app run) · Open for the owner: T476-15

Part 1 (hold): T476-8 to T476-13. Part 2 (search and library wrap, release threshold): T476-1 to -4, -6, -7, -14, -15. Part 3 (all other lists): T476-5, added with it.

- [x] **T476-1** Search grid on its last window, RT (Q, Page Down) shows the first window; LT (E, Page Up) on the first shows the last; the search row the same · E2E: yes
- [x] **T476-2** Focus on the first hit, left: the last hit; right on the last: the first; in the row with the keyboard out the same with the right stick; the library the same over all its pages · E2E: yes
- [x] **T476-3** Search grid, focus in the first row: up goes to the last row, same column, or to the last hit if that row is shorter; down in the last row to the first row; in the single row up/down do nothing · E2E: yes
- [x] **T476-4** Library: right on a page's last game goes to the next page's first, down in its bottom row to the next page's top row, same column; from the last page round to the first and back; LT/RT (Q/E, Page Up/Down, the buttons below the grid) turn round the same way · E2E: yes
- [x] **T476-6** The search grid's LT and RT buttons both show at every window while there are more hits than fit · E2E: yes
- [x] **T476-7** The mouse wheel over the search hits stops at the first and last window · E2E: yes

- [x] **T476-8** Held LT/RT and LB/RB turn after 400 ms, then every 150 ms, on every page where they turn (library, search, detail page, launcher pages in the settings, tabs); the other buttons don't repeat · E2E: yes
- [x] **T476-9** A held stick, D-pad, LT/RT, LB/RB or key stops at the end (tabs, detail page, search row, ◀ ▶ selectors); only a fresh press goes round; a held ◀ ▶ doesn't flip a switch back and forth · E2E: yes
- [x] **T476-10** Hold RT (or the D-pad), then the page or layout changes (B closes the detail page, a dialog opens): the hold does nothing more, the new page acts only on a fresh press · E2E: yes
- [x] **T476-11** Hold RT while a rescan changes the number of games: it goes on with the new number, kept in range · E2E: yes
- [x] **T476-12** Pan hides or shows a controller while RT is held: released as before (#367), no repeat afterwards · E2E: yes
- [x] **T476-13** **Owner:** On the real PC with the Steam Controller and an Xbox pad, hold RT in the library, the detail page and the search, and RB on the tabs: the speed feels right, it stops at the end, a fresh press goes on round; Q/E held do the same · E2E: no (feel of the real controllers and Windows' key repeat)
- [x] **T476-14** A held RT (or stick) that dips a little below half way and back while held is no fresh press: at the end of the search grid or the library it stays there instead of going round; only below a quarter (stick 0.3) it lets go · E2E: yes
- [ ] **T476-15** **Owner:** On the real PC with the Steam Controller and an Xbox pad: in the library and the search, D-pad/stick and LT/RT go round at the ends and across the library's pages as described; RT held half way at the last page or window, pulled a little less and more again, doesn't jump to the first · E2E: no (feel of the real triggers and sticks)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T476-8 to T476-12 | 2026-11-11 | Claude | PR (part 1), Linux container, unit and acceptance tests (`GamepadPollsTests`, `ListFocusTests`, `ValueCycleTests`, `HitWindowTests`, `HeldButtonsTests`, `HeldKeysTests`); each acceptance test red once with the feature broken | ✅ | – |
| T476-8, -9, -10, -12 | 2026-11-11 | QE | PR #479, Linux container: acceptance tests for the library, the search row, the launcher pages, a launcher's switch, the keyboard sliding in and Pan letting go; each red once | ✅ | – |
| T476-10 | 2026-11-11 | QE | PR #479, code review | ❌ a dialog of a settings category (prompt, "Controller zuordnen", question) doesn't stop a hold | fixed in this PR (Review, finding 1) |
| T476-10 | 2026-11-11 | Claude | PR #479, Linux container, `EveryControllerForGamesTests.A_held_D_pad_doesnt_reach_a_settings_dialog_opened_meanwhile` (red before the fix) | ✅ | – |
| T476-8, -9 | 2026-11-11 | QE | PR #479, real app under Xvfb, `--demo`, held E, Shift+E, Page Down (no gamepad) | ✅ library stops on the last page, tabs on the last tab, 34 to 84 repeats each | – |
| T476-13 | 2026-11-11 | owner | PR #479 | ✅ ticked by the owner for the merge | – |
| T476-1 to -4, -6, -7, -9, -14 | 2026-11-11 | Claude | PR (part 2), Linux container, unit and acceptance tests (`GridFocusTests`, `HitWindowTests`, `GamepadPollsTests`, `SearchWrapTests`, `LibraryTests`, `SearchViewTests`); each new or changed acceptance test red once without the change, `A_held_RT_wobbling_round_half_way_stays_at_the_last_window` red with only the release threshold taken out | ✅ | – |
