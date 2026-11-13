# #495 Give games own tags and manage them in one place

Derived from #43 (scenario 7, the user-data failure cases), #495 and #500 before any code, by the quality engineer.
Automated tests use made-up games, ids and tag names; nothing reaches a launcher. "The tag window" is the window "Tags"
of one game; "the tag list" is "Eigene Tags" in "Datenquellen"; "the store" is `user-data.json`, shared with #496
(own titles). Window cases are `E2E: no (window)` (#517): the quality engineer checks them in the own run. Decisions D1
to D9 are in #495; the owner agreed to them.

Deviations from the cases, for the owner's look in #43's final test:
- T495-22: a failed write puts the stored tags back and says so (toast, log line), as #496's store does for titles;
  it does not keep the change in memory for the next write. One rule for the whole file: what the player sees is what
  is stored.
- T495-8: "Infos" lists the own tags under their own caption "EIGENE TAGS" above the providers' tags (as the design),
  not as a field with the source "Eigene".
- D6: the questions quote with „…“ (`docs/product/ui-texts.md`), not ‚…'.

**Status:** Automated: T495-1 to -11, -13 to -19, -21 to -27, -30 (guard on `main`) · QE: T495-12, -20, -28, -29, -31 · Open for the owner: T495-32, T495-33

**Tag window (detail page and context menu)**

- [x] **T495-1** "Verwalten" on the detail page has the row "Tags" with the value line "Keine" or "<n> Tags", for every
      game (installed or not, every launcher); its info box says what tags are for. A opens the tag window: head
      "Tags · <game>", "Neuer Tag" first, then every own tag with a tick where the game has it. · E2E: yes
- [x] **T495-2** The context menu of a game offers "Tags" (keyboard, mouse and ⧉ alike) and opens the same window for
      that game; B there returns to where it was opened (detail page: the row "Tags", library: the game focused). · E2E: yes
- [x] **T495-3** A on a tag ticks or unticks it for this game at once (no confirm step); the detail page behind it,
      the row's value line and the tag list's count follow without a rescan. · E2E: yes
- [x] **T495-4** A on "Neuer Tag" opens the on-screen keyboard (empty); ↵ creates the tag ticked for the game and
      sorted in (D3), B on the keyboard creates nothing. Empty, only spaces or an existing name (any case) behave as
      D1/D2. · E2E: yes; the name variants as unit tests
- [x] **T495-5** Y on a tag asks with D6's text naming how many games lose it; yes removes it from every game
      (absent ones included, D5) and from the window; no or B leaves everything. Y on "Neuer Tag" does nothing. · E2E: yes
- [x] **T495-6** Navigation: ▲ ▼ go round at the ends, held they repeat (`controls.md`); 200 tags scroll, the focused one
      stays visible. The window offers no renaming (#43 out of scope). · E2E: no (view model test)
- [x] **T495-7** Footer hints "A Zuordnen", "Y Löschen", "B Schließen" (texts in `*Texts.resx`); Y's hint greyed on
      "Neuer Tag". · E2E: no (view model test)

**Detail page**

- [x] **T495-8** A game with own tags "Couch-Koop" and "Zu zweit" and Steam tags "Indie", "Puzzle": "Übersicht" shows
      the own ones first, then the providers' ones, "+n" past the space (#43 design); "Infos" lists the own tags
      (caption "EIGENE TAGS", see above). Without any tag nothing is shown, no empty heading. · E2E: yes
- [x] **T495-9** "Tags" is an entry of "Übersicht anpassen" (#500): ticked, it shows in "Übersicht" with its value and
      A opens the tag window; with "Tags" five entries exist, so "5 von 5" shows. · E2E: yes
- [x] **T495-10** Own tags stay after a rescan, "Alle Metadaten neu laden", a change of the provider order and a
      restart; providers never add, change or remove own tags, and a provider tag with the same name as an own one
      is shown once, as the own one. · E2E: yes (acceptance test across a restart)
- [x] **T495-11** Tags are global across launchers: "Couch-Koop" on a Steam and an Epic game is one tag ("· 2 Spiele"),
      ticked in both windows; the same game in two launchers are two games. · E2E: yes
- [x] **T495-12** Look at 1920 × 1080: tag window and the row "Tags" as the design canvas (artboard of the detail
      page), long names (40 characters) cut with "…" not overlapping, colours from `Theme.axaml`, one focus ring. · E2E: no (window)

**Tag list in "Datenquellen"**

- [x] **T495-13** "Datenquellen" has the row "Eigene Tags (N)" (N = number of tags, 0 included) with an info text; A
      opens the tag list: "Neuer Tag" first, then every tag as "<Name> · <n> Spiele" ("1 Spiel", D4's "0 Spiele"). · E2E: yes
- [x] **T495-14** "Neuer Tag" in the tag list creates a tag without games, sorted in; N in the row follows. · E2E: yes
- [x] **T495-15** A on a tag shows its games (D3, D5); A on a game opens its detail page, B returns to the list of games
      (D8), B again to the tag list, B again to "Datenquellen" with the focus on "Eigene Tags". · E2E: yes
- [x] **T495-16** X on a tag opens the keyboard prefilled with its name; ↵ renames it on every game (absent ones too),
      sorted in again; B or an unchanged or empty name changes nothing. · E2E: yes
- [x] **T495-17** X renaming to an existing name in another case ("zu zweit" while "Zu Zweit" exists) asks D6's merge
      question; yes merges as D7 (a game with both tags has it once, the counts add up without doubles), no leaves
      both tags and every game as before. · E2E: yes; the merge variants as unit tests
- [x] **T495-18** Y deletes as in the tag window (T495-5), the list and N follow, focus on the next tag (on the last:
      the one before, none left: "Neuer Tag"). · E2E: yes
- [x] **T495-19** Hints in the tag list "A Spiele zeigen", "X Umbenennen", "Y Löschen", "B Zurück"; on "Neuer Tag" X and
      Y greyed. ▲ ▼ round at the ends, held repeat. · E2E: no (view model test)
- [x] **T495-20** Mouse and keyboard: the ✕ closes window and list; a click on a tag ticks it (window) or opens its games
      (list); a click on a hint does what its button does; keys as elsewhere (Space = A, Esc = B, F = X, V = Y). · E2E: no (window)

**Store and failures**

- [x] **T495-21** A game leaving the library keeps its tags; when it comes back (same game key) it has them again,
      also after a restart in between. A tag deleted meanwhile is gone on it too, a renamed one has the new name. · E2E: yes
- [x] **T495-22** The store is written as a temporary file, then renamed; Arcadia closed or killed during a write leaves
      the old or the new file, never half of one. Writing happens off the UI thread; a write error logs one line with
      stack trace and shows a toast (the change is put back, see above). · E2E: no (unit test)
- [x] **T495-23** No store file (fresh install): no tags, no message, the first change creates it. · E2E: no (unit test)
- [x] **T495-24** Unreadable store (broken JSON, wrong types, empty, locked): never overwritten, a copy is kept beside it,
      a status message (`settings.md`) names the file and what to do, one log line, Arcadia keeps running with D9;
      after repairing or deleting it, the next start reads it again and the message is gone. · E2E: yes
- [x] **T495-25** Values from the store are checked: names longer than 40 characters, with control characters, empty
      or doubled (case-insensitive) are dropped or merged as D1/D7 without dropping the rest; unknown game keys are kept
      (D5). Tag names and game keys never reach a path, URI or process; logs carry no tag names. · E2E: no (unit test)
- [x] **T495-26** A tag name with umlauts, ß, emoji, Japanese characters, quotes, `/`, `\`, `..`, `%` and `<b>`: shown as
      typed everywhere, stored and read back unchanged, no markup interpreted. · E2E: no (unit test)

**Controller and performance**

- [x] **T495-27** Controller only, B back: detail page → "Verwalten" → "Tags" → "Neuer Tag" (keyboard) → tick → Y on
      another tag, yes → B; then ≡ → "Datenquellen" → "Eigene Tags" → X rename into a merge, yes → A games → A detail
      page → B, B, B. No mouse or keyboard needed. · E2E: yes
- [x] **T495-28** Release build, made-up data: 5,000 games, 200 tags, 2,000 games with 1 to 10 tags: start to usable
      library still < 2 s, opening the tag window and the tag list and every focus move < 50 ms (`Bild:` lines), a tick
      never blocks the UI thread, RAM within budget. · E2E: no (Release timings; quality engineer's run)
- [x] **T495-29** Own run of the app with `--demo`: the controller path of T495-27 and T495-12, T495-20 looked at,
      the store file after a restart read back. · E2E: no (window; quality engineer's run)
- [x] **T495-30** The tag window and the tag list are in the shared page list the window guards walk (#518). · E2E: no (guard on `main`)
- [x] **T495-31** A store made unreadable by hand while Arcadia is closed: at the start the status message shows,
      "Tags" and "Eigene Tags" are greyed out with the note (D9), the copy lies beside the file. · E2E: no (window; quality engineer's run)

**Owner** (own PC, real library, Steam Controller; collected with the other #43 slices at the end)

- [ ] **T495-32** **Owner:** on the TV with the Steam Controller: give three own games the tag "Couch-Koop" from the
      detail page and one from the context menu, rename another tag into "couch-koop" and merge, open "Eigene Tags" →
      "Couch-Koop" → a game → B back; everything readable from the couch, typing on the keyboard comfortable. · E2E: no (own PC, TV, Steam Controller)
- [ ] **T495-33** **Owner:** after T495-32, close and start Arcadia, rescan and "Alle Metadaten neu laden": all own tags
      and their games are still there, own tags before Steam's on the detail page. · E2E: no (own library, real Steam metadata)

## Tests

| Test cases | Tests |
|---|---|
| T495-1, -3, -4, -7, -8 | `Arcadia.App.Tests/OwnTagsTests.A_new_tag_from_the_detail_page_is_ticked_for_the_game_and_shows_before_the_providers_tags` |
| T495-2 | `Arcadia.App.Tests/OwnTagsTests.The_context_menu_opens_the_same_window_and_B_returns_to_the_focused_game` |
| T495-5, -6 | `Arcadia.App.Tests/OwnTagsTests.Y_deletes_a_tag_from_every_game_after_asking_and_nothing_on_Neuer_Tag`, `…Two_hundred_tags_are_walked_round_and_a_held_button_stops_at_the_end` |
| T495-9 | `Arcadia.App.Tests/OwnTagsTests.Tags_is_a_quick_selection_entry_with_its_value_and_five_entries_exist` |
| T495-8, -10 | `Arcadia.App.Tests/OwnTagsTests.Own_tags_come_first_and_a_providers_tag_of_the_same_name_shows_once_as_the_own_one`, `…Own_tags_survive_a_rescan_a_restart_and_the_game_leaving_and_coming_back` |
| T495-11 | `Arcadia.App.Tests/OwnTagsTests.A_tag_is_one_across_launchers`, `Arcadia.Core.Tests/UserData/OwnTagsTests.A_tag_is_global_across_launchers_and_a_tag_without_games_stays` |
| T495-13, -14, -15, -19, -27 | `Arcadia.App.Tests/OwnTagsTests.The_tag_list_makes_tags_shows_their_games_and_leads_to_a_games_detail_page_and_back`, `…Neuer_Tag_in_the_list_with_an_existing_name_focuses_that_tag` |
| T495-16, -17, -27 | `Arcadia.App.Tests/OwnTagsTests.X_renames_and_another_tags_name_asks_to_merge`, Core `OwnTagsTests.Rename_…`, `…Renaming_…` |
| T495-18 | `Arcadia.App.Tests/OwnTagsTests.Y_in_the_list_deletes_and_the_focus_goes_to_the_next_the_one_before_or_Neuer_Tag` |
| T495-21 | `Arcadia.App.Tests/OwnTagsTests.Own_tags_survive_…`, Core `OwnTagsTests.A_game_leaving_the_library_keeps_its_tags_with_their_new_names` |
| T495-24, -31 (view models) | `Arcadia.App.Tests/OwnTagsTests.An_unreadable_file_greys_Tags_and_Eigene_Tags_with_the_note_and_changes_nothing`, Core `OwnTagsTests.An_unreadable_file_shows_no_tags_changes_nothing_and_stays_as_it_is` |
| T495-4 (variants), -22, -23, -25, -26 | `Arcadia.Core.Tests/UserData/OwnTagsTests` (names, failed write, broken-off write, no file, file values, characters, log lines) |
| T495-30 | `Arcadia.App.Tests/Guards/GuardPages` ("Tags eines Spiels", "Eigene Tags", "Eigene Tags › Spiele") |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| All automated tests (`dotnet test`, window guards included) | 2026-11-13 | implementer | Debug, Linux cloud container | passed after adapting the tests that counted "Verwalten"'s rows, the menu and the quick selection's defaults | — |
| App look: tag window from the menu, detail page ("Übersicht", "Verwalten"), "Eigene Tags (1)", tag list, a tag's games | 2026-11-13 | implementer | Debug, Linux cloud container (Xvfb, `--demo`, keys) | passed after one fix | "1 Tags" over the list → "1 Tag" |
| T495-12, T495-20, T495-29 | 2026-11-13 | QE (Claude) | PR #529, Release, Linux Xvfb 1920 × 1080, `--demo`, keys and mouse | ✅ tag window and list as designed, 40-character names cut with "…", one focus ring; ✕, clicks on rows and hints, Space/Esc/F/V; controller path of T495-27 with B, B, B; `user-data.json` read back after a restart (tags, merged spellings, a game outside the library kept); ❌ the rail line of "Verwalten" is cut ("… Deinstallie…") while the area is focused | Review finding 1 |
| T495-26 | 2026-11-13 | QE (Claude) | as above, tag written into `user-data.json` by hand | ✅ umlauts, ß, Japanese, emoji, quotes, `/`, `\`, `..`, `%`, `<b>` shown as typed; `couch-KOOP` merged into "Couch-Koop" | – |
| T495-28 | 2026-11-13 | QE (Claude) | PR #529, Release, Linux Xvfb (cloud container, slower than the owner's PC), `--demo --demo-games 5000`, 200 tags, 2,000 games with 1–10 tags | ✅ start to library 3.7 s with and without the tags (same as `main`'s baseline in this container, no cost of the tags), RAM 368 MB vs 364 MB; tag window open 40–47 ms warm (130 ms the first time), tag list 64 ms the first time, focus moves 1–5 ms, ticks 15–17 ms on the UI thread (writing in the background) | Start time to be judged on the owner's PC (container is above the budget for `main` too) |
| T495-30 | 2026-11-13 | QE (Claude) | PR #529, Release, Linux, window guards run locally (`Arcadia.App.Tests.Guards`) | ✅ 43 green, "Tags eines Spiels", "Eigene Tags", "Eigene Tags › Spiele" included | – |
| T495-31 | 2026-11-13 | QE (Claude) | as T495-12 | ✅ status message, "Tags" (context menu, detail page) and "Eigene Tags" greyed with the note, `user-data.json.broken` beside the file, file not overwritten; after repairing it the message is gone at the next start | Review finding 3 (count "(0)") |
