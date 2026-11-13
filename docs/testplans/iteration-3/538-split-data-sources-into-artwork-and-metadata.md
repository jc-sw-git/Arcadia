# #538 Split "Datenquellen" into Artwork and Metadaten, with clearer rows and status

Derived from the owner's findings in the hand test of #43 as written in #538; the tab naming was settled in the thread
("Datenquellen" stays one category with two tabs "Artwork" and "Metadaten", each with an icon). Keys follow
`docs/product/controls.md` (WASD, Space = A, Esc = B, Q/E = LT/RT).

**Status:** Automated: T538-1 to -8 · QE: T538-9, -10 · Open for the owner: T538-11

- [x] **T538-1** ≡, RB to "Datenquellen": two tabs "Artwork" (chosen) and "Metadaten" above the rows; "Artwork" holds
      STEAMGRIDDB, REIHENFOLGE FÜR ARTWORK, ARTWORK; RT shows "Metadaten" at its first row with IGDB, REIHENFOLGE FÜR
      METADATEN, METADATEN, EIGENE DATEN ("Eigene Tags"); LT back to "Artwork" at its first row; the footer names LT/RT
      "Tab"; B closes the settings. · E2E: yes
- [x] **T538-2** "Metadaten" has its own "Bei mehreren Treffern": "Ersten nehmen" there takes the first IGDB hit of every
      waiting game (one toast) and leaves the artwork list and the artwork's choice alone; a `settings.json` from before
      keeps the old shared choice for metadata until it is changed. · E2E: yes
- [x] **T538-3** Its own "Warten auf deine Auswahl": a game waiting for both kinds counts once in each tab; A opens a list
      with only that tab's games (name and launcher, no "Artwork"/"Metadaten" marker); Y skips there without touching the
      other list; B comes back to the row. · E2E: yes
- [x] **T538-4** "Fehlende Metadaten" has "Jetzt suchen": A asks at once for what is missing without a scan, the row
      works and is greyed out until the run ends, then shows the new count. · E2E: yes
- [x] **T538-5** No row per launcher under METADATEN; the info box of "Launcher-Daten" has a box per launcher with its logo
      and name at the top and what it delivers. · E2E: yes
- [x] **T538-6** Key, Client-ID and Client-Secret: the subtitle says only the state ("Verbunden", "Noch nicht geprüft",
      "Client-Secret fehlt", …) on the lamp's colour (green, grey, yellow), not the key's end. · E2E: yes
- [x] **T538-7** A rejected IGDB access shows its warning symbol in the library header's status chip, as a rejected
      SteamGridDB key does (not only the Y glyph). · E2E: yes
- [x] **T538-8** Demo games (`--demo`): SteamGridDB and IGDB are never asked for them, none waits for a choice, they count
      neither as missing nor in a search; IGDB data cached for one earlier no longer shows. · E2E: yes
- [x] **T538-9** The look: both tabs with icons and the chosen one highlighted, a warning beside a tab, the tinted
      subtitles in each state, the "Launcher-Daten" boxes with logos, the header chip with the IGDB symbol. · E2E: no (window)
- [x] **T538-10** Opening "Datenquellen" (from "Über Arcadia" and from "Controller") reacts as fast as the other
      categories: press-to-frame from `arcadia.log` (`Bild: NextTab`), before and after. · E2E: no (rendering time)
- [ ] **T538-11** **Owner:** on the real PC, switch onto "Datenquellen" from "Controller" and "Über Arcadia" and judge the
      delay; read the new texts (tabs, subtitles, "Jetzt suchen", the info boxes). · E2E: no (owner's hardware and wording)

Automated tests:

| Test cases | Tests |
|---|---|
| T538-1 | `Arcadia.App.Tests/DataSourcesTests.The_category_shows_its_groups_in_two_tabs_and_without_a_key_greys_out_what_needs_one`; every other data-source test reaches its row through RT (`SettingsSteps.FocusRow`) |
| T538-2 | `Arcadia.App.Tests/WaitingListMetadataTests.Ersten_nehmen_of_metadaten_takes_the_first_igdb_hit_of_every_waiting_game_and_leaves_the_artwork_list`; `Arcadia.Core.Tests/SettingsServiceTests.Metadata_keeps_the_shared_choice_for_several_hits_from_before_until_it_gets_its_own` |
| T538-3 | `Arcadia.App.Tests/WaitingListMetadataTests.A_game_with_several_igdb_hits_waits_on_the_metadata_list_apart_from_its_artwork_entry_and_keeps_its_launcher_data` |
| T538-4 | `Arcadia.App.Tests/MetadataSettingsTests.Jetzt_suchen_asks_at_once_for_what_is_missing_without_a_scan` |
| T538-5 | `Arcadia.App.Tests/MetadataSettingsTests.Launcher_daten_has_a_box_per_launcher_with_what_it_delivers_and_no_row_of_its_own` |
| T538-6 | `Arcadia.App.Tests/DataSourcesTests.Key_entry_paste_and_the_connection_test_work_with_the_controller_and_B_goes_back`, `IgdbSettingsTests.Client_id_and_secret_typed_by_controller_are_checked_once_both_are_there` |
| T538-7 | `Arcadia.Core.Tests/StatusChipIconTests.Every_warning_of_the_status_bar_has_a_symbol_in_the_header_chip` |
| T538-8 | `Arcadia.Core.Tests/Artwork/ArtworkServiceByNameTests.Demo_games_are_never_searched_never_wait_and_count_neither_as_missing_nor_searched`, `Metadata/Igdb/MetadataServiceIgdbTests.Demo_games_are_never_asked_never_wait_and_show_no_igdb_data_cached_before` |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T538-1 to -8 | 2026-11-13 | implementer | Linux cloud container, Release, `tools/ci/run-tests.sh 4` | pass (757 of 757 App methods, Core, Pan) | T538-8's IGDB case was red first: data cached before a library became demo data still showed; the run now merges such games again |
| T538-9 | 2026-11-13 | implementer | Linux/Xvfb, Debug, `--demo`, keys | pass | Both tabs with icons and the chosen one lit; warning on "Artwork" and "Datenquellen" with a rejected key; yellow tinted "Schlüssel ungültig · …" without the key's end; "Launcher-Daten" boxes with logos (GOG Galaxy, Ubisoft Connect, …); "Fehlende Metadaten" greyed with demo data only. Green and grey tints and the header's IGDB symbol only by test |
| T538-10 | 2026-11-13 | implementer | Linux/Xvfb, Debug, `--demo`, 3 s between presses, `Bild: NextTab` | pass | Onto "Datenquellen": before 282 / 254 ms, after 212 / 142 ms (Launcher, Über Arcadia 85–158 ms; Controller 531–564 ms, untouched). Only the shown tab's rows are built now |
