# #227 Check a focused game's install folder off the UI thread

A focus move no longer asks the disk on the UI thread whether the focused game's install folder is still there.
`Services/InstallFolderCheck` asks on the thread pool: one check at a time, at most one waiting behind it (the last cover
focused), and only the last answer counts. A folder gone still leads to the rescan of before.

| ID | Case | Test | E2E |
|---|---|---|---|
| T227-1 | A game uninstalled in its launcher while Arcadia wasn't looking: focusing its cover leads to one rescan, after which it shows as not installed | `A_focused_game_whose_folder_is_gone_leads_to_one_rescan_that_shows_it_uninstalled` | yes |
| T227-2 | A disk that doesn't answer: five focus moves take well under a second and land on the right cover; one check hangs, only the last of the others waits | `A_disk_that_does_not_answer_holds_up_no_focus_move_and_rapid_moves_leave_one_check_waiting` | yes |
| T227-3 | A late answer "folder gone" for a cover that lost the focus meanwhile starts no rescan | `A_late_answer_for_a_game_that_lost_the_focus_starts_no_rescan` | yes |
| T227-4 | A late answer "folder gone" after the focus moved on to a game that isn't installed (so no newer check) starts no rescan | `A_late_answer_after_the_focus_moved_to_an_uninstalled_game_starts_no_rescan` | yes |
| T227-5 | A check that throws is logged, and the check waiting behind it still runs | `A_check_that_fails_is_reported_and_the_check_waiting_behind_it_still_runs` | yes |

No manual case: a disk that doesn't answer is covered by T227-2 with a fake that hangs; the owner has no such setup to
reproduce it for real (owner 2026-10-26), and on an ordinary disk the check takes microseconds.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T227-1 to T227-5, all App and Core tests | 2026-10-26 | Claude | PR branch, Linux cloud container, Avalonia headless | ✅ (each also ❌ once with the code broken on purpose, see the PR) | – |
