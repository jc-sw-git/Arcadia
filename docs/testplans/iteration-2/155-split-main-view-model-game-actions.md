# #155 Split MainViewModel: extract GameActions (step 1 of 4)

No new manual test cases: pure refactoring without a visible change, covered by acceptance tests written against the old
`MainViewModel` before any code moved (each made red once on purpose, see the PR). Touches T10-4, T11-1, T11-4, T11-6,
T12-7, T12-8, T12-9, T64-10, T127-3 and T127-4.

The transitions and contracts of #155 and where they are tested (automated only; `PageTransitionTests` unless named):

| ID | Transition or contract | Test |
|---|---|---|
| T155-1 | Library, A held: detail page in the tab's sort order, tab strip shown | `A_held_in_the_library_opens_the_detail_page_in_the_tabs_sort_order_with_the_tab_strip` |
| – | Library detail, B: library on the page of the game shown last, focus on it | T11-6 (`DetailPageTests`) |
| – | Library detail, LB/RB: next/previous tab | T11-4 (`DetailPageTests`) |
| – | Library detail, X: search over it; B there: back to it | T127-3 (`SearchHitTests`) |
| T155-2 | Rescan: library detail keeps its game, or shows the game now in its place; closes with the tab empty; the hidden library follows | `A_rescan_keeps_the_library_detail_page_on_its_game_…` |
| T155-3 | Search, B: layout menu first, then keyboard, then the search | `B_in_the_search_closes_the_layout_menu_first_…` |
| T155-4 | Search, A held with the keyboard in: details among the hits, no tab strip | `A_held_on_a_hit_with_the_keyboard_in_…` |
| – | Search, ≡ held: details among the hits; LB/RB and Y ignored there | T12-7, T12-8 (`SearchHitTests`) |
| T155-5 | Search over a library detail, B after a rescan: back to it (game gone: the one in its place); tab empty: library | `A_search_over_a_library_detail_page_returns_to_it_after_a_rescan_…` |
| – | Hit detail, X: search with its text, hit focused; B: library on the game's tab and page | T127-4 (`SearchHitTests`) |
| T155-6 | Rescan: search keeps its text with fresh hits; hit detail keeps its game or the one in its place; no hit left: search shows | `A_rescan_keeps_the_search_text_with_fresh_hits_…` |
| T155-7 | Rescan leaving no game: search closes, also under a hit detail | `A_rescan_that_leaves_no_game_closes_the_search_…` |
| T155-8 | B closing a dialog (view options, game menu, filter) leaves the page below as it was | `B_that_closes_a_dialog_leaves_the_page_below_as_it_was` |
| T155-9 | Another button or a page change while A is held: hold dropped, no late start or details | `Another_button_or_a_page_change_while_A_is_held_…` |
| T155-10 | Window loses focus while A or ≡ is held: same | `Losing_the_window_while_A_or_Menu_is_held_…` |
| T155-11 | Rescan while A is held: press moves to the game's new cover (held: details, tapped: start); game gone: dropped | `A_rescan_while_A_is_held_moves_the_press_…` |
| T155-12 | Rescan while ≡ is held on a hit (library hidden): press moves to the new hit, details open | `A_rescan_while_Menu_is_held_on_a_hit_…` |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T155-1 to T155-12 | 2026-10-25 | Claude | Branch before the split (old `MainViewModel`), Linux cloud container, Avalonia headless | ✅ (each also ❌ once with the code broken on purpose) | – |
| T155-1 to T155-12, all App and Core tests | 2026-10-25 | Claude | PR branch after the split, Linux cloud container, Avalonia headless | ✅ | – |
