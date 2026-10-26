# #180 Check UI changes visibly yourself: Linux path in arcadia-verify and a focus ring test

Cloud threads take screenshots of the real app under Linux with `tools/dev/screenshot-arcadia.sh` (Xvfb, `--demo`,
keys by xdotool), as `.claude/skills/arcadia-verify` describes, and follow its pre-push checklist. `RingGeometryTests`
checks on every page state with focusable buttons that no ring runs into a clipping parent; the pixel checks now also
cover the header tabs, the game menu and the tall Enter and layout menu of both on-screen keyboards (their edge keys
were covered since #110).

| ID | Case | Test | E2E |
|---|---|---|---|
| T180-1 | Following `arcadia-verify`, a cloud thread installs what is missing and takes screenshots of the library, the settings and the game menu with focus, full screen at 1920 × 1080 and with no pointer hovering | manual, by Claude in a cloud container | no |
| T180-2 | No ring is cut off by a clipping parent: library (first and second page), view options, game menu, detail page, search (empty, with hits), every settings category at its top and end, a launcher page, the first start, the mouse mode's keyboard and its layout menu | `RingGeometryTests` (5 tests) | yes |
| T180-3 | The selected header tab shows whole at the first, a middle and the last tab of a long strip; the ring shows whole on every game menu item, the tall Enter and every layout menu item, in the search and in the mouse mode | `FocusRingTests.The_selected_tab_shows_whole_…`, `…every_item_of_the_game_menu`, `…the_tall_Enter_and_the_layout_menu_of_the_search_keyboard`, `…of_the_mouse_modes_keyboard` | yes |
| T180-4 | With `ClipToBounds=False` for `ItemsControl` taken out of `Theme.axaml` (#145 / G-32), the geometric and the pixel tests go red; on the branch they are green | proof of red, recorded below and in the PR | no |

No manual case for the owner: the change adds tests, a script for cloud threads and docs; nothing in the app changes.
The pixel tests run on Windows in CI too (BGRA there, RGBA on Linux).

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T180-1 | 2026-10-26 | Claude | PR branch, Debug, Linux cloud container (Ubuntu 24.04, Xvfb, xdotool, ImageMagick 6.9) | ✅ library, settings (`Return`), game menu (`d d r s`) captured full screen, keys arrived | – |
| T180-2, T180-3, all App and Core tests | 2026-10-26 | Claude | PR branch, Linux cloud container, Avalonia headless | ✅ | – |
| T180-4 | 2026-10-26 | Claude | as above, `ItemsControl` style emptied | ❌ as wanted: all 5 `RingGeometryTests`, `FocusRingTests` grid corners and view options, `SearchViewTests` keyboard edges | – |
| T180-3, each red once | 2026-10-26 | Claude | as above, theme broken on purpose | ❌ as wanted: tab pushed 6 px out of its strip, `FocusRingInset` removed from game menu and layout items, Enter's ring stroke 0 | – |
