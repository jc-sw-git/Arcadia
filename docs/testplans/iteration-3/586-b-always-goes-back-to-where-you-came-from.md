# #586 B always goes back to the page you came from, also from a detail page opened in the search

**Status:** Automated: T586-1 to T586-6, T586-9, T586-10 · QE: T586-2, T586-7, T586-10 · Open for the owner: T586-8

- [x] **T586-1** Search "river" with the controller, hold ≡ on the second hit, B on its detail page: back in the search
  with "river", the same hits, the keyboard out, the focus on that hit. · E2E: yes
- [x] **T586-2** On a detail page opened from the search, RT turns to the next hit, then B: the search's focus is on
  the hit shown last. · E2E: yes
- [x] **T586-3** The keyboard put away with B, a hit's details with A held, B: back in the search with the keyboard
  still away and the same hit focused. · E2E: yes
- [x] **T586-4** A library detail page, X, a hit's details, B, B, B: the search, its keyboard away, then the library
  detail page on its game. · E2E: yes
- [x] **T586-5** X on a detail page opened from the search returns to the search as B does. · E2E: yes
- [x] **T586-6** A library detail page, ≡, "Datenquellen", "Eigene Tags", a tag, a game: its detail page; B until the
  settings close: the tag's games, the tags, "Datenquellen", then the library detail page from before, with the tabs.
  · E2E: yes
- [x] **T586-7** Mouse: the back button (side button) on a detail page from the search, in the search, in a dialog and
  in the settings acts as Esc there; while a text is written in the settings it ends the input like Esc. · E2E: no
  (window: a real mouse button)
- [ ] **T586-8** **Owner:** own library, controller: search a game, open its details (≡ held), turn with RT, B: back in
  the search on the game shown last, nothing lost; then the same with a real mouse's back button. · E2E: no (owner's PC,
  real mouse)
- [x] **T586-9** The status modal (Y), an entry's "Zu den Einstellungen", B: the modal again with its entries as they
  are now; B closes it. Settings opened with ≡ close onto the page alone. · E2E: yes
- [x] **T586-10** "Artwork ändern" without a key, "Zu den Datenquellen", B: "Artwork ändern" again for the same game,
  over its detail page. · E2E: yes

Automated tests:

| Test cases | Tests |
|---|---|
| T586-1, T586-2 | `BackWhereYouCameFromTests.B_on_a_detail_page_from_the_search_returns_to_the_search_on_the_game_shown_last` |
| T586-3 | `BackWhereYouCameFromTests.The_search_keeps_its_keyboard_put_away_when_B_returns_to_it` |
| T586-4 | `BackWhereYouCameFromTests.B_leads_from_a_hit_back_to_the_search_and_then_to_the_library_detail_page_it_was_opened_over` |
| T586-5 | `BackWhereYouCameFromTests.X_on_a_detail_page_from_the_search_also_returns_to_the_search` |
| T586-6 | `BackWhereYouCameFromTests.Settings_opened_over_a_detail_page_lead_back_to_it_after_a_game_of_the_tag_list` |
| T586-9 | `BackWhereYouCameFromTests.B_on_settings_opened_from_the_status_modal_opens_the_modal_again`, `StatusModalTests.Zu_den_Einstellungen_on_Pan_laeuft_nicht_opens_the_row_that_says_why_and_B_returns_to_the_modal_then_the_library` |
| T586-10 | `ChangeArtworkTests.Without_a_key_the_notice_follows_the_tiles_and_leads_to_the_settings_and_B_back` |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T586-1 to T586-6 | 2026-11-14 | Claude | Debug, Linux cloud container, `dotnet test` (App tests 1047/1047) | ✅ | – |
| T586-2, T586-7 | 2026-11-14 | Claude | Debug, Linux/Xvfb, `--demo`, keys and xdotool's mouse button 8: F, "a", → →, Enter held, E, Esc; Enter held, back button | ✅ Esc and the back button each return to the search with "a", 72 hits, the focus on the hit turned to (Crimson Signal, 4 of 72); log `Maus: Zurück-Taste → Back` | – |
| T586-1 to T586-6, T586-9, T586-10 | 2026-11-14 | Claude (QE) | Debug, Linux cloud container, acceptance tests run against `main`'s view models | ✅ red on `main` except T586-5 (X already returned to the search before; kept as a guard) | – |
| T586-2, T586-7, T586-10 | 2026-11-14 | Claude (QE) | Release, Linux/Xvfb, `--demo`, keys and xdotool's mouse button 8 | ✅ search "a": detail, 25 × E, Esc: the search on hit 29 on its result page; back button on the detail page, in the search, the game menu, the settings and on the key being typed acts as Esc there; "Change artwork", "To the data sources", Esc: the dialog again for the same game over its detail page, Esc: the detail page | – |
