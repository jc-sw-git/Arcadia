# Architecture: metadata in Core

Description, genres, developers, release date, age ratings and tags of the games (#43). The rows in "Datenquellen":
`app-artwork.md`; the product rules: `docs/product/settings-data-sources/metadata.md`.

- `MetadataService`: `Open` reads the cache; after each scan one background run reads the launchers' local data, then
  asks each store for the games without its answer, one at a time within its limit, 10 s each, waiting on 429; no
  network stops the run, a timeout or 5xx skips the game until the next scan. Then IGDB (`MetadataService.Igdb.cs`):
  every game without its answer, a Steam game by its AppID (`ILauncherMetadata.StoreIdOf`), every other one by its
  launcher title; a rejected access, no network or a broken answer leave IGDB out for the run. `ReloadAll`,
  `FetchMissing` (first usable access), `Progress` (stores and IGDB), `Of(key)`, `TitleOf(key)` (the title replacing a
  doubtful launcher title), `SettingsChanged` (merges every game again from the cache, no request).
- Merge: the providers switched on in the order of `MetadataSettings` (`Settings/`), per field
  (`MergedMetadata.Of`, with each field's source); a launcher title with `?` is replaced by IGDB's regardless of the
  order (`MergedMetadata.Title`, `LauncherTitle`).
- `MetadataCache`: `metadata/<launcher>/<game>.json` (local, store and IGDB data per provider, unmerged), temporary
  file and rename, damaged files deleted. `MetadataCheck` checks every value (also from the cache), `PlainText` turns
  HTML into text, `TextLanguage` the language rule.
- `Igdb/`: `IgdbAccess` (client id and secret, state None/Unchecked/Valid/Rejected, the token in memory, renewed once
  on 401/403; nothing sent without a usable access), `IgdbAccessStore` (both values in a `SecretFile`), `IgdbClient`
  (Twitch's token by POST, IGDB's API v4 with client id and token in headers, 4 requests a second for all callers, 10 s,
  waits on 429), `IgdbQuery` (the only place queries are built: AppID digits, IGDB id, a search term of letters,
  digits and a few marks), `IgdbAnswer` (genres by name onto the list, age ratings by organization and category,
  themes and keywords as tags, companies, first release date; `IgdbImages` the cover's https address),
  `IgdbMatching` (main games only, name equality with `?` as one character, several hits).
- `CacheFiles` (`Library/`): file names from game keys and writes through a temporary file, shared with `ArtworkCache`.
