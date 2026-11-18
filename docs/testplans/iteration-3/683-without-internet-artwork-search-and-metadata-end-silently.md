# #683 Without internet, artwork search and metadata loading end silently without a result

**Status:** Automated: T683-1 to T683-9 · QE: T683-10 · Open for the owner: T683-11

- [x] **T683-1** The connection check: no network adapter is offline at once and asks no host; any HTTP answer of a host is online; only the host's root is asked (`HEAD /`, no path, query or key); no answer within 3 s is offline; at most 4 hosts; nothing to ask is online · E2E: no (unit, the check itself)
- [x] **T683-2** The check before a search names only the hosts the work would ask: SteamGridDB with a usable key and one image address per launcher (tab "Artwork"); one store page per launcher and IGDB with an access (tab "Metadaten") · E2E: no (unit, the addresses)
- [x] **T683-3** Without internet a run asks no host after the first one failed: one failed request, one check, no further timeouts; with internet a failed host leaves the other hosts asked · E2E: no (unit, the run's requests)
- [x] **T683-4** A background run that lost the internet says nothing; "Jetzt suchen" and "Alle Artworks neu laden" say so once each · E2E: no (unit, the events)
- [x] **T683-5** Metadata without internet: IGDB isn't asked after the store failed; only "Jetzt suchen" and "Alle Metadaten neu laden" report it, the store's own warning doesn't come; the store alone down while the internet is there still asks IGDB · E2E: no (unit, the run's requests)
- [x] **T683-6** Datenquellen → Artwork, no internet: A on "Jetzt suchen", and A on "Alle Artworks neu laden" and A on the question: each time at once the warning toast "Keine Internetverbindung. Prüfe die Verbindung und versuche es dann noch einmal.", the row reads its action again, no progress, SteamGridDB and the image hosts get no request. With internet again the same press searches · E2E: yes
- [x] **T683-7** Datenquellen → Artwork: the internet goes during "Jetzt suchen": the search ends with the same warning, not "Keine Verbindung zu SteamGridDB …" · E2E: yes
- [x] **T683-8** Datenquellen → Metadaten, no internet: A on "Jetzt suchen", and A on "Alle Metadaten neu laden" and A on the question: the same warning at once, no store request, no reload running · E2E: yes
- [x] **T683-9** Datenquellen → Metadaten: the internet goes during "Alle Metadaten neu laden": it ends with the same warning, not "Metadaten nicht neu geladen …" · E2E: yes
- [x] **T683-10** The toast shows the warning symbol, reads in one go and stays its usual time; in "Artwork ändern" without internet the modal shows the images on this PC and the same toast once; an own term in "Warten auf deine Auswahl" shows the toast and "SteamGridDB nicht erreichbar" at once · E2E: no (window)
- [ ] **T683-11** **Owner:** real PC, Release, network off (flight mode or cable out): Datenquellen → "Jetzt suchen", "Alle Artworks neu laden", Metadaten → "Jetzt suchen", "Alle Metadaten neu laden": each warns within 3 s and starts nothing; then network on, start "Alle Artworks neu laden" and switch the network off during it: the run ends within about 15 s with the same warning · E2E: no (real network adapter)

Automated tests:

| Test cases | Tests |
|---|---|
| T683-1 | `ConnectionCheckTests` (Core) |
| T683-2 | `ArtworkSearchTriggerTests.The_check_before_a_search_names_steamgriddb_and_one_download_of_the_launcher`, `MetadataServiceIgdbTests.The_check_before_a_search_names_one_store_page_per_launcher_and_igdb` (Core) |
| T683-3, T683-4 | `ArtworkSearchTriggerTests.Without_internet_a_run_asks_no_host_after_the_first_failed_and_only_the_players_search_says_so`, `…With_internet_a_host_that_fails_leaves_the_other_hosts_asked` (Core) |
| T683-5 | `MetadataServiceIgdbTests.Without_internet_igdb_is_not_asked_after_the_store_failed_and_only_the_players_run_says_so` (Core) |
| T683-6, T683-7 | `DataSourcesTests` (`DataSourcesTests.Offline683.cs`) |
| T683-8, T683-9 | `MetadataSettingsTests` |
| T683-10 (without a window: the offer and the toast's text and level) | `ChangeArtworkTests.Without_internet_the_modal_offers_the_images_on_this_pc_and_warns_once`, `WaitingListLevelsTests.Without_internet_an_own_term_warns_and_says_steamgriddb_nicht_erreichbar_at_once` |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T683-3, T683-5 | 2026-11-18 | Claude | PR branch without the run's offline stop / with it, Debug, Linux | ❌ / ✅ | the checks go red on the bug |
| T683-1 to T683-9 | 2026-11-18 | Claude | PR branch, Debug, Linux | ✅ | – |
| T683-10 (tests above) | 2026-11-18 | Claude (QE) | PR branch with the check in "Artwork ändern" and before the own term removed / with it, Debug, Linux | ❌ / ✅ | the checks go red on the bug |
| T683-10 | 2026-11-18 | Claude (QE) | PR branch, Debug, Linux, Xvfb, `--demo`, no network (own network namespace) | ❌ | "Jetzt suchen" and "Alle Artworks neu laden" warn at once and start nothing, but the toast shows no warning symbol (no toast has one); "Artwork ändern", the waiting list and the tab "Metadaten" can't be reached with demo games (greyed), checked by the tests above |
| QE findings 1–3 | 2026-11-18 | Claude | PR branch, Debug, Linux, Xvfb, `--demo`: an Info toast (demo install) shows the blue i in front of its text; `ToastTests`, `DataSourcesTests` (shared check), all Core tests | ✅ | the toast symbol for Warning/Error is the same template, left to the QE's re-run |
| T683-10 | 2026-11-18 | Claude (QE) | PR branch after the fixes, Debug, Linux, Xvfb, `--demo`, no network (own network namespace) | ✅ | "Jetzt suchen" warns at once with the orange warning triangle in front of the text, in one line; findings 1–3 fixed |
