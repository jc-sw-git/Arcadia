# #507 One Steam image that doesn't answer keeps all Epic covers away

**Status:** Automated: T507-1, T507-2 · QE: none (no app run possible without the owner's network and catalog) · Open for the owner: T507-3

- [x] **T507-1** A Steam game's cover is missing on the server (404) and its background hangs; an Epic game after it has a catalog cover: the Epic cover is shown, the log names the hanging host. · E2E: no (unit test with fake server and fake time)
- [x] **T507-2** A host that can't be reached isn't asked again for the next game in the run, another host is; the next search asks the failed host again (a scan goes online for new games only, #506). · E2E: no (unit test)
- [ ] **T507-3** **Owner:** own PC, start Arcadia and wait for the scan: Epic games show their covers; the log's `Artwork:` summary names the host that didn't answer, if any. · E2E: no (real catalog, real network)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T507-1 | 2026-11-12 | Claude | `ArtworkServiceTests`, `ArtworkServiceSteamGridDbTests` (SteamGridDB image after an unreachable launcher host) (Core), Linux cloud container, fake server and time | ❌ before the fix (no Epic cover), ✅ after | – |
| T507-2 | 2026-11-12 | Claude | `ArtworkServiceTests` (Core), Linux cloud container, fake server; the next scan asking again: `Without_network_nothing_is_shown_and_the_next_scan_asks_again` | ❌ before the fix, ✅ after | – |
| T507-3 | 2026-11-12 | owner | main after #505, real PC (before the fix) | ❌ no Epic game had an image | this issue |
