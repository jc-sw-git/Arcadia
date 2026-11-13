# #532 Change artwork: Steam artwork back, loading state, sharp images, preview, format and ratings

**Status:** Automated: T532-1, T532-3, T532-5, T532-6, T532-8 · QE: T532-2, T532-4, T532-7, T532-9 · Owner passed: T532-10, T532-11

- [x] **T532-1** A game shows its launcher's image (automatic). In "Artwork ändern" A on a SteamGridDB tile, then A on the launcher's tile again, without closing the modal: the launcher's image is used again (set by hand, ticked), no warning "konnte nicht geladen werden". · E2E: yes
- [x] **T532-2** Detail page with several games with covers: RT twice quickly and once slowly, LT back: the cover in front is as sharp as when the detail page is opened directly on that game. · E2E: no (window: decode size; pinned by the guard `SharpCoverTests`)
- [x] **T532-3** With a key and a slow SteamGridDB, "Artwork ändern" opens: the grid shows the loading state with one of the loading texts; after 2.5 s another one; when the images are there, the tiles replace it. · E2E: yes
- [x] **T532-4** The loading state is animated (frames light up in turn, the magnifier moves to and fro), centred in the grid, readable. · E2E: no (window: animation)
- [x] **T532-5** The facts below the preview say "Format" (e.g. "600 × 900"), never "Größe". · E2E: yes
- [x] **T532-6** SteamGridDB tiles carry no rating tag and the facts no "Bewertung". · E2E: yes
- [x] **T532-7** The LB/RB glyphs beside "Cover" / "Hintergrund" are keycaps centred on the tabs' height, keyboard and controller glyphs; a click on one switches the tab. · E2E: no (window: layout)
- [x] **T532-8** After the SteamGridDB key is deleted, an artwork entry in "Warten auf deine Auswahl" says "Kein Schlüssel hinterlegt · SteamGridDB wird übersprungen", not "Schlüssel ungültig …"; nothing is asked. · E2E: yes
- [x] **T532-9** The preview shows the detail page as it is laid out: background over the whole page, the cover top left beside title and buttons, the stacks to the sides, the panel below; the cover tab rings the cover, the background tab the whole preview. · E2E: no (window: layout)
- [x] **T532-10** **Owner:** On the real PC, a Steam game that was blurry on the detail page: reach it with LT/RT from two games away: sharp. · E2E: no (real images and screen scaling)
- [x] **T532-11** **Owner:** On the real PC, a Steam game: choose a SteamGridDB cover, then Steam's again in the same modal: Steam's cover shows. · E2E: no (real Steam cache)

Automated tests:

| Test cases | Tests |
|---|---|
| T532-1 | `DataSourcesTests.The_launchers_image_in_use_can_be_chosen_again_after_a_steamgriddb_image`; `ArtworkByHandTests.The_launchers_image_in_use_can_be_taken_back_after_another_offer_in_the_same_modal` (Core) |
| T532-2 | guard `SharpCoverTests` (main only) |
| T532-3 | `DataSourcesTests.While_the_images_are_looked_for_the_modal_shows_the_loading_state_with_a_changing_text` |
| T532-5, T532-6 | `DataSourcesTests.A_steamgriddb_image_in_use_shows_its_format_and_no_rating` |
| T532-8 | `WaitingListLevelsTests.After_the_key_is_deleted_a_waiting_entry_says_there_is_no_key_not_that_it_is_invalid` |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T532-1, T532-3, T532-5, T532-6, T532-8 | 2026-11-13 | Claude | branch, Linux container, headless | ✅ (red first: T532-1 with the old offer of the file in use, T532-3 without the loading state, T532-8 with the old text) | – |
| T532-2 | 2026-11-13 | Claude | branch, Linux container, headless guard | ✅ (red before: cover decoded at 154 px, shown at 180 px) | – |
| T532-4, T532-7, T532-9 | 2026-11-13 | Claude | branch, Linux container, Xvfb 1920 × 1080, `--demo`, keyboard | ✅ looked at (loading state with a delay put in by hand for the look) | – |
| T532-2 | 2026-11-13 | QE (Claude) | branch, Linux container, headless guard (`WINDOW_GUARDS=1`) | ✅ (red with `RedrawImages` taken out) | – |
| T532-7, T532-9 | 2026-11-13 | QE (Claude) | branch, Release, Linux container, Xvfb 1920 × 1080, `--demo`, keyboard | ✅ keycaps centred on the tabs, preview as the detail page, ring on cover / whole background | – |
| T532-4 | 2026-11-13 | QE (Claude) | a3a1d45f, Release, Linux container, Xvfb, `--demo`, loading held 30 s by a local delay | ✅ frames light up in turn, magnifier moves to and fro, text changes; no "Cannot find the appropriate transform" in `arcadia.log` | – |
| T532-10, T532-11 | 2026-11-13 | Owner | `main` after #561, Release, his PC, TV, Steam Controller, his libraries | ✅ final test of #43; findings went into #560, fixed by PR #561 | – |
