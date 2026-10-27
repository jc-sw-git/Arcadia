# #141 Software, tools and soundtracks in the library

Each launcher says which types it delivers (`Game.Type`: Spiel, Software, Werkzeug, Soundtrack) and which switches it
offers (`ILauncherIntegration.LibraryOptions`). Arcadia keeps what a scan delivered and filters each tab by the switches
(`LibraryTypes`), so a switch acts at once. Steam's types come from `appcache/appinfo.vdf` (`SteamAppInfo`), Epic's
engines and launchable add-ons from manifests and catalog. In `--demo` the demo launchers (GOG, Ubisoft, …) carry a
software, two tools and two soundtracks with all three switches, so every case except the real-launcher ones can be
tried without Steam or Epic.

- [ ] **T141-1** Epic "Software und Engines einbeziehen" on: Unreal Engine (one entry, however many versions) and Unreal
  Editor for Fortnite appear in the Epic tab with the wrench; off: they disappear at once, without a rescan · E2E: yes
- [ ] **T141-2** Unreal Engine with two versions installed: A ("Tüfteln") starts the newest · E2E: no (real Epic install)
- [ ] **T141-3** Steam: each of the three switches shows and hides only its type; demos are games; runtimes and
  redistributables (Proton, Steam Linux Runtime, Steamworks Common Redistributables) never appear · E2E: yes
- [ ] **T141-4** The type icon on tile (above the title), search hit, preview (line with name) and detail page (chip);
  none on games; on a tile it never overlaps the install, update, running or starting badge or the progress bar; a long
  three-line title still ends in an ellipsis · E2E: yes
- [ ] **T141-5** A says "Spielen" / "Tüfteln" / "Abrocken" by type on the preview and detail page button, in the context
  menu and in the button hint; not installed it says "Installieren" · E2E: yes
- [ ] **T141-6** A real Steam soundtrack: "Abrocken" opens it in Steam, which plays it; Arcadia shows a toast, no
  "Startet"/"Läuft"; its detail page notes "Steam spielt den Soundtrack ab." · E2E: no (real Steam)
- [ ] **T141-7** Count line beside the tab's name: parts in the order Spiele, Software, Werkzeuge, Soundtracks; a type
  without entries left out; the bracket left out when all of a type are installed; parts split by a divider; the tooltip
  spells it out; four-digit counts with a dot ("1.234"); no page number; an empty tab keeps its text; the tab's number
  counts all entries shown · E2E: yes
- [ ] **T141-8** Launcher page: "Neu einlesen" and "Launcher-Details" ("Einträge") show the same count line · E2E: yes
- [ ] **T141-9** The switches survive a restart; with all off the launcher's entry in `settings.json` holds no
  `LibraryOptions` · E2E: yes
- [ ] **T141-10** Real Steam with `appinfo.vdf` unreadable (renamed while Arcadia is closed): tools and soundtracks are
  still recognised by id and name, software counts as a game, no crash, one warning `Steam: appinfo.vdf nicht lesbar …`
  in arcadia.log; named back and rescanned, the types are right again. Also check whether every installed soundtrack has
  its own manifest and where its files are (`steamapps/music` or `common`) · E2E: no (real Steam)
- [ ] **T141-11** Start to usable library with all switches on, Release build, real Steam and Epic, and
  `--demo --demo-games 1000`: within 2 s; arcadia.log's line `Steam: Typen von … aus appinfo.vdf in … ms gelesen` gives the read time ·
  E2E: no (owner's PC)
- [ ] **T141-12** The whole flow (switch on, see the entry, open its detail page, start it, switch off) with the controller
  only, with the keyboard only and with the mouse only (the count line's tooltip with the mouse) · E2E: no

**Visual checklist for the quality engineer** (from the issue): the focus ring fully visible on the first, the last and
an edge tile (column 8) with a type icon and a state badge; the icon never on a state badge or the progress bar (not
installed, update, running, starting, installing; tool, software, soundtrack); search hit with launcher logo, type icon
and state badge at once; the count line with four types and four-digit counts beside "Ubisoft Connect" clear of the
view button; 200 % scaling sharp; preview line and chip never for games.

Unit and integration tests: `SteamAppInfoTests` (v27/v28/v29, broken files), `SteamScannerTests` (types from appinfo and
the fallback, runtimes left out, the music folder, a new install read again), `EpicCatalogTests`/`EpicScannerTests`
(engine and add-ons as tools, versions to one entry starting the newest), `LibraryTypesTests` (filter, counts, switches
saved), `LibrarySessionTests` (a switch acts at once without a rescan), `TypeCountLineTests` (parts, tooltip, texts per
type). Acceptance tests: `EntryTypeTests` (T141-1 with Epic's files, T141-3 with Steam's files and `appinfo.vdf`,
T141-5, T141-7, T141-8, T141-9) and `Window/EntryTypeViewTests` (T141-4 on cover, search hit, preview, detail page and the
focus ring; T141-7 as drawn beside "Ubisoft Connect").

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| Unit and integration tests above, all App and Core tests | 2026-10-27 | Claude | PR branch, Linux cloud container | ✅ | – |
| T141-4, T141-5, T141-7, T141-8 and the switches looked at | 2026-10-27 | Claude | PR branch, Debug, Linux, Xvfb, `--demo` with all GOG switches on, then tools off | ✅ count line, cover badge, preview line, detail chip, "Abrocken" and its toast in demo, search hits, launcher page, switch off acting at once | – |
| `SteamAppInfo` speed | 2026-10-27 | Claude | Linux cloud container, synthetic v29 file with 100,000 entries, 500 ids near its end | 36 ms (file cached) | T141-11 on the owner's PC |
| T141-2, T141-6, T141-10, real `appinfo.vdf` and Epic manifests | – | – | owner's PC was offline | not run | owner's test |
| Acceptance tests T141-1, 3, 4, 5, 7, 8, 9 (12 tests), each made red once by breaking the feature | 2026-10-27 | Claude (QE) | PR branch, Linux cloud container, Avalonia headless | ✅ | – |
| Visual check: launcher page switches, count line in header, "Neu einlesen" and "Launcher-Details", covers with type icon | 2026-10-27 | Claude (QE) | PR branch, Debug, Linux, Xvfb, `--demo`, GOG's three switches on | ✅ (gap before ")" in the bracket, see review) | 200 % scaling not looked at |
