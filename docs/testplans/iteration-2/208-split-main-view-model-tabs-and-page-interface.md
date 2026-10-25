# #208 Split MainViewModel: shared tabs component and the page interface (step 2 of 4)

No new manual test cases: a refactoring covered by acceptance tests. One small difference, by the #155 contract: a
page that loses the top drops its held buttons; before, X held in the search went on erasing under the settings (T208-1).
The transitions of #155 (T155-1 to T155-12) and every other App test stay green with their expectations unchanged;
tests only read the tabs at their new place (`Main.LauncherTabs.Items`, `.SelectedTab` instead of `Main.Tabs`,
`Main.SelectedTab`).

The page interface's contracts that had no test yet, automated only (`PageInterfaceTests`):

| ID | Contract | Test | E2E |
|---|---|---|---|
| T208-1 | A page that loses the top drops its held buttons: X held in the search stops erasing once the settings cover it | `X_held_in_the_search_stops_erasing_once_the_settings_cover_it_…` | yes |
| T208-2 | Run and install states reach the covers of every open page: library cover, hit and the hit's detail page at once | `An_install_in_the_launcher_shows_on_the_cover_on_the_hit_…` | yes |
| T208-3 | The input kind reaches the open page: the settings' text keyboard names keys after a key press, buttons after a pad press | `The_keyboard_of_a_text_in_the_settings_names_the_keys_…` | yes |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T208-1 to T208-3, all App and Core tests | 2026-10-25 | Claude | PR branch, Linux cloud container, Avalonia headless | ✅ (each also ❌ once with the code broken on purpose, see the PR; T208-1 also ❌ on `main` before this PR) | – |
