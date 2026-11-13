# #547 Settings info box keeps its scroll while data loads, metadata runs can be cancelled

Derived from the owner's findings in the hand test of #43 after #538, as written in #547. Keys follow
`docs/product/controls.md` (WASD, Space = A, Esc = B, Shift+W/S = RS).

**Status:** Automated: T547-1 to -4 · QE: T547-5 · Open for the owner: T547-6

- [x] **T547-1** ≡ → "Datenquellen" → "Metadaten", focus "Launcher-Daten" while "Fehlende Metadaten" loads and scroll its
      info box down with RS: it stays where it was scrolled while the run refreshes the rows (no jump to the top). The
      same holds for every choice row (artwork order, switches) during an artwork search or a scan. · E2E: yes
- [x] **T547-2** While a search goes (after a scan or "Jetzt suchen"), "Fehlende Metadaten" reads "Suche abbrechen"; A
      stops the run after the request in flight, what was loaded stays, the next "Jetzt suchen" asks for the rest. · E2E: yes
- [x] **T547-3** While "Alle Metadaten neu laden" runs, it reads "Neu laden abbrechen" and "Fehlende Metadaten" is
      greyed; A stops it, the games it didn't reach keep their data and nothing counts as missing. · E2E: yes
- [x] **T547-4** Both with the controller alone: after stopping, each row offers its action again ("Jetzt suchen",
      "Neu laden"); B leaves the settings as everywhere. · E2E: yes
- [ ] **T547-5** The look: "Suche abbrechen" and "Neu laden abbrechen" on their rows with the spinner, the info box
      scrolled down during a run (Linux/Xvfb). · E2E: no (window)
- [ ] **T547-6** **Owner:** on the real PC with your libraries, scroll the info box of "Launcher-Daten" while metadata
      loads, then stop the search and a reload. · E2E: no (owner's libraries and timing)

Automated tests:

| Test cases | Tests |
|---|---|
| T547-1 | `Arcadia.App.Tests/MetadataSettingsTests.While_a_run_goes_the_focused_rows_info_stays_where_it_was_scrolled` |
| T547-2 | `Arcadia.Core.Tests/Metadata/MetadataServiceTests.Cancelling_a_search_keeps_what_was_found_and_the_next_search_asks_for_the_rest` |
| T547-3 | `Arcadia.Core.Tests/Metadata/MetadataServiceTests.Cancelling_a_reload_keeps_the_old_data_of_the_games_it_didnt_reach_and_nothing_counts_as_missing` |
| T547-4 | `Arcadia.App.Tests/MetadataSettingsTests.Suche_abbrechen_and_neu_laden_abbrechen_stop_the_run_on_a_and_the_rows_offer_their_action_again`; `Alle_Metadaten_neu_laden_asks_first_…` (T493-26) checks "Neu laden abbrechen" while it runs |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T547-1 | 2026-11-13 | implementer | Linux cloud container, Debug, `dotnet test` | fail, then pass | Red first: "Launcher-Daten" announced its value 3 times during one run, each sending the info box to its top; `ChoiceRowViewModel.Refresh` now announces only a changed value |
| T547-1 to -4 | 2026-11-13 | implementer | Linux cloud container, Release, `tools/ci/run-tests.sh 4` | pass (761 of 761 App methods, Core, Pan) | — |
| T547-5 | 2026-11-13 | implementer | Linux/Xvfb `--demo` | not run | Demo games ask no store since #538, so no run goes there; left to QE with a made-up library or the owner |
