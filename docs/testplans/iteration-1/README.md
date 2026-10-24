# Testplan Iteration 1

Meilenstein „Iteration 1“. Format und Regeln: [README](../README.md). Die Häkchen setzt erst der
Abschluss-Test am installierten Release-Kandidaten.

**Abschluss-Test:** Release-Kandidat 0.1.0-rc.1, getestet von Juri, am 2026-10-24 (#74).

Eine Datei pro Issue (bzw. für das Grundgerüst). Ein PR, der ein Issue schließt, legt dessen Datei an
und trägt sie hier ein; wer ein Issue testet, liest nur dessen Datei.

| Datei | Issue | Titel | Testfälle |
|---|---|---|---|
| [grundgeruest.md](grundgeruest.md) | – | Grundgerüst (ohne Issue) | 33 |
| [001-steam-bibliothek-einlesen.md](001-steam-bibliothek-einlesen.md) | #1 | Steam-Bibliothek einlesen | 9 |
| [008-ansicht-und-auswahl-speichern.md](008-ansicht-und-auswahl-speichern.md) | #8 | Ansicht und Auswahl speichern | 3 |
| [010-kontextmenue-fuer-spiele.md](010-kontextmenue-fuer-spiele.md) | #10 | Kontextmenü für Spiele | 9 |
| [011-spieldetails-als-karussell.md](011-spieldetails-als-karussell.md) | #11 | Spieldetails als Karussell | 10 |
| [012-suche-ueber-alle-launcher.md](012-suche-ueber-alle-launcher.md) | #12 | Suche über alle Launcher | 19 |
| [015-einstellungsseite-mit-kategorien-im-header.md](015-einstellungsseite-mit-kategorien-im-header.md) | #15 | Einstellungsseite mit Kategorien im Header | 10 |
| [023-installer-self-update-release-pipeline.md](023-installer-self-update-release-pipeline.md) | #23 | Installer, Self-Update, Release-Pipeline | 8 |
| [027-spielstart-robust-machen.md](027-spielstart-robust-machen.md) | #27 | Spielstart robust machen | 3 |
| [028-steam-controller-support-serialisieren.md](028-steam-controller-support-serialisieren.md) | #28 | Steam Controller Support serialisieren | 3 |
| [029-usbip-installer-vor-dem-start.md](029-usbip-installer-vor-dem-start.md) | #29 | usbip-Installer vor dem Start mit Adminrechten prüfen | 3 |
| [041-nicht-installierte-spiele-anzeigen-und.md](041-nicht-installierte-spiele-anzeigen-und.md) | #41 | Nicht installierte Spiele anzeigen und installieren | 10 |
| [042-ganze-epic-bibliothek-einlesen.md](042-ganze-epic-bibliothek-einlesen.md) | #42 | Ganze Epic-Bibliothek einlesen | 4 |
| [064-controller-als-maus-nutzen-mausmodus.md](064-controller-als-maus-nutzen-mausmodus.md) | #64 | Controller als Maus nutzen (Mausmodus per Back + Start) | 19 |
| [066-abschluss-review.md](066-abschluss-review.md) | #66 | Abschluss-Review Iteration 1 | 4 |
| [067-mainviewmodel-aufteilen.md](067-mainviewmodel-aufteilen.md) | #67 | MainViewModel aufteilen | 1 |
| [068-launcher-als-ein-buendel-registrieren.md](068-launcher-als-ein-buendel-registrieren.md) | #68 | Launcher als ein Bündel registrieren | 0 |
| [081-gleiche-funktionen-fuer-steam-und.md](081-gleiche-funktionen-fuer-steam-und.md) | #81 | Gleiche Funktionen für Steam und Epic | 6 |
| [085-steam-controller-support-erst-einschaltbar.md](085-steam-controller-support-erst-einschaltbar.md) | #85 | Steam Controller Support erst einschaltbar, wenn alles bereit ist | 20 |
| [086-refinement-skill.md](086-refinement-skill.md) | #86 | Refinement-Skill | 0 |
| [087-diagnose-skill.md](087-diagnose-skill.md) | #87 | Diagnose-Skill | 0 |
| [088-launcher-befunde-aus-claude-md.md](088-launcher-befunde-aus-claude-md.md) | #88 | Launcher-Befunde aus CLAUDE.md auslagern | 0 |
| [092-steam-controller-support-verstaendlich-benennen.md](092-steam-controller-support-verstaendlich-benennen.md) | #92 | „Steam Controller Support“ verständlich benennen und beschreiben | 5 |
| [094-nach-einem-aus-arcadia-gestarteten.md](094-nach-einem-aus-arcadia-gestarteten.md) | #94 | Nach einem aus Arcadia gestarteten Spiel kommt Arcadia wieder in den Vordergrund | 7 |
| [095-laufende-spiele-anzeigen-statt-spielen.md](095-laufende-spiele-anzeigen-statt-spielen.md) | #95 | Laufende Spiele anzeigen statt „Spielen“ anzubieten | 14 |
| [100-beenden-von-arcadia-bricht-einen.md](100-beenden-von-arcadia-bricht-einen.md) | #100 | Beenden von Arcadia bricht einen laufenden SISR-Start ab | 3 |
| [102-ausfaden-am-rand-des-listings.md](102-ausfaden-am-rand-des-listings.md) | #102 | Ausfaden am Rand des Listings | 4 |
| [103-laufende-spiele-ohne-exceptions-erkennen.md](103-laufende-spiele-ohne-exceptions-erkennen.md) | #103 | Laufende Spiele ohne Exceptions erkennen | 3 |
| [110-bildschirmtastatur-fuer-fremde-fenster-y.md](110-bildschirmtastatur-fuer-fremde-fenster-y.md) | #110 | Bildschirmtastatur für fremde Fenster (Y im Mausmodus) | 15 |
| [113-steam-tab-zeigt-spiele-die.md](113-steam-tab-zeigt-spiele-die.md) | #113 | Steam-Tab zeigt Spiele, die Steam nicht mehr listet | 1 |
| [115-ci-installer-seltener-hochladen.md](115-ci-installer-seltener-hochladen.md) | #115 | CI: Installer seltener hochladen | 0 |
| [124-sisr-uebernimmt-bei-steam-input.md](124-sisr-uebernimmt-bei-steam-input.md) | #124 | SISR übernimmt bei Steam Input für Xbox-Controller auch den Xbox Controller | 9 |
| [127-ui-polishing.md](127-ui-polishing.md) | #127 | UI Polishing | 10 |
| [129-bildschirmtastatur-als-nachbildung-echter-tastaturlayouts.md](129-bildschirmtastatur-als-nachbildung-echter-tastaturlayouts.md) | #129 | Bildschirmtastatur als Nachbildung echter Tastaturlayouts | 7 |
| [136-launcher-aktivieren-und-anordnen.md](136-launcher-aktivieren-und-anordnen.md) | #136 | Launcher aktivieren und anordnen | 13 |
| [137-launcher-bearbeiten-name-und-such.md](137-launcher-bearbeiten-name-und-such.md) | #137 | Launcher bearbeiten: Name und Such-Kürzel, eine Seite je Launcher | 8 |
| [138-launcher-logos-ordnerwahl-und-windows.md](138-launcher-logos-ordnerwahl-und-windows.md) | #138 | Launcher-Logos, Ordnerwahl und Windows-Dialoge mit dem Controller | 5 |
| [139-ersteinrichtung-launcher-waehlen.md](139-ersteinrichtung-launcher-waehlen.md) | #139 | Ersteinrichtung: Launcher wählen | 7 |
| [143-steam-input-fuer-ganze-controllertypen.md](143-steam-input-fuer-ganze-controllertypen.md) | #143 | Steam Input für ganze Controllertypen legt den Steam Controller lahm | 6 |
| [167-update-und-startanzeige-ueberlappen.md](167-update-und-startanzeige-ueberlappen.md) | #167 | Cover: Update- und Startanzeige überlappen, wenn der Launcher vor dem Start aktualisiert | 5 |
| [184-abnahmetests-view-model-ebene.md](184-abnahmetests-view-model-ebene.md) | #184 | Abnahmetests auf View-Model-Ebene für die Kernabläufe | 0 |
| [188-abnahmetests-suche-und-tastatur.md](188-abnahmetests-suche-und-tastatur.md) | #188 | Abnahmetests View-Model: Suche und Bildschirmtastatur | 0 |

## Explorativer Test

Auftrag: –. Zeitrahmen: –.

Funde am 0.1.0-rc.1 (Juri, 2026-10-24, bei Testfällen und nebenbei):

- Setup meldet „bereits installiert“, weil nur der Log-Ordner unter `%LOCALAPPDATA%\Arcadia` lag → PR #162 (behoben).
- Deinstallieren entfernt den Steam-Eintrag „SISR Marker“ nicht; Einstellungen und Logs bleiben ohne Nachfrage → PR #163.
- Installationsort nicht wählbar → #161 (Iteration 2).
- Statusmeldungen im Header sind mit dem Controller nicht erreichbar → #166.
- Spielstart, der im Original-Launcher erst ein Update auslöst: die Anzeigen „Startet“ und „Lädt“ überlappen → #167 (Iteration 2), Entwurf PR #168.
- Steams Flag `Running` kann hängen bleiben (T95-12), ein Spiel gälte dann dauerhaft als laufend → #182 (Iteration 2).

## Abnahmetests (E2E)

Wird beim Abschluss-Test aus allen Testfällen mit `E2E: ja` erstellt und in der Abnahme-Iteration
umgesetzt.

Automatisiert in `tests/Arcadia.App.Tests` (Abnahme-Iteration 1, #184, #188; Regeln im [README](../README.md)):

| Testfall | Test | Abdeckung |
|---|---|---|
| G-1 | `StartTests.Start_shows_a_tab_per_launcher_with_the_first_tab_selected_and_a_game_focused` | ganz |
| G-6 | `LibraryTests.The_D_pad_moves_the_focus_through_the_grid_and_stops_at_its_edges` | teilweise: ohne Wiederholung beim Halten, ohne Linker Stick, ohne Sichtbarkeit |
| G-7 | `LibraryTests.LB_and_RB_switch_the_launcher_tab_and_wrap_around` | teilweise: ohne Drehen und Ausblenden der Leiste |
| G-8 | `LibraryTests.LT_and_RT_turn_the_pages_and_the_page_bar_shows_the_current_page` | ganz |
| G-12, G-14 | `LibraryTests.View_options_change_the_page_size_at_once_and_offer_only_sizes_the_tab_can_fill` | ganz |
| G-13 | `LibraryTests.Each_sort_in_view_options_orders_the_tab_with_games_without_a_date_last` (alle sechs Sortierungen) | ganz |
| T8-1 | `LibraryTests.Sort_page_size_and_tab_are_the_same_after_a_restart_with_the_focus_on_page_one` | teilweise: Tab per LB/RB, nicht per Klick |
| T10-4 | `GameStartTests.A_tapped_starts_the_focused_game_through_its_launcher_on_the_release`, `…A_held_shows_the_frame_then_opens_the_detail_page…`, `…A_let_go_between_the_frame_and_the_details_does_nothing_also_later` | teilweise: Rahmen als Zustand, nicht wie er aussieht |
| T11-1 | `GameStartTests.A_held_shows_the_frame_then_opens_the_detail_page_after_a_second_while_still_held_and_starts_nothing` | teilweise: Halten und Position, ohne ⧉ → „Details anzeigen“, Pfadleiste, Nachbarn |
| T11-2 | `DetailPageTests.LT_and_RT_show_the_previous_and_next_game_across_page_borders_and_wrap_around` | teilweise: ohne Animation |
| T11-6 | `DetailPageTests.B_goes_back_to_the_library_on_the_page_of_the_game_shown_last_with_the_focus_on_it` | teilweise: B, nicht Klick auf die Pfadleiste |
| T12-2 | `OnScreenKeyboardTests.The_D_pad_walks_the_keys_to_the_nearest_one_and_stops_at_the_edges_A_types_Y_a_space_X_erases`, `…X_held_or_A_held_on_the_erase_key_erases_one_then_after_a_pause_one_after_another` | teilweise: ohne linken Stick und gehaltenes D-Pad (die Wiederholung kommt aus der Eingabe, nicht aus dem View Model) |
| T12-3 | `SearchTests.Hits_come_from_all_launchers_by_relevance_whatever_the_case_accents_and_trademark_signs`, `SearchTests.An_accent_or_trademark_sign_in_the_title_or_the_term_does_not_matter_and_no_hit_shows_the_empty_state`, `SearchTests.Trademark_registered_and_copyright_signs_count_for_nothing_and_each_hit_names_its_launcher` | teilweise: Launchername, nicht Logo und Grafik (Aussehen) |
| T12-7 | `SearchHitTests.Start_tapped_on_a_hit_starts_it_on_the_release_or_installs_it_held_opens_its_details_and_View_its_menu` | teilweise: Ring als Zustand; ohne Maus, Rechtsklick, Umschalt+F10 und Legende |
| T12-8 | `SearchHitTests.The_details_of_a_hit_turn_through_the_hits_in_their_order_without_tabs_and_LB_RB_and_Y_do_nothing` | teilweise: Pfadleiste und Footer nicht (Ansicht); B führt seit #127 in die Bibliothek, geprüft in T127-4 |
| T12-9 | `SearchTests.B_first_puts_the_keyboard_away_then_goes_back_to_the_library_as_it_was_without_erasing`, `SearchTests.B_twice_in_a_row_goes_straight_back_to_the_library_as_it_was` | ganz |
| T12-13 | `OnScreenKeyboardTests.LB_tapped_latches_shift_until_tapped_again_and_held_it_counts_only_while_held` | ganz |
| T12-15 | `SearchHitTests.L3_opens_filter_and_sort_whose_every_choice_counts_at_once_and_a_new_search_starts_by_relevance` | teilweise: L3, nicht Klick auf „Sortiert nach …“ oder die Taste „Filter“ |
| T110-11 | `OnScreenKeyboardTests.The_keyboards_arrows_move_the_caret_where_typing_and_erasing_then_act_also_from_a_real_keyboard` | teilweise: von der echten Tastatur nur Tippen, nicht ⌫ |
| T110-13 | `OnScreenKeyboardTests.With_AltGr_on_left_of_the_first_key_with_a_character_leads_to_shift_and_left_of_the_tall_enter_to_the_tilde` | teilweise: nur in der Suche; die Tastatur des Mausmodus (mit ↹) startet `ArcadiaDriver` nicht |
| T127-3 | `SearchHitTests.X_on_a_detail_page_of_the_library_opens_the_search_and_B_there_comes_back_to_the_same_page` | teilweise: X und B, nicht F, Esc und Klick auf den Such-Chip |
| T127-4 | `SearchHitTests.From_the_details_of_a_hit_X_returns_to_the_search_as_it_was_and_B_goes_to_the_game_in_its_launchers_listing` | teilweise: B, nicht Esc |
| T129-1 | `OnScreenKeyboardTests.X_opens_the_German_keyboard_with_the_real_rows_and_the_shift_and_AltGr_characters_small_on_the_keys` | teilweise: Reihen, Versatz und Zeichen, nicht Größe und Lage der Beschriftung |
| T129-2 | `OnScreenKeyboardTests.LB_tapped_latches_shift_until_tapped_again_and_held_it_counts_only_while_held` | ganz |
| T129-3 | `OnScreenKeyboardTests.RB_tapped_latches_AltGr_whose_characters_alone_show_and_type_and_LB_and_RB_switch_each_other_off` | ganz |
| T129-4 | `OnScreenKeyboardTests.With_AltGr_on_the_D_pad_passes_over_empty_keys_and_rows_and_without_it_up_and_down_come_back` | ganz (hoch von e geht auf 4, nicht wie im Beispiel auf 3; wieder runter auf e) |
| T129-5 | `OnScreenKeyboardTests.The_dead_keys_type_their_character_at_once` | ganz |
| T129-6 | `OnScreenKeyboardTests.R3_switches_to_US_UK_FR_and_ES_each_with_its_real_keys` | teilweise: Tasten stichprobenhaft; Klick auf das graue AltGr nicht |
| T15-1 | `SettingsTests.Menu_opens_the_settings_and_B_goes_back_to_the_same_tab_page_and_game` | teilweise: Controller, nicht Enter/Esc und Klick |
| G-31 | `SettingsTests.The_settings_are_worked_with_the_controller_alone_rows_categories_and_values` | teilweise: Scrollen ins Bild nicht |
| T139-1 | `FirstRunTests.A_fresh_install_asks_first_with_the_launchers_found_switched_on`, `FirstRunTests.A_launcher_not_found_cannot_be_switched_on` | teilweise: ohne Spielzahl und Texte |
| T139-3 | `FirstRunTests.Only_the_launchers_left_on_become_tabs_also_after_a_restart_and_the_question_does_not_come_again` | teilweise: „Fertig“ mit ≡, nicht Enter oder Klick |

„Teilweise“ heißt: Der Rest des Testfalls bleibt im Abschluss-Test. „Einmal rot“ zeigt, dass der Test eine
bestimmte Änderung erkennt, nicht dass er den Testfall ganz abdeckt.

Was sie nicht prüfen, bleibt im Abschluss-Test: Aussehen und Animationen, Maus und echte Tastatur
(Headless-Tests, #180), echte Launcher und Windows. Die Bildschirmtastatur bedienen die Tests seit #188 mit
D-Pad und A wie der Nutzer; nur Tests, die etwas anderes als die Tastatur prüfen, tippen wie eine echte Tastatur.
