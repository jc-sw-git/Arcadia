# #210 Split MainViewModel: detail page, LibraryViewModel and routing in Main (step 4 of 4)

No new manual test cases: a refactoring covered by acceptance tests. The library is now a page (`LibraryViewModel`), the
detail page and the settings ask the window through `IPageHost` to close, loading, installs and running games moved to
`LibrarySession`, and `MainViewModel` only routes each press to the topmost layer. The transitions of #155 (T155-1 to
T155-12: A held in the library, B/LB/RB/X on a library detail page, the search's B, ≡ and A held, X and B on a hit's
detail page, rescans), T208-1 to T208-3, T209-1 to T209-3 and every other App test stay green with their expectations
unchanged; tests only read the library at its new place (`Main.Library.PageGames`, `.FocusedGame`, `.EmptyTab` …,
`Main.Session.…` for loading and polling).

The routing's contracts that had no test yet, automated only (`RoutingTests`):

| ID | Contract | Test | E2E |
|---|---|---|---|
| T210-1 | ≡ opens the settings over a detail page; ≡ or B closes them back to the same page and game, once (not opened again) | `Menu_opens_the_settings_over_a_detail_page_and_Menu_or_B_closes_them_…` | yes |
| T210-2 | ⧉ on a detail page opens no menu and doesn't reach the library under it | `Options_on_a_detail_page_opens_no_menu_and_leaves_the_library_under_it_alone` | yes |
| T210-3 | An open dialog takes ≡ and X: neither settings nor search open under it | `An_open_dialog_takes_Menu_and_X_so_neither_the_settings_nor_the_search_open_under_it` | yes |

**Measurement** (budgets in `CLAUDE.md`): before and after, see the PR. In the cloud container only the view model's share
was measurable (Release, Avalonia headless, 5,000 games); start to usable library and focus move on the owner's PC are
part of the owner's test before the merge.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T210-1 to T210-3, all App and Core tests | 2026-10-25 | Claude | PR branch, Linux cloud container, Avalonia headless | ✅ (each also ❌ once with the code broken on purpose, see the PR) | – |
