# #566 Remove the setting "Zuschneiden erlaubt"; covers may be cropped by up to 15 %

**Status:** Automated: T566-1, T566-2, T566-3 · QE: – · Open for the owner: –

- [x] **T566-1** Datenquellen shows no row "Zuschneiden erlaubt"; the group "ARTWORK" still works with the gamepad. · E2E: yes
- [x] **T566-2** A launcher cover cut by 10 % is shown ahead of SteamGridDB; one cut by 16 % gives way to SteamGridDB's
  2 : 3 cover. · E2E: yes
- [x] **T566-3** An old `settings.json` with `"Crop": "Much"` starts normally, covers follow the 15 % rule, and saving
  drops the key. · E2E: yes

Automated tests:

| Test cases | Tests |
|---|---|
| T566-1 | `DataSourcesTests.The_category_shows_its_groups_in_two_tabs_and_without_a_key_greys_out_what_needs_one` |
| T566-2 | `ArtworkServiceSteamGridDbTests.A_launcher_cover_fits_while_the_crop_cuts_away_at_most_fifteen_percent` (10 %, Epic's 3 : 4, 14.8 %, 15.8 %, 20 %), `ArtworkServiceSteamGridDbTests.A_background_that_cuts_more_than_fifteen_percent_gives_way_to_steamgriddbs`, `DataSourcesTests.A_wide_background_gives_way_to_steamgriddbs_hero_with_a_key` (Core: `ArtworkFormatTests`) |
| T566-3 | `DataSourcesTests.An_old_crop_value_in_settings_json_is_ignored_and_dropped_at_the_next_save`, `SettingsServiceTests.An_old_crop_value_is_ignored_and_dropped_at_the_next_save` (Core) |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T566-1 to T566-3 | 2026-11-13 | Claude | Debug, Linux cloud container, `dotnet test` | ✅ | – |
| T566-2, T566-3 | 2026-11-13 | Claude | Debug, Linux cloud container, tolerance set to 30 % on purpose | ❌ as intended (red once) | reverted to 15 % |
| T566-1 | 2026-11-13 | Claude | Debug, Linux/Xvfb, `--demo`, keys | ✅ "ARTWORK": "Von Hand gesetzte Artworks überschreiben" is followed by "Bei mehreren Treffern"; the info "REIHENFOLGE" names the 15 % rule | – |
