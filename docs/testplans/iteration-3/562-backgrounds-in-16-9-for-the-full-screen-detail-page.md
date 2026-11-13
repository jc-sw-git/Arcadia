# #562 Backgrounds in 16:9 for the full-screen detail page

**Status:** Automated: T562-1, T562-2, T562-3 · QE: T562-4 · Open for the owner: T562-5

- [x] **T562-1** A background in 16 : 9 (Epic's `DieselGameBox`) fits: it stays when a SteamGridDB key arrives, and
  SteamGridDB isn't asked for the game's background. · E2E: yes
- [x] **T562-2** A flat background (Steam's 1920 × 620) fits too: SteamGridDB isn't asked for it; of SteamGridDB's own
  backgrounds, a 16 : 9 one is taken before a better rated flat one. · E2E: yes
- [x] **T562-3** "Zuschneiden erlaubt" changes only covers: with "Viel" a background that cuts 22 % still doesn't fit, a
  cover that cuts 20 % does, at once and without a request. · E2E: yes
- [ ] **T562-4** "Artwork ändern", tab "Hintergrund": the heading reads "HINTERGRUND · 16 : 9", the tiles are 16 : 9 and
  show a flat image cropped as the detail page shows it; "Warten auf deine Auswahl" shows backgrounds in 16 : 9 tiles; the
  info boxes "Wer was liefert" and "Zuschneiden erlaubt" read as listed in the PR. · E2E: no (window: layout)
- [ ] **T562-5** **Owner:** on your PC, "Alle Artworks neu laden", then two Epic games and two Steam games on the detail
  page at the TV: the Epic ones show Epic's 16 : 9 background, not a blown-up flat strip; the Steam ones keep theirs.
  · E2E: no (real catalogs and SteamGridDB)

Automated tests:

| Test cases | Tests |
|---|---|
| T562-1 | `ArtworkServiceSteamGridDbTests.A_sixteen_by_nine_background_fits_and_steamgriddb_isnt_asked_for_one`, `EpicArtworkTests` (the 16 : 9 image fits) |
| T562-2 | `ArtworkServiceSteamGridDbTests.A_flat_background_fits_so_steamgriddb_isnt_asked_for_a_steam_game`, `…Of_steamgriddbs_backgrounds_a_sixteen_by_nine_one_beats_a_better_rated_flat_one`, `ArtworkFormatTests` |
| T562-3 | `ArtworkServiceSteamGridDbTests.Zuschneiden_erlaubt_leaves_backgrounds_at_fifteen_percent`, `DataSourcesTests.A_changed_crop_tolerance_picks_again_among_the_images_on_disk_at_once_without_a_request` |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T562-1 to -3 | 2026-11-13 | Claude | branch, Linux, headless tests | ✅ (T562-1, the 16 : 9 beats flat case of T562-2 and T562-3 red with `main`'s `ArtworkFormat`) | – |
| T562-4 | 2026-11-13 | Claude | branch, Linux/Xvfb, `--demo --demo-games 120`, keys | ✅ "Artwork ändern" → "Hintergrund": heading "16 : 9", 16 : 9 tiles, a 1920 × 620 image cropped in its tile; waiting list not seen (demo games get no hits) | QE looks at the info texts |
