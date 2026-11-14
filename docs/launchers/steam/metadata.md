# Steam — metadata (#43, #493)

Checked on the owner's PC for #43 (`Run-Check-43-v2/-v3`, 79 installed apps, 30 store pages).

## `appcache/appinfo.vdf` (local, at once, offline)

- `common/genres` (map index → Steam genre id) and `primary_genre`; `common/store_tags` (map index → tag id, ids only);
  `common/associations` (index → `type` developer/publisher, `name`), else `extended/developer|publisher`;
  `steam_release_date` (Unix seconds), else `original_release_date`. **No age ratings.** Values may be strings or ints.
- Steam's genre ids are the same in `appinfo.vdf` and on the store page, so `SteamGenres` maps by id (1 Action, 2
  Strategy, 3 RPG, 4 Casual, 9 Racing, 18 Sports, 23 Indie, 25 Adventure, 28 Simulation, 29 MMO); free to play, early
  access and the software genres have no place in Arcadia's list.
- Tag names come from Steam's public lists `https://store.steampowered.com/tagdata/populartags/<language>` (id → name,
  ~430 tags, the same ids in every language), one per UI language (`german`, `english`, `french`, `spanish`), saved as
  `steam-tags-<language>.json` and embedded; any other UI language reads `english`. A tag Steam added later has no
  name until the files are saved again. Arcadia sends no request for them. Tags come from the local file at every
  scan, so they follow a new UI language at once.

## Store page `appdetails` (no key)

- `https://store.steampowered.com/api/appdetails?l=<language>&appids=<AppID>`, no cookie, nothing but the AppID and
  the UI language (`german`, `english`, `french`, `spanish`; any other `english`).
- Answer `{"<AppID>":{"success":bool,"data":{…}}}`. `success:false`: Steam has no page (delisted, a tool) — cached
  as "nothing". An AppID Steam moved answers with the new app's data (`steam_appid` differs): treated as no answer.
- Used: `about_the_game` (else `detailed_description`; HTML with images, lists, links), `short_description`,
  `genres[].id`, `developers`, `publishers`, `ratings.usk|pegi|esrb|steam_germany.rating` (ESRB lower case: `m`).
  `steam_germany` is Steam's own rating for Germany, counted as USK only without a real one. `release_date.date` is
  a localised text ("24. Feb. 2022") and isn't used; `appinfo.vdf` has the date.
- A game without texts in the asked language answers in English (with `l=german` 3 of 30). Which ratings come depends on the
  country of the requesting IP: `steam_germany` comes from German addresses.
- Limit: about 200 requests per 5 minutes without a key, then HTTP 429. Arcadia sends 180 per 5 minutes, one at a time.
