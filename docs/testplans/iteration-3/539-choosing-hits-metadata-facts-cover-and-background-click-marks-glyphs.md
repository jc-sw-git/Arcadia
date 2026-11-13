# #539 Choosing hits: metadata facts instead of covers, cover and background together, click marks, real button glyphs

Derived from the owner's findings in the hand test of #43 as written in #539. Keys follow `docs/product/controls.md`
(WASD, Space = A, Esc = B, V = Y). Every game of the made-up launcher has neither cover nor background, so it seeks both.

**Status:** Automated: T539-1 to -7, -10, -11 · QE: T539-8, -9 (implementer and QE) · Open for the owner: T539-12

- [x] **T539-1** "Warten auf deine Auswahl" in "Metadaten": the hits are a list (name, year, first genre · developer), no
      covers, ▲ ▼ through it; beside it an info box with the marked hit's ERSCHIENEN, GENRES, ENTWICKLER, PUBLISHER,
      ALTERSFREIGABE (only what IGDB has, checked) and the start of the description (cut at a word after 600 characters);
      the box follows the focus; A takes the hit. · E2E: yes
- [x] **T539-2** A hit IGDB knows only by name: the box says "IGDB hat zu diesem Spiel keine weiteren Angaben." · E2E: yes
- [x] **T539-3** A game seeking cover and background: tabs "Cover" (selected) and "Hintergrund" with LB/RB, the footer
      "Cover nehmen, weiter zum Hintergrund"; A on a cover marks it as "DEIN COVER" and turns to "Hintergrund", the
      backgrounds three a row, the focus on the cover's own game; A on another game's background takes both and goes on
      to the next game. · E2E: yes
- [x] **T539-4** RB first to "Hintergrund": A picks a background and turns to "Cover"; LB/RB keep each tab's pick
      (marked, focused); A on a cover then takes both. B in the hits goes to the games list and takes nothing. · E2E: yes
- [x] **T539-5** Cover and background from two SteamGridDB games: each kind comes from its own game. · E2E: yes
- [x] **T539-6** After a restart and "Alle Artworks neu laden" both games are asked again, not searched by name;
      "Artwork ändern" offers each kind's images of its own game; a stored background game that can't be read falls back
      to the cover's game. · E2E: yes
- [x] **T539-7** A click on a hit only marks it (the focus comes onto it, also from the games list); a double click takes
      it as A does (picks it for its tab; with both picked, takes them); a click on a hit of a game shown before takes
      nothing. · E2E: yes
- [x] **T539-8** The look: the tabs with "DEIN COVER" / "DEIN HINTERGRUND", the metadata list beside its info box, the focused row's ring,
      the note's glyph inside its text. · E2E: no (window)
- [x] **T539-9** Mouse for real: single click marks, double click takes, on both tabs and in the metadata list.
      · E2E: no (real pointer gesture in the window)
- [x] **T539-10** The note says "Keins davon passt? Mit [Y] überspringst du …" with the skip button's glyph, in both
      lists; no text of the App's strings or `AppTexts.resx` names a bare button. · E2E: yes
- [x] **T539-11** With the keyboard in use, a button in a text shows the key that does the same (Y as V, A as Leertaste,
      B as Esc, …); a text about one controller only keeps its buttons. · E2E: yes
- [ ] **T539-12** **Owner:** with the Steam Controller, choose cover and background from two different hits of a real
      waiting game and look at the result in the library and on the detail page; on the metadata list, judge whether the
      info box tells namesakes apart; read the new texts (tabs, "DEIN COVER", "DEIN HINTERGRUND", "Noch nicht gewählt", the empty box).
      · E2E: no (owner's library and wording)

Automated tests:

| Test cases | Tests |
|---|---|
| T539-1 | `Arcadia.App.Tests/WaitingListMetadataTests.A_game_with_several_igdb_hits_waits_on_the_metadata_list_apart_from_its_artwork_entry_and_keeps_its_launcher_data`, `…A_on_a_metadata_hit_shows_its_fields_and_title_everywhere_at_once_and_y_skips_for_good` (▼ in the list); `WaitingHitFactsTests.The_box_shows_what_igdb_has_checked_in_the_detail_pages_order`, `…A_long_description_is_cut_at_a_word_and_one_without_spaces_at_the_limit` |
| T539-2 | `Arcadia.App.Tests/WaitingHitFactsTests.Without_any_fact_the_box_says_so` |
| T539-3 | `Arcadia.App.Tests/WaitingListChoiceTests.A_on_a_cover_picks_it_and_shows_the_backgrounds_on_its_own_game_where_another_one_can_be_taken`; own term on both tabs: `WaitingListLevelsTests.A_result_of_the_own_term_is_taken_and_the_term_isnt_kept` |
| T539-4 | `Arcadia.App.Tests/WaitingListChoiceTests.LB_RB_turn_the_tabs_each_keeps_its_pick_and_the_background_can_be_picked_first`, `…B_goes_back_to_the_games_and_skips_nothing` |
| T539-5 | `Arcadia.Core.Tests/Artwork/ArtworkServiceByNameTests.Cover_and_background_chosen_from_two_games_each_come_from_their_own` |
| T539-6 | `Arcadia.Core.Tests/Artwork/ArtworkServiceByNameTests.Two_chosen_games_last_across_a_restart_and_a_reload`, `…Artwork_aendern_offers_each_kind_from_its_chosen_game`, `…A_stored_background_game_that_cant_be_read_falls_back_to_the_cover_game` |
| T539-7 | `Arcadia.App.Tests/WaitingListChoiceTests.A_click_only_marks_a_hit_and_a_double_click_takes_it`, `…A_click_on_a_hit_of_a_game_shown_before_takes_nothing` |
| T539-10 | `Arcadia.App.Tests/WaitingListChoiceTests.The_notes_name_the_skip_button_as_a_glyph`; `Arcadia.Core.Tests/ButtonGlyphTextTests.Resource_texts_write_controller_buttons_as_glyphs` |
| T539-11 | `Arcadia.App.Tests/KeyNamesTests` (each key does what its button does, every button checked, Guide keeps its badge) |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T539-1 to -7, -10, -11 | 2026-11-13 | implementer | Linux cloud container, Release, `tools/ci/run-tests.sh 4` | pass, but for `MetadataSettingsTests.Jetzt_suchen_asks_at_once_for_what_is_missing_without_a_scan` once in a shard; it passed 3 of 3 alone | Seven tests of #491/#506 took a hit with one A; they take cover and background in two steps now |
| T539-8 | 2026-11-13 | implementer | Linux, headless window drawn by Skia (1920 × 1080), the made-up launcher and fake SteamGridDB/IGDB, controller and keyboard presses | pass | Metadata rows first hugged their text and had uneven heights; now as wide as the list and of one height. Pictures: `captures-539/` in the project files |
| T539-9 | 2026-11-13 | implementer | Linux, headless window drawn by Skia, pointer events on the tiles | pass | One click marks (focus into the hits, nothing taken), a second click 600 ms later takes nothing, a double click takes the cover and then the background; the metadata rows share the handler, not clicked separately |
| T539-8 | 2026-11-13 | implementer | Linux, headless window drawn by Skia (1920 × 1080), as above, after the owner asked for tabs instead of two steps | pass: tabs "Cover"/"Hintergrund" with LB/RB (⇧ Q/⇧ E with the keyboard), "DEIN COVER"/"DEIN HINTERGRUND" with "Noch nicht gewählt", the pick's accent frame apart from the focus ring | Two steps replaced by tabs; pictures `captures-539/tab-*.png` |
| T539-9 | 2026-11-13 | implementer | as above, real pointer events (press and release, twice for the double click) | pass: a click on the tab "Hintergrund" turns to it, a click marks, a double click picks for its tab and turns, a double click on a cover then takes both | – |
| T539-1 to -7, -10, -11 | 2026-11-13 | implementer | Linux cloud container, Release, `tools/ci/run-tests.sh 4`, after the tabs | pass (Core 3203, Pan 438, App 867 in 4 shards) | – |
| T539-1 to -7, -10, -11 | 2026-11-13 | QE | Linux cloud container, Release, `tools/ci/run-tests.sh 4` | pass (Core 3203, Pan 438, App 4 shards green); T539-5 and T539-7 red once by mutation (background game dropped; a click taking the hit) | Found: B in the hits keeps the picks (see PR Review) |
| T539-8, -9 | 2026-11-13 | QE | Linux, headless window drawn by Skia (1920 × 1080), the made-up launcher and fake SteamGridDB, controller presses, keyboard (D) and a real pointer click | pass: both tabs, "DEIN COVER"/"DEIN HINTERGRUND", focus on the pick's own game, a click on a background marks it and takes nothing, with the keyboard ⇧ Q/⇧ E, V, Esc, Leertaste, WASD | Pictures `captures-539/qe/` in the project files |
