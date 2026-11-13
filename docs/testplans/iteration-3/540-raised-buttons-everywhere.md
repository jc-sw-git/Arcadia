# #540 Raised buttons everywhere: settings rows, value arrows, selected tabs, checkmarks

Findings of the owner's hand test of #43: #531 left buttons flat in places. The cases were written by the implementing
thread from the issue's four changes, as the issue had none; the quality engineer checks them in the review.

**Status:** Automated: T540-3 (the selected tab drawn whole) · QE: T540-1, T540-2, T540-3, T540-4, T540-5 · Open for the owner: T540-6

Keys follow `docs/product/controls.md`; every case works with the gamepad alone.

- [x] **T540-1** Settings, every category: the pill on the right of an action row ("Jetzt einlesen", "Einfügen",
  "Öffnen", "Jetzt suchen", "Installieren", Pan's held pill) is raised like the detail page's buttons: lighter at the
  top, a soft shadow beneath, its label set in; with the row's focus it turns blue and stays raised. A row whose action
  can't run now ("Testen" without a key) lies flat and greyed. The shadow is soft on all sides, not cut off in a
  rectangle. · E2E: no (window: the look)
- [x] **T540-2** No ▶ at the end of a row or button that doesn't switch between values: the settings' action pills,
  the filter's categories ("Installiert", "Typ", …) and "Zu den Datenquellen" in "Artwork ändern". Where ◀ ▶ switch a
  value (launcher "Aktiv", "An"/"Aus", choices, the quick selection's "Steam-Controller-Modus", controller layouts, the
  first start), both arrows are small raised buttons; a greyed or unavailable row's arrows lie flat. ◀ ▶ and a click on
  an arrow still change the value. · E2E: no (window: the look)
- [x] **T540-3** The selected tab is raised (light, a shadow beneath) in the launcher tabs of the library, the settings
  categories, the tabs of "Datenquellen" and of "Artwork ändern"; it shows whole at the first, a middle and the last
  tab of a long strip; the other tabs stay flat. · E2E: no (window: covered by the window guard
  `The_selected_tab_shows_whole_at_the_first_a_middle_and_the_last_tab_of_a_long_strip`)
- [x] **T540-4** The detail page's quick selection: "Artwork ändern", "Tags", "Deinstallieren …", the choices and
  "Übersicht anpassen" (keeps its outline) are raised; a greyed entry lies flat; the focus ring sits around the raised
  entry. · E2E: no (window: the look)
- [x] **T540-5** The checkmark sits in the middle of its box in "Übersicht anpassen", the tag window and the filter
  ("Typ"), on the chosen tile in "Artwork ändern" and beside the chosen layout of the on-screen keyboard; the check after "Für alle Spiele übernehmen" and the one in the dialog after the controllers are released sit in the middle
  of their place. · E2E: no (window: pixel position)
- [ ] **T540-6** **Owner:** Look at the settings (all four categories), the library's tabs, a detail page and the
  filter on the TV: every button looks raised and the same, the checkmarks sit straight. · E2E: no (look and feel; the
  owner approves the design)

Automated tests:

| Test cases | Tests |
|---|---|
| T540-3 | `FocusRingTests.The_selected_tab_shows_whole_at_the_first_a_middle_and_the_last_tab_of_a_long_strip` (window guard, `main`) |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T540-1 | 2026-11-13 | Claude | PR #544, demo data, Linux/Xvfb | ❌ the pills' shadow cut off in a rectangle | fixed in this PR (`ContentControl.unclipped`) |
| T540-1 | 2026-11-13 | Claude | PR #544, demo data, Linux/Xvfb | ✅ | – |
| T540-2 | 2026-11-13 | Claude | PR #544, demo data, Linux/Xvfb (settings, filter, quick selection, "Artwork ändern") | ✅ | – |
| T540-3 | 2026-11-13 | Claude | PR #544, demo data, Linux/Xvfb; window guard | ✅ | – |
| T540-4 | 2026-11-13 | Claude | PR #544, demo data, Linux/Xvfb | ✅ | – |
| T540-5 | 2026-11-13 | Claude | PR #544, demo data, Linux/Xvfb ("Übersicht anpassen" at 1000 %) | ✅ | – |
| T540-1, -2, -4, T540-3 (QE run) | 2026-11-13 | Claude (QE) | branch at 3d91b60, Debug, Linux/Xvfb 1920 × 1080, `--demo`, keys by xdotool | ✅ T540-1: „Jetzt einlesen“ raised, shadow whole; T540-2: ◀ ▶ raised in the launcher rows, flat in the greyed quick selection entries, no ▶ on the filter's categories and on „Zu den Datenquellen“; T540-3: library tab and settings category and „Artwork ändern“ tab raised; T540-4: „Artwork ändern“, „Tags“, „Deinstallieren …“, „Übersicht anpassen“ raised, greyed choices flat | – |
| T540-5 (QE review) | 2026-11-13 | Claude (QE) | branch at 3d91b60, by geometry | ❌ two checks keep the old geometry in a square box: the chosen tile in „Artwork ändern“ (`artworkTickMark`, 16 × 16) and the chosen layout on the on-screen keyboard (20 × 20), each about 2–3 px too high | sent to the implementer |
| T540-5 | 2026-11-13 | Claude | PR #544 after the review, by geometry (every check now uses `CheckMarkIcon` in its proportions) | ✅ | – |
