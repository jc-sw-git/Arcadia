# Architecture: the games' data in `Arcadia.Core`

The rest of Core: [core.md](core.md). Artwork and metadata are caches Arcadia may delete and fetch again; own data is
what the player set by hand and is never thrown away.

**Artwork** (`Artwork/`, #13, #487, #489, #491, #506)
- `ArtworkService`: after each scan one background run brings every game's cover and background into the cache: the
  launcher's images (`ILauncherArtwork`) in their order, the first that fits `ArtworkFormat` (2 : 3; background 16 : 9 or flat 3.1 : 1; 15 %
  cut away at most), else the first at all; downloads only for what is missing or what the launcher replaced (it no longer offers the cached image's address but
  another of the same origin, #488), 10 s each; an address that gave no image is not asked again in the same run;
  the run stops downloading at the first network failure; keeps the images of games that left a library, so they come back with them. `Of(key)` is
  what the views show (`GameArtwork`), `Changed` says for which games it changed. `ReloadAll` runs it over every image
  again, set by hand ones too only with "Von Hand gesetzte Artworks überschreiben". Order, switches (`Off`) and
  tolerance come from `ArtworkSettings`, followed per image during a run; `SettingsChanged` hides a switched-off
  source's images at once (`Of` filters) and queues a run that searches nothing. SteamGridDB is asked only where nothing
  cached fits: in a scan only for games new to the library (`KnownGames`, `artwork/known-games.json`; no list yet: all
  known), else by `SearchAgain` (player or first key) and `ReloadAll`; `CancelSearch` stops a run that searches.
  `Search`/`SearchChanged`: the missing counts and the progress of a run that searches (#506).
- `SteamGridDb/`: `SteamGridDbClient` (API v2, the key only in the `Authorization` header, 10 s, waits on 429, checks
  every answer), `SteamGridDbKey` (the key's shape, state None/Unchecked/Valid/Rejected, nothing sent without a
  usable key), `SteamGridDbKeyStore` (the key in a `SecretFile`) and
  `SteamGridDbSuggestions` (the client as `IArtworkSuggestions` for "Artwork ändern").
- `ArtworkService.ByHand.cs` ("Artwork ändern", #490): `ChoicesAsync` (image in use, own file, offers: the launcher's
  as a scan would choose it, then `IArtworkSuggestions`' up to 20, waiting in `artwork/.suggestions/`), `UseAsync`,
  `UseFileAsync` (own file, 20 MB), `UseOwnFileAsync`, `RestoreAsync`. All set by hand; one lock per game write with the run.
- `ArtworkService.ByName.cs` (#491): a game SteamGridDB doesn't know by id is searched by name (`SteamGridDbNames`:
  search term, comparison, `Decide`); `NameMatchStore` keeps per game key Chosen (a SteamGridDB game id), Skipped or
  Waiting (hits, position) in `artwork/name-matches.json`. `Waiting`/`WaitingChanged`, `ChooseAsync`, `Skip`,
  `TakeFirstForAllWaiting`, `HitCoverAsync` (a hit's cover for the waiting list, in `.suggestions/`).
- `ArtworkCache`: `artwork/<game>/cover|hero.*` plus `artwork.json` (source, origin, stamp, "set by hand"), the own
  files as `own-cover|own-hero.*`, the image of a source switched off renamed to `off-cover|off-hero.*` (one per kind)
  and renamed back when it is on again, every file through a temporary file and a rename; folder names from the key that never
  leave the cache; unreadable metadata makes the folder's images "set by hand". `ImageCheck`: a whole PNG, JPEG or WebP of at most 20 MB and 40 megapixels, and its size.

**Metadata** (`Metadata/`, #43, #493, #494): description, genres, developers, release date, age ratings and tags. The
rows in "Datenquellen": `app-artwork.md`; the product rules: `docs/product/settings-data-sources/metadata.md`.
- `MetadataService`: `Open` reads the cache; after each scan one background run reads the launchers' local data, then
  asks each store for the games without its answer, one at a time within its limit, 10 s each, waiting on 429; no
  network stops the run, a timeout or 5xx skips the game until the next scan. Then IGDB (`MetadataService.Igdb.cs`):
  every game without its answer, a Steam game by its AppID (`ILauncherMetadata.StoreIdOf`), every other one by its
  launcher title; a rejected access, no network or a broken answer leave IGDB out for the run. Several hits with
  "Selbst auswählen" wait (`MetadataService.Waiting.cs`: `Waiting`, `Choose`, `Skip`, `TakeFirstForAllWaiting`,
  `SearchByTermAsync`), the hits checked and kept as `CachedMetadata.IgdbByName` in the game's cache file; a choice is
  in memory at once and written in the background. `ReloadAll`,
  `FetchMissing` (first usable access), `Progress` (stores and IGDB), `Of(key)`, `TitleOf(key)` (the title replacing a
  doubtful launcher title), `SettingsChanged` (merges every game again from the cache, no request).
- Merge: the providers switched on in the order of `MetadataSettings` (`Settings/`), per field
  (`MergedMetadata.Of`, with each field's source); a launcher title with `?` is replaced by IGDB's regardless of the
  order (`MergedMetadata.Title`, `LauncherTitle`).
- `MetadataCache`: `metadata/<launcher>/<game>.json` (local, store and IGDB data per provider, unmerged, and a waiting
  or skipped search by name), temporary
  file and rename, damaged files deleted. `MetadataCheck` checks every value (also from the cache), `PlainText` turns
  HTML into text, `TextLanguage` the language rule.
- `Igdb/`: `IgdbAccess` (client id and secret, state None/Unchecked/Valid/Rejected, the token in memory, renewed once
  on 401/403; nothing sent without a usable access), `IgdbAccessStore` (both values in a `SecretFile`), `IgdbClient`
  (Twitch's token by POST, IGDB's API v4 with client id and token in headers, 4 requests a second for all callers, 10 s,
  waits on 429), `IgdbQuery` (the only place queries are built: AppID digits, IGDB id, a search term of letters,
  digits and a few marks), `IgdbAnswer` (genres by name onto the list, age ratings by organization and the category's text, `IgdbAnswer.Reading` marking cached answers to ask again,
  themes and keywords as tags, companies, first release date; `IgdbImages` the cover's image id),
  `IgdbMatching` (main games only, name equality with `?` as one character, several hits).
- `CacheFiles` (`Library/`): file names from game keys and writes through a temporary file, shared with `ArtworkCache`.
- `SecretFile` (`Settings/`): a player's secret (SteamGridDB key, IGDB access) encrypted through injected
  protect/unprotect, else memory only.

**Own data** (`UserData/`, #43, #496, #495)
- `UserDataStore`: what the player set per game key (`GameUserData`: own title) and the own tags (`OwnTag`: name and
  game keys, `TagsChanged`) in `%APPDATA%\Arcadia\user-data.json`, kept for games absent from the library. Read values
  are checked (`CheckedTags`: names of at most 40 characters without control characters, equal ignoring case merged). `Change` holds at once; the file is
  written in the background through a temporary file and a rename; a failed write puts the stored data back and raises
  `WriteFailed`. A file unreadable at the start (`IsUnreadable`) is copied to `user-data.json.broken`, never written,
  and nothing changes until Arcadia starts again.
- `GameTitles` (`Library/`): the shown title, own above the automatic one (`MetadataService.TitleOf`, #494, else the
  launcher's); `Shown` lays it over a launcher's game, `OwnTitleFrom` (trimmed; empty or the automatic title is none),
  `Change`, `Reset`, `Changed`. `LibrarySession` shows the libraries with it; providers and artwork keep the launcher's title.
- `OwnTags`: the tags over the store, alphabetically (`GameSorter.TitleComparer`): `Create`, `Set`, `Delete`, `Rename`
  (`TagRename.NeedsMerge` for another tag's name), `Merge`, `Of(gameKey)`; names through `NameFrom` (trimmed, ≤ 40).
