# #585 IGDB descriptions don't follow the UI language

**Status:** Automated: T585-1, T585-2 · QE: T585-3 · Open for the owner: –

- [x] **T585-1** In each UI language (Deutsch, English, Français, Español) a description in that language comes before
  IGDB's English one, whatever the order of "Reihenfolge für Metadaten"; without one, IGDB's English one shows; an IGDB
  text in another language never shows. · E2E: yes
- [x] **T585-2** After a change of "Sprache", IGDB's English description replaces Steam's text in the old language at
  once, without a request; "Alle Metadaten neu laden" asks the store in the new language and its text comes first
  again. · E2E: yes
- [ ] **T585-3** The info of "Sprache" (Lokalisierung) and of IGDB in "Reihenfolge für Metadaten" say what T585-1 and
  T585-2 show, in all four languages, and fit their panel. · E2E: no (window)

Automated tests:

| Test cases | Tests |
|---|---|
| T585-1 | `MetadataServiceIgdbTests.A_description_in_the_ui_language_beats_igdbs_english_in_either_order`, `MetadataServiceIgdbTests.Without_one_in_the_ui_language_igdbs_english_description_shows`, `MetadataServiceIgdbTests.An_igdb_text_in_neither_the_ui_language_nor_english_is_never_shown` (Core; the merge alone: `MetadataLanguageTests`) |
| T585-2 | `MetadataServiceIgdbTests.After_a_change_of_the_language_igdbs_english_beats_a_stale_german_text_until_a_reload_brings_the_new_one` |
| T585-3 | `EveryUiTextTests` (every text in every language) |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T585-1, T585-2 | 2026-11-14 | Claude | Debug, Linux cloud container, `dotnet test` on `main` before any change | ✅ (the bug didn't reproduce: the code already follows the rule; the report came from the German IGDB info, which says "eine deutsche Beschreibung" because German is its UI language) | fix the "Sprache" info only |
| T585-1 | 2026-11-14 | Claude | Debug, Linux cloud container, `MergedMetadata` changed on purpose to prefer German | ❌ as intended (5 cases red) | reverted |
| T585-3 | 2026-11-14 | Claude | Debug, Linux/Xvfb, `--demo`, French, keys | ✅ "Langue": the new sentence fits the panel with room left | – |
