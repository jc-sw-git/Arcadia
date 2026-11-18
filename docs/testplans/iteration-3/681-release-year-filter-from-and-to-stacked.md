# #681 Release-year filter: "From" and "To" stacked so the controller reaches both

**Status:** Automated: T681-1 · QE: T681-2 · Open for the owner: –

- [x] **T681-1** Filter (R3) → "Erscheinungsjahr" → A: "von" has the ring; down moves to "bis", up back to "von", up once more to the quick picks; ◀▶ on either only steps its year and keeps the ring there; B goes back · E2E: yes
- [x] **T681-2** Running app, filter at "Erscheinungsjahr": "von" and "bis" stand one below the other at full width, the switch "Ohne Angabe einbeziehen" shows below them once a year range is chosen; the ring around "von" and "bis" is whole on all sides; mouse clicks on ◀▶ step the year as before · E2E: no (window)

Automated tests:

| Test cases | Tests |
|---|---|
| T681-1 | `LibraryMetadataFilterTests.Von_and_bis_stack_so_up_and_down_alone_reach_both_and_left_right_only_step_the_year`; ◀▶, LT/RT in `…The_years_step_by_one_and_ten_within_the_tabs_years_and_von_never_passes_bis` (T466-11) |
| T681-2 | `RingGeometryTests` (filter page states) |

Other side-by-side pairs checked: "Übersicht anpassen" moves sideways between its two columns with ◀▶, the info grids on the detail page and in the library's side panel hold no focusable fields; nothing else needs "down" to reach a field on the right.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T681-1, all `LibraryMetadataFilterTests` | 2026-11-18 | Claude | PR branch, Release, Linux headless | ✅ 26/26 | – |
| T681-2 | 2026-11-18 | Claude | PR branch, Debug, Linux Xvfb 1920 × 1080, `--demo`, keys G, S ×5, Space, then S, A | ✅ stacked, ring on "von" then on "bis", "bis" 2025 → 2024 with A (◀), switch below; `RingGeometryTests` 37/37; mouse: two clicks on ◀ of "bis" 2025 → 2023, ◀ of "von" stays at the tab's first year 1995 | – |
| QE: T681-2 | 2026-11-18 | Claude (QE) | PR branch, Linux Xvfb, `--demo` | ✅ stacked, ring whole, mouse clicks on ◀▶ 1995 → 1996 and 2025 → 2023, switch below once a year is chosen | – |
