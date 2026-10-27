# #277 Detect a Steam game start that Steam refused

A start the launcher aborts ends "Wird gestartet …" at the next poll of the running games (every 2 s): Arcadia comes to
the front as after a game's end (#94) and a toast names the reason. Launchers report it through
`IRunningGamesMonitor.RefusedStarts` (`Arcadia.Launchers.Abstractions`); Steam reads `LaunchApp failed with <code>` from
`logs/console_log.txt` (`SteamConsoleLog`). A start that times out after 2 min (any launcher) now toasts too.

| ID | Case | Test | E2E |
|---|---|---|---|
| T277-1 | Start an installed game the signed-in Steam account doesn't own (e.g. 3000th Duel, 1098080): "Wird gestartet …" ends within 5 s of Steam's error dialog, Arcadia comes to the front over it, toast „<Spiel>“: Das angemeldete Steam-Konto besitzt dieses Spiel nicht.; Steam's dialog stays open behind Arcadia; with the Steam Controller on SISR, the controller drives Arcadia again at once; A starts again | `A_start_the_launcher_refuses_ends_at_the_next_poll_with_its_reason_and_brings_Arcadia_to_the_front`, `After_a_refused_start_SISR_comes_back_at_once_so_the_Steam_Controller_drives_Arcadia_again` (fake launcher); Steam's log lines: `SteamConsoleLogTests` | yes (fake launcher) + by hand with real Steam |
| T277-2 | Start a game with a launch option dialog (e.g. 4231820) and choose within 2 min: "Wird gestartet …" stays until chosen, then the game runs; no toast | `Steam_waiting_for_the_player_and_a_start_that_completes_are_no_failure` (log lines only) | no (real Steam dialog) |
| T277-3 | Start a normal Steam game: runs as before, no toast of #277, Arcadia stays behind the game | `A_normal_start_runs_untouched_by_an_older_refusal_of_the_same_game` | yes + by hand with real Steam |
| T277-4 | A start that never runs and reports nothing: after 2 min "Wird gestartet …" ends with the toast „<Spiel>“ wurde nicht gestartet.; Arcadia stays where it is | `A_start_nothing_is_heard_of_ends_after_the_timeout_with_a_toast_and_leaves_Arcadia_where_it_is` | yes |
| T277-6 | Steam closed, then A on a game the account doesn't own (Steam starts first): as T277-1 | – | no (real Steam start) |
| T277-7 | A game with a pending update (Steam logs `AppError_18` and updates it first): "Wird gestartet …" stays while Steam updates, also past 2 min; the cover shows only the update's progress, preview and detail page show both without overlap (as #167); no toast of #277; then the game runs | `An_update_steam_starts_instead_of_the_game_is_no_refusal` (log lines), `An_update_the_launcher_runs_before_the_start_keeps_it_starting_and_the_cover_shows_only_the_update` (fake launcher) | yes (fake launcher) + by hand with real Steam |
| T277-5 | A failure code Arcadia doesn't know: toast „<Spiel>“: Steam hat den Start abgebrochen.; the code is only in `arcadia.log` | `A_refusal_without_a_known_reason_names_the_launcher` | yes (fake launcher) |

By hand, T277-1 also checks `arcadia.log`: a line `steam:<id>: der Launcher hat den Start abgebrochen (AppError_5)`, and no
path from Steam's line. Only `AppError_5` has its own text: the meaning of `AppError_28` (missing executable?) isn't
confirmed, so it gets the generic text until a test confirms it. `AppError_18` is Steam updating the game first (T277-7).

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T277-1, T277-3 to T277-5 (acceptance tests), Steam log parser and tracker unit tests, all App and Core tests | 2026-10-27 | Claude | PR branch, Linux cloud container, Avalonia headless; log lines copied from the owner's `console_log.txt` (2026-10-27) | ✅ (each acceptance test also ❌ once with the code broken on purpose, see the PR) | real Steam by hand still open |
| QE review fixes: SISR after a refusal, `AppError_18`, log read position; T277-1 to T277-7 automated parts, all App and Core tests | 2026-10-27 | Claude | PR branch, Linux cloud container, Avalonia headless | ✅ (the new SISR acceptance test and the `AppError_18` test also ❌ once with the code broken on purpose) | real Steam by hand still open |
| T277-7 acceptance test (owner chose "like an update" for `AppError_18`, display as #167) | 2026-10-27 | Claude | PR branch, Linux cloud container, Avalonia headless | ✅ (❌ once each with the cover badge showing "Startet" during the update and with the tracker ignoring the update) | real Steam by hand still open |
