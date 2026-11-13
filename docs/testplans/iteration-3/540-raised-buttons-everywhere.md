# #540 Raised buttons everywhere: settings rows, value arrows, selected tabs, checkmarks

Findings of the owner's hand test of #43: #531 left buttons flat in places. The cases were written by the implementing
thread from the issue's four changes, as the issue had none; the quality engineer checks them in the review.

**Status:** Automated: T540-3 (the selected tab drawn whole) · QE: T540-1, T540-2, T540-3, T540-4, T540-5 · Open for the owner: T540-6

Keys follow `docs/product/controls.md`; every case works with the gamepad alone.

- [ ] **T540-1** Settings, every category: the pill on the right of an action row ("Jetzt einlesen", "Einfügen",
  "Öffnen", "Jetzt suchen", "Installieren", Pan's held pill) is raised like the detail page's buttons: lighter at the
  top, a soft shadow beneath, its label set in; with the row's focus it turns blue and stays raised. A row whose action
  can't run now ("Testen" without a key) lies flat and greyed. The shadow is soft on all sides, not cut off in a
  rectangle. · E2E: no (window: the look)
- [ ] **T540-2** No ▶ at the end of a row or button that doesn't switch between values: the settings' action pills,
  the filter's categories ("Installiert", "Typ", …) and "Zu den Datenquellen" in "Artwork ändern". Where ◀ ▶ switch a
  value (launcher "Aktiv", "An"/"Aus", choices, the quick selection's "Steam-Controller-Modus", controller layouts, the
  first start), both arrows are small raised buttons; a greyed or unavailable row's arrows lie flat. ◀ ▶ and a click on
  an arrow still change the value. · E2E: no (window: the look)
- [x] **T540-3** The selected tab is raised (light, a shadow beneath) in the launcher tabs of the library, the settings
  categories, the tabs of "Datenquellen" and of "Artwork ändern"; it shows whole at the first, a middle and the last
  tab of a long strip; the other tabs stay flat. · E2E: no (window: covered by the window guard
  `The_selected_tab_shows_whole_at_the_first_a_middle_and_the_last_tab_of_a_long_strip`)
- [ ] **T540-4** The detail page's quick selection: "Artwork ändern", "Tags", "Deinstallieren …", the choices and
  "Übersicht anpassen" (keeps its outline) are raised; a greyed entry lies flat; the focus ring sits around the raised
  entry. · E2E: no (window: the look)
- [ ] **T540-5** The checkmark sits in the middle of its box in "Übersicht anpassen", the tag window and the filter
  ("Typ"); the check after "Für alle Spiele übernehmen" and the one in the dialog after the controllers are released sit in the middle
  of their place. · E2E: no (window: pixel position)
- [ ] **T540-6** **Owner:** Look at the settings (all four categories), the library's tabs, a detail page and the
  filter on the TV: every button looks raised and the same, the checkmarks sit straight. · E2E: no (look and feel; the
  owner approves the design)

## Runs during implementation

- Linux cloud container (Ubuntu 24.04, Xvfb 1920 × 1080, `--demo`, keys for the gamepad): settings "Launcher" and
  "Datenquellen" (focus on an action row, greyed rows), library tab strip, filter, detail page with the quick selection,
  "Übersicht anpassen" (checkmarks at 1000 %). T540-1 to T540-5 looked at; the shadow of the settings' pills was cut off
  by the row's content and is fixed.
- `tools/ci/run-tests.sh 4` green; window guards: `The_selected_tab_shows_whole…` green after its probe learned the
  gradient; `No_ring_is_cut_off…` red on "Datenquellen › artwork-missing" exactly as on `main` since #543 (fixed there
  separately).
