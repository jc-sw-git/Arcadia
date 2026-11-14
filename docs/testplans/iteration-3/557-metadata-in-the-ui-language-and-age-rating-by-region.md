# #557 Metadata in the UI language and age rating by region

Part 5 of #14; scenarios 17 to 19 there. The cases were derived from the specification before any code; the quality
engineer checks them again in the review.

**Status:** Automated: T557-1 to T557-7 · QE: T557-8 · Open for the owner: T557-9, T557-10

"UI language" is the language Arcadia runs in ("Sprache", applied after the restart). "Region" is the system's region:
on Windows "Land oder Region" (home location), elsewhere the regional format's. Français and Español join the UI
languages with their texts in #555/#556; the metadata side already speaks them.

- [x] **T557-1** Steam's store page is asked in the UI language: German `l=german`, English `l=english`, French
  `l=french`, Spanish `l=spanish`, any other (e.g. Italian) `l=english`; the address still holds nothing but the AppID
  and the language. · E2E: yes
- [x] **T557-2** Steam's tags (from `appinfo.vdf`) are named in the UI language from Steam's tag list in that language
  (German "Abenteuer", English "Adventure", French "Aventure", Spanish "Aventura"; any other English), at every scan, so
  they follow a new UI language at once. Every list names the same tags. · E2E: yes
- [x] **T557-3** Language rule for descriptions and short descriptions, for Steam, Epic and IGDB: the first provider's
  text in the UI language, else one in English (IGDB's are English), whatever the order; a text in neither (French in a
  German UI, Italian, Japanese) is left out, the detail page then shows no description, never another language. A text
  with a `?` inside a word counts only when no whole one fits. · E2E: yes
- [x] **T557-4** Cached texts stay: German texts loaded before, Arcadia restarted in English: the German description
  still shows and the store isn't asked again. "Alle Metadaten neu laden" asks Steam in English and the English
  description replaces the German one; after the next start it still shows. · E2E: yes
- [x] **T557-5** Genres are kept by key and named in the UI language ("Rollenspiel" / "Role-playing"), on the detail
  page's info line, in "Infos" and in the info box of a metadata hit; cached genres need no reload. · E2E: yes
- [x] **T557-6** The first age rating badge is the region's board, with all three delivered: USK 16 in Germany, PEGI 18
  in France, ESRB M in the USA; "Infos" and the info box of a metadata hit list the region's first, then USK, PEGI,
  ESRB. A game without the region's rating shows the next one in that order. · E2E: yes
- [x] **T557-7** Region → first board: Germany and Austria USK, USA and Canada ESRB, every other region (Switzerland,
  France, UK, Japan) and an unknown one PEGI. · E2E: yes
- [ ] **T557-8** Arcadia in English with `--demo`: a demo game's detail page shows genres, tags and description in
  English, the badge and "Infos" in the region's order; nothing cut off. · E2E: no (window)
- [ ] **T557-9** **Owner:** On your PC (German Windows, Steam library): "Sprache" English, "Sprache anwenden". The
  descriptions on the detail pages stay German; the Steam tags are English after the restart. "Datenquellen" →
  "Alle Metadaten neu laden": the descriptions turn English where Steam or IGDB has them. Back to "Deutsch": the
  same the other way round. · E2E: no (real Steam store, real Windows)
- [ ] **T557-10** **Owner:** Windows settings → "Zeit und Sprache" → "Region" → "Land oder Region" to "Vereinigte
  Staaten", restart Arcadia: a game with USK, PEGI and ESRB shows "ESRB" as its badge; "Frankreich" gives PEGI,
  "Österreich" and "Deutschland" USK. Set it back afterwards. · E2E: no (real Windows setting)

Automated tests:

| Test cases | Tests |
|---|---|
| T557-1 | `SteamAppDetailsTests` (Core) |
| T557-2 | `MetadataLanguageTests`, `SteamAppMetadataTests` (Core) |
| T557-3 | `MetadataLanguageTests` (App); the variants in `MetadataLanguageTests` (Core) |
| T557-4 | `MetadataLanguageTests` (App); `MetadataServiceTests`, `MetadataLanguageTests` (Core) |
| T557-5 | `MetadataLanguageTests` (Core); the names per language in `TranslationsTests` |
| T557-6 | `MetadataLanguageTests` (App), `WaitingHitFactsTests` (App); `MetadataLanguageTests`, `MetadataServiceTests` (Core) |
| T557-7 | `MetadataLanguageTests` (Core) |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T557-1 to T557-7 | 2026-11-14 | Claude | branch build, Linux container, `dotnet test` | ✅ | – |
