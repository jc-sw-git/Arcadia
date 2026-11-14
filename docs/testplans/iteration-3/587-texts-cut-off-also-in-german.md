# #587 Texts cut off also in German: quick selection rows and info cards with a button glyph

**Status:** Automated: T587-1, T587-2 · QE: T587-3 · Open for the owner: T587-4

- [x] **T587-1** On every page, dialog and settings category of the guards' page list, in every state of its focus, no text
  of Arcadia's own (a label, button, row title or subtitle, value line, info box) ends in "…", runs past its edge or has
  its lower edge cut by a button glyph in its line, in German,
  English, French and Spanish at 1920 × 1080; a game's or launcher's data (title, path, genre, tag) may. · E2E: yes
- [x] **T587-2** The same on the detail page inside every area: the rail with A's glyph on each area, every row of every
  area focused once with its info box, for a played game whose launcher defaults to "Arcadia" and for a never played game
  of "Ubisoft Connect" defaulting to "Steam-Eingabe"; the quick selection's "Steam-Controller-Modus" / "Modo del Steam
  Controller" and "Individueller Mausmodus", the value "Standard (Steam-Eingabe)" (quick selection, rail, area
  "Controller") and "Ubisoft Connect meldet keine" (playtime) show whole. · E2E: yes
- [ ] **T587-3** The detail page (overview, rail with A's glyph on "Controller" and "Verwalten") in German, English,
  French and Spanish, and the info panel of "Mit ⧉ zurück zu Arcadia" (value boxes with [R]) looked at in the running
  app: nothing of Arcadia's own cut, the quick selection and the facts beside it in line. · E2E: no (window)
- [ ] **T587-4** **Owner:** On your PC, set "Sprache" to Français, open a game's detail page and walk the rail and the
  quick selection with the controller, then the settings categories: every text of Arcadia's own is whole (only game
  titles, paths, genres and tags may end in "…"). · E2E: no (Windows' font rendering on a real screen)

Automated tests:

| Test cases | Tests |
|---|---|
| T587-1 | `Guards.NoLabelCutTests.No_text_of_Arcadias_own_is_cut_off_on_any_page_dialog_or_settings_category` (de-DE, en-US, fr-FR, es-ES; window guard, runs on `main`) |
| T587-2 | `Guards.NoLabelCutTests.No_text_of_Arcadias_own_is_cut_off_in_any_row_of_the_detail_pages_areas` (de-DE, en-US, fr-FR, es-ES; window guard, runs on `main`) |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T587-1, T587-2 | 2026-11-14 | Claude | Debug, Linux cloud container, headless window, before the fix | ❌ as intended (red once): quick selection "Steam-Controller-Modus", "Individueller Mausmodus" (de, en), "Mode du / Modo del Steam Controller" (fr, es); rail value line beside A's glyph on "Controller" (de, fr) and "Verwalten" (all four) | fixed in this PR |
| T587-3 | 2026-11-14 | Claude | Debug, Linux/Xvfb, `--demo`, keys, de/en/fr/es | ❌ the value boxes of "Mit ⧉ zurück zu Arcadia" cut their text's lower edge beside [R] (line height 21 px, glyph 22 px), in all four languages | guard extended (glyph taller than a fixed line), red in the controller settings' value boxes; fixed in this PR |
| T587-1, T587-2 | 2026-11-14 | Claude | Debug, Linux cloud container, headless window, after the fixes | ✅ | – |
| T587-3 | 2026-11-14 | QE (Claude) | `cbd2459`, Release, Linux/Xvfb, `--demo`, de/fr/es | ❌ F1: "Standard (Steam-Eingabe)" cut in the quick selection, rail and area "Controller" for a launcher defaulting to Steam Input; F2: "Ubisoft Connect n’en signale aucun" cut in the playtime fact | T587-2 extended with that game (red in all four languages), fixed in this PR |
| T587-1, T587-2 | 2026-11-14 | Claude | Debug, Linux cloud container, headless window, after the QE fixes | ✅ | – |
