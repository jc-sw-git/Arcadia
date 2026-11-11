# #476 Wrap round at the ends of every list and repeat LT/RT and LB/RB while held

**Status:** Automated: T476-1 to T476-12, T476-14, T476-16, T476-17 · QE: T476-8, T476-9 (PR #479), T476-2 to T476-4, T476-6 (PR #481, app run), T476-5 (PR #486, app run) · Open for the owner: –

Part 1 (hold): T476-8 to T476-13. Part 2 (search and library wrap, release threshold): T476-1 to -4, -6, -7, -14, -15. Part 3 (all other lists): T476-5. T476-1 replaces T12-16's "the grid stops at its ends" (only the wheel stops there now); T476-5 replaces T12-2's "the focus stays at the keyboard's edges".

- [x] **T476-1** Search grid on its last window, RT (E, Page Down) shows the first window; LT (Q, Page Up) on the first shows the last; the search row the same · E2E: yes
- [x] **T476-2** Focus on the first hit, left: the last hit; right on the last: the first; in the row with the keyboard out the same with the right stick; the library the same over all its pages · E2E: yes
- [x] **T476-3** Search grid, focus in the first row: up goes to the last row, same column, or to the last hit if that row is shorter; down in the last row to the first row; in the single row up/down do nothing · E2E: yes
- [x] **T476-4** Library: right on a page's last game goes to the next page's first, down in its bottom row to the next page's top row, same column; from the last page round to the first and back; LT/RT (Q/E, Page Up/Down, the buttons below the grid) turn round the same way · E2E: yes
- [x] **T476-5** Every other list, menu, dialog's buttons and the on-screen keyboard go round: a step past the end goes to the other end (settings rows, first start, context menu, keyboard layout menu, "Ansicht", filter categories and values, status messages and their two buttons, the detail page's buttons and rows, questions, the empty tab's two buttons, the prompts in "Controller", "Controller zuordnen", the firmware dialog); greyed-out items are passed over; where the edge leads elsewhere it still does (filter: left out of the values); a held D-pad stops at the end · E2E: yes
- [x] **T476-6** The search grid's LT and RT buttons both show at every window while there are more hits than fit · E2E: yes
- [x] **T476-7** The mouse wheel over the search hits stops at the first and last window · E2E: yes

- [x] **T476-8** Held LT/RT and LB/RB turn after 400 ms, then every 150 ms, on every page where they turn (library, search, detail page, launcher pages in the settings, tabs); the other buttons don't repeat · E2E: yes
- [x] **T476-9** A held stick, D-pad, LT/RT, LB/RB or key stops at the end (tabs, ~~detail page~~ (replaced by T476-16), search row, ◀ ▶ selectors); only a fresh press goes round; a held ◀ ▶ doesn't flip a switch back and forth · E2E: yes
- [x] **T476-10** Hold RT (or the D-pad), then the page or layout changes (B closes the detail page, a dialog opens): the hold does nothing more, the new page acts only on a fresh press · E2E: yes
- [x] **T476-11** Hold RT while a rescan changes the number of games: it goes on with the new number, kept in range · E2E: yes
- [x] **T476-12** Pan hides or shows a controller while RT is held: released as before (#367), no repeat afterwards · E2E: yes
- [x] **T476-13** **Owner:** On the real PC with the Steam Controller and an Xbox pad, hold RT in the library, the detail page and the search, and RB on the tabs: the speed feels right, it stops at the end, a fresh press goes on round; Q/E held do the same · E2E: no (feel of the real controllers and Windows' key repeat)
- [x] **T476-14** A held RT (or stick) that dips a little below half way and back while held is no fresh press: at the end of the search grid or the library it stays there instead of going round; only below a quarter (stick 0.3) it lets go · E2E: yes
- [x] **T476-16** Detail page: held RT (LT) turns on past the last (first) game round to the first (last) without a stop · E2E: yes
- [x] **T476-17** Library: a held stick or D-pad stops at the page's edge (also at the last game of a page); a fresh press crosses onto the next page, from the last page round to the first · E2E: yes
- [x] **T476-15** **Owner:** On the real PC with the Steam Controller and an Xbox pad: in the library and the search, D-pad/stick and LT/RT go round at the ends and across the library's pages as described; RT held half way at the last page or window, pulled a little less and more again, doesn't jump to the first · E2E: no (feel of the real triggers and sticks)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T476-8 to T476-12 | 2026-11-11 | Claude | PR (part 1), Linux container, unit and acceptance tests (`GamepadPollsTests`, `ListFocusTests`, `ValueCycleTests`, `HitWindowTests`, `HeldButtonsTests`, `HeldKeysTests`); each acceptance test red once with the feature broken | ✅ | – |
| T476-8, -9, -10, -12 | 2026-11-11 | QE | PR #479, Linux container: acceptance tests for the library, the search row, the launcher pages, a launcher's switch, the keyboard sliding in and Pan letting go; each red once | ✅ | – |
| T476-10 | 2026-11-11 | QE | PR #479, code review | ❌ a dialog of a settings category (prompt, "Controller zuordnen", question) doesn't stop a hold | fixed in this PR (Review, finding 1) |
| T476-10 | 2026-11-11 | Claude | PR #479, Linux container, `EveryControllerForGamesTests.A_held_D_pad_doesnt_reach_a_settings_dialog_opened_meanwhile` (red before the fix) | ✅ | – |
| T476-8, -9 | 2026-11-11 | QE | PR #479, real app under Xvfb, `--demo`, held E, Shift+E, Page Down (no gamepad) | ✅ library stops on the last page, tabs on the last tab, 34 to 84 repeats each | – |
| T476-13 | 2026-11-11 | owner | PR #479 | ✅ ticked by the owner for the merge | – |
| T476-1 to -4, -6, -7, -9, -14 | 2026-11-11 | Claude | PR #481, Linux container, unit and acceptance tests (`GridFocusTests`, `HitWindowTests`, `GamepadPollsTests`, `SearchWrapTests`, `LibraryTests`, `SearchViewTests`); each new or changed acceptance test red once without the change, `A_held_RT_wobbling_round_half_way_stays_at_the_last_window` red with only the release threshold taken out | ✅ | – |
| T476-2 to -4, -6 | 2026-11-11 | QE | PR #481, real app under Xvfb, `--demo`, keys only (no gamepad); `LibraryTests.The_page_buttons_below_the_grid_turn_round_at_the_ends` red with `PreviousPage` back on `GoToPage(_requestedPage - 1)` | ✅ library: down from the bottom row onto page 2, up from page 1's top row onto page 2's row, Page Down on page 2 back to 1; search "e" (112 hits): both turn buttons on the first window, left on the first hit to the last (99–112), up and down within the last rows, right on the last hit back to 1–14; no line under the first row's last hit | – |
| T476-16, T476-17 | 2026-11-11 | Claude | PR #481, Linux container, `HeldButtonsTests.Held_RT_turns_the_detail_page_round_past_the_last_game_without_stopping`, `LibraryTests.The_D_pad_held_repeats_the_move_and_stops_at_the_pages_edge_until_a_fresh_press`, each red without the change | ✅ | – |
| T476-15 | 2026-11-11 | owner | PR #481 | ✅ ticked by the owner for the merge | – |
| T476-5 | 2026-11-11 | Claude | PR (part 3), Linux container, `ListWrapTests` (each red with `ListFocus.Cycle` clamping again), `KeyGridTests`, `FullKeyboardTests`, `ListFocusTests`, changed `OnScreenKeyboardTests`, `LibraryFilterTests`; real app under Xvfb, `--demo`, keys only: settings ↑ on the first row to "Alle neu einlesen", context menu ↓ ↑ ↑ to "Deinstallieren …", rings whole | ✅ | – |
| T476-5 | 2026-11-11 | QE | PR #486, Linux container: acceptance tests for the filter's values (list and type grid, left out of the first column back to the categories), first start, the empty tab, the firmware dialog, "Controller zuordnen", the Windows restart question and the context menu's ring over its greyed-out entry, each red with `ListFocus.Cycle` clamping again (the filter with its view model back on `main`); real app under Xvfb, `--demo`, keys only: "Ansicht" ↑ on "Name A → Z" to "24", filter values ↑ on "Alle" to "Nur nicht installierte", rings whole | ✅ | – |
