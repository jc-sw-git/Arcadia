# #209 Split MainViewModel: the search page handles its own input (step 3 of 4)

No new manual test cases: a refactoring covered by acceptance tests. The search now takes all its buttons itself (B, L3,
≡, A on a hit, ⧉) and asks the window through `IPageHost` to close it, show the hit's menu or the shared filter dialog.
The transitions of #155 (T155-1 to T155-12), T208-1 to T208-3 and every other App test stay green with their expectations
unchanged.

Paths this step moved that had no test yet, automated only:

| ID | Contract | Test | E2E |
|---|---|---|---|
| T209-1 | The filter over the search stays open with the search's options after a rescan, and closes with the search once no game is left | `The_search_filter_stays_open_with_the_searchs_options_after_a_rescan_…` | yes |
| T209-2 | A click on "Schließen" leaves a search opened over a detail page for that page, as B does | `A_click_on_Schliessen_leaves_a_search_opened_over_a_detail_page_…` | yes |
| T209-3 | A search closed after its filter is not kept alive by the closed dialog (Codex review) | `A_search_closed_after_its_filter_is_not_kept_alive_…` | no |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T209-1 to T209-3, all App and Core tests | 2026-10-25 | Claude | PR branch, Linux cloud container, Avalonia headless | ✅ (each also ❌ once with the code broken on purpose, see the PR) | – |
