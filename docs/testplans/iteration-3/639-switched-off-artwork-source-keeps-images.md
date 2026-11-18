# #639 Switching an artwork source off deletes images no other source has

- [x] **T639-1** E2E: no (Core test) A source switched off keeps its image on disk and in the cache record (`OffCover`) also when no other source has one; the game shows its placeholder; switching the source on shows the image again at once, without a download.

No manual test needed: the case is covered by `ArtworkSearchTriggerTests` (Core), checking file and record for both steps.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T639-1 red on the old code (file deleted, record empty), green with the fix | 2026-11-18 | Claude | Debug, Linux cloud container, .NET 10; made-up game and image | ✅ | fixed in `ArtworkService.UpdateGameAsync` |
| `dotnet test` Core, artwork tests: 319 passed, 1 skipped | 2026-11-18 | Claude | same | ✅ | – |
