# #228 Follow-ups to the MainViewModel split: sorted snapshot, UI-free session, page lifecycle

No new manual test cases: a refactoring covered by tests. Nothing the player sees changes; the transitions of #155, #208,
#209 and #210 (T155-*, T208-*, T209-*, T210-*) and every other App test stay green with their expectations unchanged.
What changed inside: `LibraryViewModel` keeps each tab's sorted list until a rescan or the sort changes it,
`LibrarySession` (now in `Services/`) refers to no view model and raises events that Main turns into tabs, covers and
toasts, and Main runs one lifecycle for every page (`OnPagesChanged`), disposing closed pages there and all open ones,
with the session's delayed rescans, when Arcadia closes.

The new contracts, automated only:

| ID | Contract | Test | E2E |
|---|---|---|---|
| T228-1 | A tab is sorted once: page changes, the detail page and a rescan that changed nothing keep the sorted list; a new sort or a changed library sorts it again; a tab switched off lets go of its sorted list while another stays selected | `LibraryTests.A_tab_is_sorted_once_and_again_only_after_a_new_sort_or_a_rescan_that_changed_it`, `…A_tab_switched_off_lets_go_of_its_sorted_games_while_another_tab_stays_selected` | yes |
| T228-2 | The library session refers to no App view model, reads the launchers and says when a rescan starts and ends | `LibrarySessionTests.The_session_refers_to_no_view_model_of_the_App`, `…hands_out_what_it_read_and_says_when_a_rescan_starts_and_ends` | no |
| T228-3 | An uninstall is read again after a moment, unless the session ended meanwhile | `LibrarySessionTests.An_uninstall_is_read_again_after_a_moment_unless_the_session_ended_meanwhile` | no |
| T228-4 | Closed settings are disposed; settings and a search still open are disposed once when Arcadia closes | `PageInterfaceTests.Closed_settings_are_disposed_and_so_are_the_pages_still_open_when_Arcadia_closes` | yes |

**Measurement** (page change, view model only, Release, Avalonia headless, 5,000 games, 100 changes per round): before
4.6–7.8 ms, after 0.14–0.53 ms per page change. Rendering is not part of it (#229 measures that on the owner's PC).

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T228-1 to T228-4, all App and Core tests | 2026-10-26 | Claude | PR branch, Linux cloud container, Avalonia headless | ✅ (each also ❌ once with the code broken on purpose, see the PR) | – |
| T228-1 (switched-off tab), after the Codex review | 2026-10-26 | Claude | PR branch, Linux cloud container, Avalonia headless | ✅ (❌ once with the pruning left out) | – |
