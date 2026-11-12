# #496 Change a game's title, reset it and show the original

Derived from the specification in #43 (decision "Manual changes → Title", scenario 6, the user-data failure cases,
scenario 12) and the scope of #496 before any code; the PR copies them into
`docs/testplans/iteration-3/496-change-a-games-title.md`. Every case runs with the gamepad alone; keys follow
`docs/product/controls.md` (WASD, Space = A, Esc = B, G = R3). "Automatic title" is the title Arcadia would show
without a change (launcher or provider, #493/#494); "own title" is the one the player typed. Automated cases use
made-up Steam and Epic fixtures (`ArcadiaDriver`); window cases are not window tests of this issue (#517) and run in
the quality engineer's pass. Planned: automated T496-1 to -12, -14, -15; quality engineer's run T496-13, -16, -17;
owner T496-18.

Assumptions where the specification is silent (proposed, waiting for the owner's OK before implementation): ↵ takes
the title, B on the keyboard keeps the old one; an empty title or one equal to the automatic title clears the change;
the search finds a game by its own and by its automatic title; the [R3] line goes off when the carousel turns to
another game; while the user-data file is unreadable "Titel ändern" and "Titel zurücksetzen" are greyed out.

Implemented as the assumptions propose; the owner judges them in #43's final manual test. In addition: an own title
holds at most 200 characters (the field says "Höchstens 200 Zeichen" beyond); like every game action, "Titel ändern …"
and "Titel zurücksetzen" are also in the context menu (`docs/product/detail-page.md`: menu and detail page offer the
same actions). The owner's cases are collected by the closing thread of #43.

**Status:** Automated: T496-1 to -12, -14, -15 · QE: T496-13, T496-16, T496-17 · Open for the owner: T496-18

**Change and reset**

- [x] **T496-1** Given a game's detail page, area "Verwalten": the rows "Titel ändern" and "Titel zurücksetzen" are there
      beside "Artwork ändern"; while the game has no own title "Titel zurücksetzen" is greyed out, takes the focus and
      the info box says why. · E2E: yes
- [x] **T496-2** A on "Titel ändern" opens the on-screen keyboard prefilled with the title shown now (the automatic
      one, or the own one if set). Type a new title, ↵: the keyboard closes, the focus is back on "Titel ändern", the
      page's title is the new one at once and "Titel zurücksetzen" is no longer greyed out. · E2E: yes
- [x] **T496-3** B on the keyboard (Esc with the real keyboard) closes it without a change; the title stays as before.
      With a real keyboard the player can type into the field and Enter takes the title like ↵. · E2E: yes
- [x] **T496-4** Leading and trailing spaces are dropped; an empty title (or only spaces) and a title equal to the
      automatic one change nothing or clear an existing change (no own title, no [R3] line, "Titel zurücksetzen"
      greyed out). Umlauts, `ß`, `?`, quotes and a very long title are stored and shown unchanged; a long one is cut
      with "…" where the space ends, as any long title. · E2E: no (variant: unit test)
- [x] **T496-5** Given a game with an own title, A on "Titel zurücksetzen": the automatic title is back at once on the
      page and everywhere else (T496-7), the row is greyed out again, and after a restart the automatic title stays.
      · E2E: yes

**Where the title applies**

- [x] **T496-6** An own title survives a restart, a rescan, "Alle Metadaten neu laden" (#43 scenario 8) and an automatic
      title that changes later (e.g. IGDB corrects "K?ln" after the change): the own title stays shown, the
      "Original-Titel" line shows the new automatic one. · E2E: no (variant: unit test)
- [x] **T496-7** After a change the own title shows, without a rescan, on the library tile and its preview, the
      placeholder tile's title, the detail page, the search hits, the "Artwork ändern" modal's header and the waiting
      list "Warten auf deine Auswahl". · E2E: yes
- [x] **T496-8** Sorting by name (ascending and descending) uses the own title: a game renamed from "Zebra" to "Aal"
      moves to the front at once, also across pages; the detail page carousel (LT/RT) follows the new order; B from the
      detail page returns to the library page where the game is now, with it focused. · E2E: yes
- [x] **T496-9** Sorting variants: a mix of own and automatic titles, case and umlauts sort as the name sort does today;
      install date and last played are unaffected; with 5,000 games of which 100 have own titles the sort stays
      within the start budget. · E2E: no (variant: unit test)
- [x] **T496-10** The search finds a renamed game by its own title and by its automatic title; the hit shows the own
      title; sorting the search by name uses the own title. · E2E: yes

**Original title on the detail page**

- [x] **T496-11** Given a game with an own title, the detail page shows the hint "[R3] Original-Titel" (exact text from the
      PR's UI text list); R3 (G) shows the line "Original-Titel: <automatic title>" under the title, R3 again hides it.
      Without an own title there is no hint and R3 does nothing on the detail page. · E2E: yes
- [x] **T496-12** The line is off again after leaving the page (B) and opening it again, after LT/RT to another game and
      back, and after LB/RB to another tab; R3 still works from the buttons, the rail and the rows. · E2E: yes
- [ ] **T496-13** Look: the "Original-Titel" line sits under the title, does not push the button row out of place or
      overlap the artwork's scrim; the [R3] hint stands with the other hints; colours from `Theme.axaml`; a long
      original title is cut with "…". · E2E: no (window)

**User data and failures**

- [x] **T496-14** Own titles are kept with the own tags (#495) per game (launcher and launcher id): a game that leaves the
      library (uninstalled, account switched, launcher off) and comes back has its own title again; nothing else is
      lost. Written to a temporary file and renamed; Arcadia closed or crashed while writing leaves the old file
      whole. · E2E: no (variant: unit test)
- [x] **T496-15** Given an unreadable user-data file at start: it is never overwritten, a copy stays beside it, a status
      message says so, the failure is logged (no titles or paths of the user in the log line beyond the file name);
      every game shows its automatic title, "Titel ändern" and "Titel zurücksetzen" are greyed out with the reason in
      the info box. After the file is repaired or deleted and Arcadia started again, everything works. Writing that
      fails while Arcadia runs (disk full, file locked): logged with stack trace, toast, the title shown stays the one
      stored, Arcadia keeps running. · E2E: no (unit test; one acceptance test for the status message)
- [ ] **T496-16** Controller only, B back: library → A held → ▼ → "Verwalten" → A → "Titel ändern" → type with the
      D-pad and A → ↵ → R3 → R3 → "Titel zurücksetzen" → B → B; the library shows the automatic title again. The same
      with keys only (WASD, Space, Esc, G). · E2E: no (real gamepad in the quality engineer's run; the key path is
      covered by T496-2, -5, -11)
- [ ] **T496-17** Release build, demo data with 1,000 games: changing a title, resetting it and toggling the line each
      show within 50 ms (`Bild:` lines); the UI thread never waits for the file write. · E2E: no (Release timings;
      quality engineer's run)
- [ ] **T496-18** **Owner:** own library, Windows, Steam Controller: give an Epic game with a broken title (`?` in it)
      an own title, check library, sorting by name and the search; R3 on its detail page shows the broken original;
      close and start Arcadia: the own title is still there; "Titel zurücksetzen" brings the original back.
      · E2E: no (own library, real Steam Controller)

Automated tests:

| Test cases | Tests |
|---|---|
| T496-1, -2, -7 (page, carousel, library, "Artwork ändern"), -8, -11 | `Arcadia.App.Tests/ChangeTitleTests.A_new_title_shows_at_once_everywhere_and_moves_the_game_in_the_sort` |
| T496-3 | `Arcadia.App.Tests/ChangeTitleTests.B_on_the_keyboard_keeps_the_title_and_Enter_on_the_real_keyboard_takes_it` |
| T496-5, -12 | `Arcadia.App.Tests/ChangeTitleTests.A_reset_brings_back_the_automatic_title_and_the_line_is_off_on_every_new_page` |
| T496-6 (restart, rescan), -14 (absence) | `Arcadia.App.Tests/ChangeTitleTests.An_own_title_survives_a_restart_a_rescan_and_the_game_leaving_and_coming_back` |
| T496-10 | `Arcadia.App.Tests/ChangeTitleTests.The_search_finds_a_renamed_game_by_both_titles_and_shows_the_own_one`, `Arcadia.Core.Tests/UserData/GameTitlesTests` |
| T496-15 | `Arcadia.App.Tests/ChangeTitleTests.An_unreadable_file_is_a_status_message_and_greys_both_rows_with_the_reason`, `Arcadia.Core.Tests/UserData/UserDataStoreTests` |
| T496-7 (waiting list), -8 (across pages), -12 (LB/RB) | `Arcadia.App.Tests/ChangeTitleAcrossTheAppTests` (quality engineer; red once each: the waiting list given the launcher's title, the line kept across pages, own titles left out of `GameTitles.Shown`) |
| T496-4, -6 (automatic title changing later), -9 | `Arcadia.Core.Tests/UserData/GameTitlesTests` |
| T496-14 (per key, temporary file, crash), -15 (failed write) | `Arcadia.Core.Tests/UserData/UserDataStoreTests` |
| T496-8 (the game's menu, across pages) | `Arcadia.App.Tests/ChangeTitleTests.From_the_game_menu_the_focus_follows_the_renamed_and_the_reset_game_across_pages` (red once: the focus left on the old slot) |
| T496-15 ("Zu den Einstellungen" opens "Eigene Titel") | `Arcadia.App.Tests/ChangeTitleTests.An_unreadable_file_is_a_status_message_and_greys_both_rows_with_the_reason` |
| A provider's batch without a new shown title (seam with #494) | `Arcadia.App.Tests/ChangeTitleTests.A_providers_batch_that_changes_no_shown_title_leaves_the_library_as_it_is` (red once: every batch rebuilt the libraries) |
| T494-25 (IGDB's title in library, sorting, search) | `Arcadia.App.Tests/IgdbSettingsTests.Igdbs_title_for_a_doubtful_launcher_title_shows_in_the_library_the_sorting_and_the_search` (red once: `GameTitles` without `MetadataService.TitleOf`) |

"Alle Metadaten neu laden" (T496-6) touches only the metadata cache, not `user-data.json`.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| All automated tests (`run-tests.sh 4`, window guards on) | 2026-11-12 | implementer | Release, Linux cloud container (Xvfb) | passed | — |
| App look: Verwalten rows, dialog, changed title, R3 line, footer hint | 2026-11-12 | implementer | Debug, Linux cloud container (Xvfb, `--demo`, keys) | passed after widening the field (it was 250 px) | field style moved after the general field styles |
| QE review: all tests, `ChangeTitleAcrossTheAppTests` red once each | 2026-11-12 | quality engineer | Release, Linux cloud container | passed | — |
| QE run T496-13, -16 (keys), -17, T496-15 status message | 2026-11-12 | quality engineer | Release, Linux cloud container (Xvfb, `--demo --demo-games 1000`, keys) | failed | two-line own title with the R3 line hides the button row behind the areas; "Titel ändern" opens in 380–560 ms (search with keyboard 20–90 ms); status line cut, "Zu den Einstellungen" lands on the SteamGridDB key; reset 40 ms, R3 ≤ 1 ms |
| Fixes of the QE's first pass: "Titel ändern" press to frame (`Bild: Accept`), 1,000 demo games | 2026-11-12 | implementer | Debug, Linux cloud container (Xvfb, `--demo --demo-games 1000`, keys) | first opening 15.5 ms, later 6–8 ms (before: 525–688 ms); "Artwork ändern" 81 ms | view kept by `MainWindow`, its keyboard drawer in the layout from the start |
| Fixes of the QE's first pass: two-line own title with the R3 line | 2026-11-12 | implementer | Debug, Linux cloud container (Xvfb, `--demo`, keys) | passed: the line takes the info line's place, the button row stays above the areas | — |
