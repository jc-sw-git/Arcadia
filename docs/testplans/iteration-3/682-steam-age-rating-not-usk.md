# #682 Steam's age rating is shown as "USK" but is not the real USK rating

Cases written by the implementing thread from the issue's acceptance criteria (no refinement round, at the owner's
request to finish it for rc.3); the quality engineer checks them in the review.

**Status:** Automated: T682-1, T682-2, T682-3 · QE: T682-4 · Open for the owner: T682-5

- [x] **T682-1** Steam's store answer with `usk`, `pegi`, `esrb` and `steam_germany`: USK, PEGI and ESRB are read,
  Steam's own `steam_germany` is not. · E2E: yes
- [x] **T682-2** A game whose Steam data has only Steam's own rating for Germany (16) besides a PEGI 12: the detail page
  shows no USK (badge and "Infos" show PEGI 12), the filter "Altersfreigabe" offers no USK for it. With a real USK 12
  from IGDB, USK 12 from IGDB shows. · E2E: yes
- [x] **T682-3** A metadata cache written by rc.2 that holds Steam's own rating as USK: after the update the game shows
  no USK from it, without a rescan and without asking Steam again. · E2E: yes
- [ ] **T682-4** Demo data on a German system: no game shows a USK that Steam's own rating would have given; games with
  a USK still show it in their colour, the filter "Altersfreigabe" still offers USK and PEGI. · E2E: no (window: drawn)
- [ ] **T682-5** **Owner:** After updating to rc.3, the game from the rc.2 finding (Steam said 16, the USK says 12):
  the detail page shows USK 12 from IGDB if IGDB has it, else no USK; it is no longer under USK 16 in the filter
  "Altersfreigabe". · E2E: no (real Steam and IGDB data) · RC

Automated tests:

| Test cases | Tests |
|---|---|
| T682-1 | `SteamAppDetailsTests` (Core) |
| T682-2 | `DetailMetadataTests`, `LibraryMetadataFilterTests`; the merge in `MetadataRulesTests` (Core) |
| T682-3 | `MetadataServiceTests` (Core) |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T682-1 to T682-3 | 2026-11-18 | Claude | branch, Linux, `dotnet test`; T682-2 and T682-3 red without the fix | ✅ | – |
