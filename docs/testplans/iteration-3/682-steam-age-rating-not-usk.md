# #682 Steam's age rating is shown as "USK" but is not the real USK rating

Cases written by the implementing thread from the issue and the owner's decision in its thread (Steam's own rating as
its own badge with the region, the real USK from another source), without a refinement round, to finish it for rc.3;
the quality engineer checks them in the review.

**Status:** Automated: T682-1, T682-2, T682-3, T682-4 · QE: T682-5 ✅ · Open for the owner: T682-6

- [x] **T682-1** Steam's store answer with `usk`, `pegi`, `esrb` and `steam_germany`: USK, PEGI and ESRB are read as
  delivered, `steam_germany` as Steam's own board "Steam DE", never as USK. · E2E: yes
- [x] **T682-2** A game where Steam has only its own rating for Germany (16) and IGDB the real USK 12: "Infos" shows
  USK 12 (source IGDB) and "Steam DE 16" (source Steam); the badge on a German PC is USK 12. Without a real USK from any
  source there is no USK at all; "Steam DE" comes after USK, PEGI and ESRB. · E2E: yes
- [x] **T682-3** The filter "Altersfreigabe" offers "Steam DE" as its own system beside USK/PEGI/ESRB with the USK's
  scale 0 … 18; picking "Steam DE" 16 finds only games with Steam's 16, the summary reads "Steam DE 16". · E2E: yes
- [x] **T682-4** A metadata cache written by rc.2 that holds Steam's own rating as USK: after the update it shows as
  "Steam DE", without a rescan and without asking Steam again. · E2E: yes
- [x] **T682-5** Demo data on a German system: a game with Steam's own rating shows "Steam DE 12" in the info line or
  "Infos" with the USK's green border, never "USK" for it; the filter "Altersfreigabe" shows the chip "Steam DE"; the
  longer name fits the badge and the chip. · E2E: no (window: drawn)
- [ ] **T682-6** **Owner:** After updating to rc.3, the game from the rc.2 finding (Steam said 16, the USK says 12):
  "Infos" shows USK 12 from IGDB (or from Steam if it has a real one) and "Steam DE 16" from Steam; the filter finds it
  under USK 12, and under "Steam DE" 16. · E2E: no (real Steam and IGDB data) · RC

Automated tests:

| Test cases | Tests |
|---|---|
| T682-1 | `SteamAppDetailsTests` (Core) |
| T682-2 | `DetailMetadataTests`; the merge and order in `MetadataRulesTests` (Core); Steam DE from Steam beside the real USK from IGDB in `MetadataServiceIgdbTests` (Core) |
| T682-3 | `LibraryMetadataFilterTests` |
| T682-4 | `MetadataServiceTests` (Core) |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T682-1 to T682-4 | 2026-11-18 | Claude | branch, Linux, `dotnet test` | ✅ | – |
| T682-1 to T682-4 | 2026-11-18 | Claude (QE) | branch, Linux, Release `dotnet test`, plus the IGDB case for T682-2 (red with Steam's value read as USK) | ✅ | – |
| T682-5 | 2026-11-18 | Claude (QE) | branch, Linux Xvfb, Release, `--demo`, region DE | ✅ "Infos" shows "Steam DE 12" with green border beside ESRB T, filter chip and tiles "Steam DE" fit (`captures-682/qe/`) | – |
