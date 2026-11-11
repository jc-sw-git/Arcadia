# #488 Show Epic's covers and backgrounds from its catalog

**Status:** Automated: T488-1 to T488-15 · QE: app run with `--demo --demo-games 200` (Epic formats cropped, no distortion) · Open for the owner: T488-16, T488-17

Automated tests read a recreated `catcache.bin` (Base64 JSON) with made-up catalog ids, app names and image URLs and a
fake image server; nothing reaches Epic's real servers. "Shown" means at all four places: library tile, preview, detail
page, search hit. Tolerance is "Normal" (15 %) until #489. What #487 covers for every launcher (cache layout, temporary
file and rename, unreadable JSON, interrupted run, a game leaving the library, decoding at the size shown) is not
repeated; T488-12 checks that Epic games go through the same path. With a valid key the background is SteamGridDB's
fitting hero and the cover stays Epic's (#13, scenario 2): tested in #489.

**Reading the catalog**

- [x] **T488-1** An owned Epic game whose catalog entry has a `DieselGameBoxTall` (1200 × 1600) and a `DieselGameBox` (2560 × 1440), no key: after the scan the cover is the tall image cropped centred to 2 : 3 (cuts 11 %, fits at "Normal") and the background is the 16 : 9 image cropped centred to the hero format (no source fits, so the first with any image is used); both are shown, installed and not installed alike. · E2E: yes
- [x] **T488-2** An entry with only `DieselGameBoxTall`: cover shown, background placeholder. Only `DieselGameBox`: background shown cropped, cover placeholder (a 16 : 9 image is never used as cover). Neither, or no `keyImages` at all: both placeholders, no request, no log line. · E2E: no (variants: unit test of the Epic source)
- [x] **T488-3** Other `keyImages` types (`Thumbnail`, `DieselStoreFrontWide`, `OfferImageTall`, …) are ignored; with several entries of the same type the first usable one is taken. Type names are matched exactly as in the catalog. · E2E: no (unit test)
- [x] **T488-4** Broken entries: `keyImages` not an array, an entry without `type` or `url`, an empty or non-absolute URL, `http://`, `file://`, a host that is not one of Epic's image hosts, a URL with user info: that image counts as missing, no request, one log line for the game; the other image and every other game are read normally; the scan and the library are unaffected. · E2E: no (unit test)
- [x] **T488-5** Images come only from the catalog entry of the game itself: a DLC or add-on entry naming the game in `mainGameItem` does not lend it its images; a game without a catalog entry (installed, catalog not refreshed yet) keeps both placeholders. · E2E: no (unit test)
- [x] **T488-6** With the launcher's switch for tools on, an Unreal Engine entry whose catalog entry has images shows them like a game; without images it keeps the placeholder. · E2E: no (unit test)
- [x] **T488-7** `catcache.bin` missing, empty, not Base64 or not JSON: the Epic tab shows as today (installed games from the manifests), every Epic game keeps its placeholders, no toast, one log line; Steam's images are unaffected. · E2E: no (unit test)

**Download and failure cases**

- [x] **T488-8** An image is downloaded only once: after a scan and a restart without network both images are still shown and no request is sent; a second scan with the same catalog sends no request. The request carries no cookie, token or account data (only the URL from the catalog). · E2E: yes
- [x] **T488-9** Download fails (404, 500, no network, timeout after 10 s with fake time) or the answer is no image Arcadia can decode, larger than 20 MB or above the pixel limit (as T487-6): placeholder, one log line per game, no toast and no status message; the next scan asks again and shows the image when the server answers. · E2E: no (failure variants: unit tests with fake server and fake time)
- [x] **T488-10** No catalog value leads outside the cache: a manipulated app name or catalog id (e.g. `../x`, `a/b`, `con`) gives no path outside `%LOCALAPPDATA%\Arcadia\artwork\`; the image's file name in the cache comes from Arcadia, not from the URL. · E2E: no (unit test)
- [x] **T488-11** The catalog's URL for an image changes after Arcadia cached it (Epic swapped the art): the next scan downloads the new one and shows it; an image set by hand is not replaced (as T487-17). · E2E: no (unit test)
- [x] **T488-12** Epic images live in the same cache as Steam's (`artwork/<gameKey>/cover|hero.*` plus the JSON, source "launcher", "set by hand" false); installing an owned game, or uninstalling one, keeps its images and downloads nothing again. · E2E: no (unit test)

**Display and performance**

- [x] **T488-13** Placeholder until the image is there: an Epic tile, the preview, the detail page and a search hit show the placeholder and swap to the image when the download arrives, without the grid moving or the focus leaving the focused tile. · E2E: yes
- [x] **T488-14** A scan with 500 Epic games that all need a download, each answer taking 2 s: the library is usable at once, a focus move stays under 50 ms, the search opens as fast as without images, the downloads run in the background, throttled, never on the UI thread. · E2E: yes (speed test like T487-20)
- [x] **T488-15** Controller only, B back: Epic tab → page turns → detail page of an installed and an uninstalled game → back; search with X → Epic hit → detail page → B twice: every place shows the game's own images, none another game's. · E2E: yes

**Owner** (only he has a real Epic catalog with real images)

- [ ] **T488-16** **Owner:** own Epic library, controller: start Arcadia and wait for the scan, page through the Epic tab, open the detail page of an installed and an uninstalled game, search for two Epic games: real covers and backgrounds, cropped sensibly (no cut-off title in the middle of the cover), no visible pause when turning pages; games without catalog images keep the placeholder. `arcadia.log` has no line `Epic-Katalogbilder: Bild von Katalogeintrag … übergangen` (it would name an image host Arcadia doesn't know yet). · E2E: no (real catalog, real images, real timings)
- [ ] **T488-17** **Owner:** network off, close and start Arcadia: every Epic image from before is still there, no message about the network. · E2E: no (owner's PC, real network)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T488-2 to -7, -9 to -12 | 2026-11-11 | Claude | PR branch, Debug, Linux cloud container; made-up catalog, fake image server, fake time | ✅ `Core.Tests/Artwork/EpicArtworkTests`, `ArtworkServiceTests.An_image_from_a_second_host_still_offered_is_kept_without_asking_the_first_again` | T488-11 red before `ArtworkService` re-downloaded a replaced address (a fitting cached cover was kept) |
| T488-1, -8, -13 | 2026-11-11 | Claude | PR branch, Debug, Linux cloud container; recreated Epic folders (`Fixtures/EpicFolder`), fake image server | ✅ `App.Tests/EpicArtworkTests`; red once with the source reading no image type | – |
| T488-14, -15 | 2026-11-11 | Claude | PR branch, Debug, headless window, Linux cloud container; 500 and 30 made-up Epic games, one colour per game | ✅ `Window/EpicArtworkWindowTests`; red once with the source reading no image type. In -15 all games are installed: the "not installed" shade darkens the colour the check reads; installed and uninstalled alike in T488-1 | – |
| T488-1, -6 | 2026-11-11 | Claude (QE) | PR branch, Debug, Linux cloud container; probe test (not committed): two Epic entries (`UE_5.3`, `UE_5.4`) with the same `DieselGameBoxTall` URL, one scan | ❌ only the first gets the cover; the second is skipped because `ArtworkService` counts the URL as tried in this run | Fix in this PR (finding 1 in the Review) |
| – | 2026-11-11 | Claude (QE) | PR branch, Debug build, Linux cloud container, Xvfb 1920 × 1080, `--demo --demo-games 200`, keys | ✅ covers and preview background in Epic's formats fill their frames, cropped centred; only checked with demo gradients, not real art | – |
