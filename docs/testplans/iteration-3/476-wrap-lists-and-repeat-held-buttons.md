# #476 Wrap round at the ends of every list and repeat LT/RT and LB/RB while held

**Status:** Automated: T476-8 to T476-12 · QE: T476-8, T476-9 (app run) · Open for the owner: T476-13

Part 1 (hold). T476-1 to T476-7 (scenarios 1 to 7 of the issue) are added with part 2 (search and library wrap: T476-1 to -4, -6, -7) and part 3 (all other lists: T476-5).

- [x] **T476-8** Held LT/RT and LB/RB turn after 400 ms, then every 150 ms, on every page where they turn (library, search, detail page, launcher pages in the settings, tabs); the other buttons don't repeat · E2E: yes
- [x] **T476-9** A held stick, D-pad, LT/RT, LB/RB or key stops at the end (tabs, detail page, search row, ◀ ▶ selectors); only a fresh press goes round; a held ◀ ▶ doesn't flip a switch back and forth · E2E: yes
- [x] **T476-10** Hold RT (or the D-pad), then the page or layout changes (B closes the detail page, a dialog opens): the hold does nothing more, the new page acts only on a fresh press · E2E: yes
- [x] **T476-11** Hold RT while a rescan changes the number of games: it goes on with the new number, kept in range · E2E: yes
- [x] **T476-12** Pan hides or shows a controller while RT is held: released as before (#367), no repeat afterwards · E2E: yes
- [ ] **T476-13** **Owner:** On the real PC with the Steam Controller and an Xbox pad, hold RT in the library, the detail page and the search, and RB on the tabs: the speed feels right, it stops at the end, a fresh press goes on round; Q/E held do the same · E2E: no (feel of the real controllers and Windows' key repeat)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T476-8 to T476-12 | 2026-11-11 | Claude | PR (part 1), Linux container, unit and acceptance tests (`GamepadPollsTests`, `ListFocusTests`, `ValueCycleTests`, `HitWindowTests`, `HeldButtonsTests`, `HeldKeysTests`); each acceptance test red once with the feature broken | ✅ | – |
| T476-8, -9, -10, -12 | 2026-11-11 | QE | PR #479, Linux container: acceptance tests for the library, the search row, the launcher pages, a launcher's switch, the keyboard sliding in and Pan letting go; each red once | ✅ | – |
| T476-10 | 2026-11-11 | QE | PR #479, code review | ❌ a dialog of a settings category (prompt, "Controller zuordnen", question) doesn't stop a hold | fixed in this PR (Review, finding 1) |
| T476-10 | 2026-11-11 | Claude | PR #479, Linux container, `EveryControllerForGamesTests.A_held_D_pad_doesnt_reach_a_settings_dialog_opened_meanwhile` (red before the fix) | ✅ | – |
| T476-8, -9 | 2026-11-11 | QE | PR #479, real app under Xvfb, `--demo`, held E, Shift+E, Page Down (no gamepad) | ✅ library stops on the last page, tabs on the last tab, 34 to 84 repeats each | – |
