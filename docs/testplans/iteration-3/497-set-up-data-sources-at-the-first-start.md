# #497 Set up data sources at the first start

Derived from the specification in #43 (scenario 11) and #497's scope before any code. Automated tests use made-up games,
keys and access values and the fake SteamGridDB (`FakeSteamGridDb`), Twitch and IGDB (`FakeIgdb`), which record every
request; nothing reaches the real ones. Window cases run in the quality engineer's own pass (no window tests per feature
since #517; the step is in #518's page list as "Erster Start › Datenquellen"). The owner's cases of #43 are collected by
the closing thread of #43; T497-15 is a release candidate test.

The step is the settings page with the one category "Datenquellen" (header "Einrichtung", no ✕), so its rows, info
panel, on-screen keyboard and browser pages are the settings' own. T497-12 says greyed-out rows are passed over; the
step does what the settings' "Datenquellen" do (greyed rows keep the focus, T494-1, `docs/product/controls.md` says
"passed over"): the contradiction is for the owner to decide, the step follows the settings.

**Status:** Automated: T497-1 to T497-12 · QE: T497-13 · Open for the owner: T497-14 (T497-15 · RC on the release candidate)

- [x] **T497-1** Fresh first start (settings without `ActiveLaunchers`), controller only: "Fertig" on "Welche Launcher
  soll Arcadia nutzen?" leads to the step "Datenquellen" in place of the library, same frame as the launcher page (header
  with date and clock, pattern "Leiste"): the SteamGridDB rows of #489 and the IGDB rows of #494 with the same texts and
  info texts as in the settings, the box "WAS ARCADIA SENDET" (Steam gets the AppID, SteamGridDB and IGDB launcher ids or
  names, nothing else), the info texts naming Steam's and IGDB's limits, and a way to skip. · E2E: yes
- [x] **T497-2** Skipping by controller alone: one press ends the step without any key; the library shows with the
  launcher data (launcher covers or placeholders, Steam metadata from Steam alone); no request reaches SteamGridDB or
  IGDB, no warning, no status message, no toast about missing keys. After closing and starting Arcadia neither the
  launcher question nor the step comes again; "Datenquellen" in the settings shows "Kein Schlüssel hinterlegt" for both.
  · E2E: yes
- [x] **T497-3** SteamGridDB key in the step, by on-screen keyboard and by "Aus der Zwischenablage einfügen": saved and
  checked at once as in the settings ("Verbunden · endet auf …", the same toasts); after ending the step the settings
  show the same key and the artwork search for the library starts in the background. The key appears in no log line,
  toast or exception text, also from the first start. · E2E: yes
- [x] **T497-4** IGDB access (client id and secret) in the step, typed and pasted: saved and checked as in the settings
  (#494); after ending the step the metadata fetch from IGDB starts for the library's games; client id and secret are in
  no log line, toast or exception text. · E2E: yes (pasting: the rows of #494, T494-3)
- [x] **T497-5** The first fetch continues in the background: with a slow fake SteamGridDB and IGDB, ending the step
  shows the library at once (it waits for no request); covers and metadata arrive while the player moves through the
  library, the progress rows in "Datenquellen" count down, focus moves stay instant (nothing on the UI thread). A fetch
  begun while the step was open is not started twice. · E2E: yes
- [x] **T497-6** A key or access the service rejects, entered in the step: the row warns as in the settings ("Schlüssel
  ungültig · SteamGridDB wird übersprungen", the same for IGDB); the step can still be ended; afterwards the warning is
  at the category's tab, the row and the status chip, the rejected service gets no further request, launcher data and
  the other service keep working. · E2E: yes (variants for IGDB as unit tests)
- [x] **T497-7** "Verbindung testen" in the step with no network or no answer within 10 s: "Keine Verbindung", key or
  access kept, no warning, no status message; a second A during a test starts no second request; the step stays operable
  and can be ended while a test runs. · E2E: no (variants: view model test with a fake HTTP handler)
- [x] **T497-8** *(assumed)* B on the step goes back to the launcher page with the switches as left; B on the launcher
  page still does nothing. ≡ or Enter on the step end it like the skip; keys already entered stay. · E2E: yes
- [x] **T497-9** The first start continues where it stopped: Arcadia closed (cleanly and killed) on the launcher page
  after switching, or on the step "Datenquellen" after entering a key: the next start shows the same page with the
  switches and keys as left. The record is written to a temporary file and renamed, never half written. A record
  that is missing or unreadable: the first start begins again on the launcher page with the launchers found switched
  on, no crash, one log line; a key stored before shows as stored. Once the step has ended, a restart shows the
  library. · E2E: yes
- [x] **T497-10** *(assumed)* The step comes only with the launcher question: settings that hold `ActiveLaunchers`
  (also from a version before #497) show the library and no step; `--demo` skips both and saves nothing; `--first-run`
  shows both pages again, with a stored key shown as stored; `--demo --first-run` keeps the answer only until Arcadia
  closes and leaves no record to continue from (T139 rules). · E2E: yes
- [x] **T497-11** "Schlüssel-Seite öffnen" and "Zugang anlegen" from the step open SteamGridDB's API page and Twitch's
  developer console (HTTPS, fixed in code) in the default browser with the mouse mode on while the browser is in front
  (also with "Mausmodus: Aus"); back in Arcadia the mouse mode is off again and the step shows with the focus on the same
  row; a browser that fails to start gives the same warning toast as in the settings. · E2E: no (variants: view model
  test with a fake process starter; the real browser is T497-14)
- [x] **T497-12** The step's rows by controller: ▲ ▼ go round, greyed-out rows ("Schlüssel löschen", "Zugang löschen"
  without a key) are passed over, A opens the on-screen keyboard with an empty field, B in the keyboard keeps the previous
  value without a request; WASD and arrow keys do the same. · E2E: yes (greyed rows keep the focus as in the settings,
  see above)
- [ ] **T497-13** Window, QE's run: the step at 1920×1080 in Big Picture and in a small desktop window: rows, info panel
  and the skip button unclipped, focus ring whole, button hints match the step; a mouse click on a row, on "Einfügen" and
  on the skip button does what A does; every text German and per `docs/product/ui-texts.md`. · E2E: no (window)
- [ ] **T497-14** **Owner:** start the own Arcadia with `--first-run` and only the Steam Controller: walk the launcher
  page and the step "Datenquellen"; "Schlüssel-Seite öffnen", copy the SteamGridDB key by mouse, back, "Einfügen":
  "Verbunden"; "Zugang anlegen", copy client id and secret from Twitch's console, back, paste both: "Verbunden"; end the
  step: the library is usable at once, covers and descriptions arrive while you move, the mouse mode is off. · E2E: no
  (real browser, accounts and controller)
- [ ] **T497-15** **Owner:** release candidate installed on a PC (or Windows user) without Arcadia settings: the
  launcher question, then the step "Datenquellen"; skip it: the library shows Steam's metadata, Epic games only their
  developer, and `arcadia.log` shows no request to SteamGridDB or IGDB. · E2E: no (real installation) · RC

Automated tests:

| Test cases | Tests |
|---|---|
| T497-1 to T497-12 | `FirstRunDataSourcesTests` (acceptance, `ArcadiaDriver`, fake SteamGridDB and IGDB); the record in `FirstRunSetupTests` (Core); the launcher page in `FirstRunTests`; the step in the window guards' page list (`GuardPages`) |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T497-1 to T497-12 | 2026-11-12 | Claude | PR branch, Linux container, headless acceptance tests (no window) | ✅ | – |
| T497-1 to T497-12 | 2026-11-13 | Claude (QE) | PR branch 93ea8eb, Linux container, `tools/ci/run-tests.sh 4` (Release), 710 App tests; two mutations (no launcher limit on the step, no switch record) turn 3 tests red | ✅ | – |
| T497-13 | 2026-11-13 | Claude (QE) | PR branch 93ea8eb, Release, Xvfb 1920×1080 and window 1280×720, `--demo --first-run` and a fresh first start without `--demo`, keys and mouse | ❌ | Fresh step fine (rows, info panel, hints, texts, clicks on a row, "Einfügen", wrap to "Überspringen"). After a restart on the step the list doesn't follow the focus: "Überspringen" focused but out of sight (the window never follows a settings page set before it binds); back to the architect |
