# Architecture: the games' data in `Arcadia.Core`

The rest of Core: [core.md](core.md). Artwork and metadata are caches Arcadia may delete and fetch again; own data is
what the player set by hand and is never thrown away.

**Artwork** (`Artwork/`, #13, #487, #489, #491, #506)
- `ArtworkService`: after each scan one background run brings every game's cover and background into the cache: the
  launcher's images (`ILauncherArtwork`) in their order, the first that fits `ArtworkFormat` (2 : 3, 3.1 : 1, 15 %
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
  usable key), `SteamGridDbKeyStore` (encrypted file through injected protect/unprotect, else memory only) and
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

**Metadata** (`Metadata/`, #43, #493)
- `MetadataService`: `Open` reads the cache; after each scan one background run reads the launchers' local data, then
  asks each store for the games without its answer, one at a time within its limit, 10 s each, waiting on 429; no
  network stops the run, a timeout or 5xx skips the game until the next scan. `ReloadAll`, `Progress`, `Of(key)`.
- `MetadataCache`: `metadata/<launcher>/<game>.json` (local and store data per provider, unmerged), temporary file and
  rename, damaged files deleted. `MetadataCheck` checks every value (also from the cache), `PlainText` turns HTML
  into text, `TextLanguage` the language rule, `MergedMetadata.Of` merges providers per field with their source.
- `CacheFiles` (`Library/`): file names from game keys and writes through a temporary file, shared with `ArtworkCache`.

**Own data** (`UserData/`, #43, #496)
- `UserDataStore`: what the player set per game key (`GameUserData`: own title; own tags come with #495) in
  `%APPDATA%\Arcadia\user-data.json`, kept for games absent from the library. `Change` holds at once; the file is
  written in the background through a temporary file and a rename; a failed write puts the stored data back and raises
  `WriteFailed`. A file unreadable at the start (`IsUnreadable`) is copied to `user-data.json.broken`, never written,
  and nothing changes until Arcadia starts again.
- `GameTitles` (`Library/`): the shown title, own above the automatic one (a provider's correction, #494, else the
  launcher's); `Shown` lays it over a launcher's game, `OwnTitleFrom` (trimmed; empty or the automatic title is none),
  `Change`, `Reset`, `Changed`. `LibrarySession` shows the libraries with it; providers and artwork keep the launcher's title.
