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
| [187-abnahmetests-view-model-bibliothek-detailseite.md](187-abnahmetests-view-model-bibliothek-detailseite.md) | #187 | Abnahmetests View-Model: Bibliothek, Detailseite, Spielstart und App-Start | 0 |
| [188-abnahmetests-suche-und-tastatur.md](188-abnahmetests-suche-und-tastatur.md) | #188 | Abnahmetests View-Model: Suche und Bildschirmtastatur | 0 |
| [189-abnahmetests-einstellungen-controller-updates.md](189-abnahmetests-einstellungen-controller-updates.md) | #189 | Abnahmetests View-Model: Einstellungen, Mausmodus, Controller-Support und Updates | 0 |
| [190-abnahmetests-launcher-und-ersteinrichtung.md](190-abnahmetests-launcher-und-ersteinrichtung.md) | #190 | Abnahmetests View-Model: Launcher-Einstellungen und Ersteinrichtung | 0 |
| [191-abnahmetests-headless-ui-bibliothek.md](191-abnahmetests-headless-ui-bibliothek.md) | #191 | Abnahmetests Headless-UI: Bibliothek, Detailseite, Header, Maus und Tastatur | 0 |
| [192-abnahmetests-headless-ui-suche-einstellungen.md](192-abnahmetests-headless-ui-suche-einstellungen.md) | #192 | Abnahmetests Headless-UI: Suche, Tastatur, Einstellungen und Launcher-Seiten | 0 |
| [193-abnahmetests-launcher-fixtures.md](193-abnahmetests-launcher-fixtures.md) | #193 | Abnahmetests Launcher-Fixtures: Steam und Epic mit nachgestellten Dateien | 0 |

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

Automatisiert in `tests/Arcadia.App.Tests` (Abnahme-Iteration 1, #184, #187, #188, #189, #190; Regeln im [README](../README.md)):

| Testfall | Test | Abdeckung |
|---|---|---|
| G-1 | `StartTests.Start_shows_a_tab_per_launcher_with_the_first_tab_selected_and_a_game_focused` | ganz |
| G-2 | `StartTests.A_fresh_system_without_any_launcher_says_so_after_the_first_question_without_an_error`, `StartTests.Launchers_without_games_show_their_tabs_saying_so_without_an_error` | ganz (Hinweis seit #136/#139: „Kein Launcher aktiv“ bzw. „Keine Spiele in …“) |
| G-4 | `StartTests.A_broken_settings_file_is_kept_as_broken_and_Arcadia_starts_with_the_defaults` | ganz |
| G-5 | `StartTests.The_log_keeps_the_user_profile_out_of_what_it_writes` | teilweise: nur Benutzerprofil aus dem Log; Tasten, Gamepad-Tasten, SISR und Rotation ab 5 MB nicht |
| G-6 | `LibraryTests.The_D_pad_moves_the_focus_through_the_grid_and_stops_at_its_edges`, `…The_D_pad_held_repeats_the_move_after_a_moment_and_stops_at_the_edge_of_the_page`, `…The_left_stick_moves_the_focus_like_the_D_pad_but_not_when_only_nudged` | teilweise: Halten und Stick über `GamepadPolls`, nicht über XInput; ohne Sichtbarkeit |
| G-7 | `LibraryTests.LB_and_RB_switch_the_launcher_tab_and_wrap_around` | teilweise: ohne Drehen und Ausblenden der Leiste |
| G-8 | `LibraryTests.LT_and_RT_turn_the_pages_and_the_page_bar_shows_the_current_page` | ganz |
| G-12, G-14 | `LibraryTests.View_options_change_the_page_size_at_once_and_offer_only_sizes_the_tab_can_fill` | ganz |
| G-13 | `LibraryTests.Each_sort_in_view_options_orders_the_tab_with_games_without_a_date_last` (alle sechs Sortierungen) | ganz |
| G-15 | `LibraryTests.The_preview_shows_last_played_playtime_size_launcher_and_location_of_the_focused_game` | ganz |
| G-20 | `GameStartTests.A_demo_game_shows_the_demo_hint_instead_of_an_error` | ganz |
| G-30 | `StartTests.The_clock_runs_and_no_status_symbol_shows_while_nothing_needs_one` | teilweise: Uhr und „ohne Bedarf kein Symbol“; Symbole bei Bedarf und Öffnen der Einstellungen nicht |
| T8-1 | `LibraryTests.Sort_page_size_and_tab_are_the_same_after_a_restart_with_the_focus_on_page_one` | teilweise: Tab per LB/RB, nicht per Klick |
| T8-2 | `LibraryTests.Sort_and_page_size_apply_to_every_tab_and_a_small_tab_shows_the_next_smaller_size_only_for_itself` | ganz |
| T8-3 | `LibraryTests.A_last_tab_whose_launcher_is_gone_starts_in_the_first_tab_and_in_its_own_again_once_it_is_back` | teilweise: Launcher ganz fehlend; ein deinstallierter, aber aktiver Launcher behält seit #136 seinen Tab („nicht gefunden“) |
| T10-3 | `InstallTests.The_menu_of_a_game_not_installed_offers_installing_and_shows_a_running_installation_instead` | ganz |
| T10-4 | `GameStartTests.A_tapped_starts_the_focused_game_through_its_launcher_on_the_release`, `…A_held_shows_the_frame_then_opens_the_detail_page…`, `…A_let_go_between_the_frame_and_the_details_does_nothing_also_later` | teilweise: Rahmen als Zustand, nicht wie er aussieht |
| T11-1 | `GameStartTests.A_held_shows_the_frame_then_opens_the_detail_page_after_a_second_while_still_held_and_starts_nothing` | teilweise: Halten und Position, ohne ⧉ → „Details anzeigen“, Pfadleiste, Nachbarn |
| T11-2 | `DetailPageTests.LT_and_RT_show_the_previous_and_next_game_across_page_borders_and_wrap_around` | teilweise: ohne Animation |
| T11-3 | `DetailPageTests.With_two_games_the_other_one_stands_on_both_sides_and_with_one_game_there_are_no_neighbours` | teilweise: ohne Abdunklung |
| T11-4 | `DetailPageTests.LB_and_RB_switch_the_launcher_and_the_detail_page_stays_open_on_the_first_game_of_the_tab` | teilweise: LB/RB, nicht Klick auf einen Tab |
| T11-5 | `DetailPageTests.Y_on_the_detail_page_says_it_shows_one_game_and_another_sort_keeps_the_game_while_neighbours_and_position_follow` | ganz |
| T11-6 | `DetailPageTests.B_goes_back_to_the_library_on_the_page_of_the_game_shown_last_with_the_focus_on_it` | teilweise: B, nicht Klick auf die Pfadleiste |
| T11-8 | `DetailPageTests.An_update_keeps_Spielen_with_a_note_and_a_game_not_installed_offers_Installieren_and_its_progress` | teilweise: ohne abgedunkeltes Cover |
| T11-9 | `DetailPageTests.The_figures_show_last_played_with_date_and_time_playtime_by_the_launcher_install_date_with_path_and_size_with_drive` | teilweise: Werte, nicht wo sie stehen (nicht unter dem Titel) |
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
| T27-1 | `GameStartTests.A_start_that_fails_says_so_leaves_Arcadia_running_and_A_tries_again` | ganz (Fake-Launcher, der beim Start wirft) |
| T27-3 | `GameStartTests.An_unexpected_error_goes_with_its_stack_trace_into_the_log_a_hint_names_the_log_and_the_controller_still_works` | ganz (Fake-Launcher, der beim Start wirft) |
| T41-7 | `LibraryTests.Sorted_by_install_date_games_not_installed_come_last_in_both_directions` | ganz |
| T41-9 | `InstallTests.A_game_waiting_in_the_queue_shows_the_hourglass_instead_of_the_spinner_on_the_cover_and_in_the_preview` | teilweise: Zustand der Kachel, nicht das Symbol selbst; Warteschlange aus einem Fake-Monitor, nicht aus Epics Dateien |
| T95-8 | `RunningGameTests.In_the_demo_data_one_game_per_launcher_runs_and_offers_Zurueck_ins_Spiel_but_no_uninstall` | teilweise: ohne Größe und Lage von „Zurück ins Spiel“ und „Optionen“, ohne Tastatur |
| T15-1 | `SettingsTests.Menu_opens_the_settings_and_B_goes_back_to_the_same_tab_page_and_game` | teilweise: Controller, nicht Enter/Esc und Klick (prüft der Fenstertest, #192) |
| G-31 | `SettingsTests.The_settings_are_worked_with_the_controller_alone_rows_categories_and_values` | teilweise: Scrollen ins Bild nicht (prüft der Fenstertest, #192) |
| T15-5 | `SettingsRowTests.The_footer_names_only_the_buttons_of_the_focused_row` | teilweise: was der Footer nennt (◀▶, A mit Text, Kategorien), nicht der Wechsel zu Tasten und Maus nach Taste oder Klick |
| T15-10 | `ControllerSupportTests.The_header_symbols_open_their_category_Controller_for_SISR_and_Ueber_Arcadia_for_Arcadias_update` | teilweise: Klick als Befehl des View Models, nicht auf das Symbol |
| T15-11 | `SettingsRowTests.Choices_wrap_at_their_ends_and_switches_flip_with_either_arrow`, `…Steam_Controller_Kompatibilitaet_switches_with_either_arrow` | teilweise: ohne Schalter der Ersteinrichtung und ohne Taste A der Tastatur (die Tests drücken ◀) |
| T23-2 | `UpdateTests.Without_a_newer_release_the_search_says_Arcadia_is_up_to_date_and_offers_no_update` | teilweise: Velopack mit `TestVelopackLocator` und eigener Quelle statt installiertem Arcadia und GitHub |
| T23-4 | `UpdateTests.What_is_new_lists_the_changes_of_every_release_after_the_running_one_and_B_goes_back` | teilweise: wie T23-2 |
| T64-7 | `MouseModeSettingsTests.Mausmodus_hold_and_speed_change_with_the_controller_apply_at_once_and_stay_after_a_restart` | teilweise: Werte und Neustart; wie sie wirken, prüft `MouseModeTests`; „startet immer im normalen Modus“ nicht (braucht das Fenster) |
| T64-8 | `MouseModeSettingsTests.So_funktioniert_es_shows_the_buttons_and_the_limits` | teilweise: seit #15 eine Box im Infotext von „Mausmodus“ statt eines Dialogs mit A, B schließt die Einstellungen |
| T64-10 | `ViewAndMenuTests.View_and_Menu_act_when_let_go_or_held_alone_a_moment`, `…pressed_together_open_nothing`, `…Menu_held_a_second_on_a_search_hit_still_opens_its_details`, `…With_a_game_in_front_or_in_the_mouse_mode_the_buttons_leave_Arcadia_alone` | teilweise: über `GamepadRouting` ohne XInput; von den anderen Tasten nur das Steuerkreuz |
| T64-12 | `MouseModeSettingsTests.Mausmodus_Aus_dims_the_other_rows_which_keep_their_values_for_when_it_comes_back` | teilweise: die Einstellungen; ⧉ + ≡ und Fokuswechsel bei „Aus“ prüfen `MouseModeTests` und `MouseModeAutomationTests`, „Mausmodus aus“ nicht |
| T64-15 | `MouseModeSettingsTests.The_mouse_mode_boxes_name_View_and_Menu_by_their_symbols_only` | teilweise: Texte, nicht die runde Umrandung |
| T85-1 | `ControllerSupportTests.Without_SISR_or_what_it_needs_the_setting_offers_SISR_installieren_instead_of_the_switch` (vier Fälle) | ganz (Fake-Plattform) |
| T85-2 | `ControllerSupportTests.With_Steam_running_SISR_installieren_asks_first_and_cancelling_installs_nothing` | ganz (Fake-Plattform) |
| T85-9 | `ControllerSupportTests.Without_Windows_or_Steam_the_setting_is_greyed_out_says_why_and_A_does_nothing` | ganz (Fake-Plattform) |
| T85-11 | `ControllerSupportTests.Steam_not_starting_within_a_minute_shows_Steam_startet_nicht_with_cancel_and_retry`, `…Steam_hanging_at_its_login_when_switching_on_shows_the_prompt_and_SISR_does_not_start` | ganz (Fake-Plattform, Uhr gefälscht) |
| T85-14 | `ControllerSupportTests.While_Steam_or_SISR_start_the_switch_is_locked_and_other_buttons_do_nothing`, `…While_Steam_or_SISR_start_B_leaves_the_settings_at_once` (zwei Fälle), `…With_Steam_hanging_at_its_login_the_switch_stays_usable` | teilweise: Start von Steam und SISR; Beenden von selbst (nach dem Ende von Steam) nicht |
| T85-16 | `ControllerSupportTests.Steam_hanging_at_its_login_for_over_30_seconds_shows_a_message_with_Steam_neu_starten_and_SISR_keeps_running` | ganz (Fake-Plattform) |
| T92-1 | `ControllerSupportTests.The_setting_is_called_Steam_Controller_Kompatibilitaet_with_both_modes_and_no_text_says_Steam_Controller_Support` | teilweise: Einstellungen und Statusleiste, nicht die Deinstallationsfrage (Installer) |
| T92-2 | `ControllerSupportTests.The_preview_shows_Steam_Input_for_Steam_games_and_Xbox_Controller_SISR_for_all_others` | teilweise: der Wert, nicht „ohne Dropdown-Pfeil“ |
| T110-9 | `MouseModeSettingsTests.So_funktioniert_es_shows_the_buttons_and_the_limits`, `…Zurueck_zu_Arcadia_holds_from_1_to_9_seconds_without_Sofort` | ganz |
| T127-2 | `ControllerSupportTests.On_the_detail_page_the_game_settings_show_no_note_below_the_buttons_and_the_context_menu_says_what_they_switch_to` | teilweise: ohne Tooltip über einem deaktivierten Button (Maus) |
| T136-4 | `LauncherSettingsTests.Y_lifts_a_launcher_the_D_pad_moves_it_and_Y_puts_it_down_with_the_tabs_following_at_once`, `…B_while_a_launcher_is_lifted_puts_everything_back_as_it_was`, `…A_launcher_put_down_under_the_inactive_ones_is_off_and_its_tab_goes` | teilweise: ohne Maus (Ziehen), Rahmen und Footer nur als Zustand |
| T136-5 | `LauncherSettingsTests.Beside_the_list_the_tab_strip_shows_the_tabs_in_order_and_the_details_explain_the_focused_launcher` | teilweise: ohne Logo, RS-Scrollen und Fade; Warnungen in T136-16 |
| T136-6 | `LauncherSettingsTests.X_reads_all_active_launchers_again_and_a_toast_says_so` | teilweise: Toast als Text, nicht seine Lage |
| T136-7 | `LauncherSettingsTests.A_tab_without_games_keeps_its_tab_and_its_buttons_read_again_or_open_its_launcher_settings`, `…A_tab_of_a_launcher_no_longer_found_keeps_its_tab_and_looks_for_it_again`, `…With_every_launcher_off_the_library_says_so_and_offers_only_the_launcher_settings` | teilweise: ohne Maus und Leertaste, ohne Bild der leeren Tabs |
| T136-14 | `LauncherSettingsTests.The_row_read_all_again_is_reached_with_the_D_pad_and_A_reads_like_X`, `…Without_an_active_launcher_the_row_read_all_again_is_greyed_out_and_A_does_nothing` | teilweise: Glyphe nur als Wert, ohne Klick |
| T136-15 | `LauncherSettingsTests.With_every_launcher_off_the_library_says_so_and_offers_only_the_launcher_settings` | teilweise: ohne Klick |
| T136-16 | `LauncherSettingsTests.An_active_launcher_without_games_is_information_without_a_symbol_at_the_category`, `…An_active_launcher_no_longer_found_stays_a_warning_with_a_symbol_at_the_category` | teilweise: Farbe und Symbol nur als Stufe |
| T137-2 | `LauncherPageTests.LT_and_RT_turn_to_the_launcher_before_and_after_in_the_order_of_the_list_and_wrap_around`, `…A_single_launcher_has_no_neighbours_to_turn_to` | teilweise: ohne Klick auf Nachbarn und Rahmen beim Überfahren |
| T137-3 | `LauncherPageTests.Active_on_the_page_switches_like_the_list_and_the_head_shows_the_state`, `…A_launcher_not_found_cannot_be_switched_on_on_its_page` | ganz |
| T137-5 | `LauncherPageTests.A_name_taken_shows_at_once_in_tab_list_search_preview_and_details_also_after_a_restart`, `…A_name_taken_empty_is_the_launchers_own_name_again` | teilweise: in der Suche nur der Chip, nicht die Treffer |
| T137-6 | `LauncherPageTests.A_keyword_taken_by_another_launcher_or_against_the_rules_is_red_at_once_and_not_taken`, `…A_valid_keyword_holds_at_once_in_the_search_and_its_chip`, `…The_keywords_on_screen_keyboard_offers_only_keys_it_may_hold_and_the_D_pad_skips_the_others`, `…On_a_French_keyboard_shift_stays_for_the_digits_and_offers_only_them`, `…The_names_on_screen_keyboard_offers_every_key` | teilweise: rot und ausgegraut nur als Zustand, ohne Stick |
| T137-7 | `LauncherPageTests.The_info_panel_previews_the_order_with_only_the_pages_launcher_framed_and_gives_an_example_for_the_keyword`, `…Read_again_on_the_page_reads_only_this_launcher_and_then_names_its_games_and_the_time`, `…Read_again_is_greyed_out_with_the_reason_for_a_launcher_inactive_or_not_found` | teilweise: ohne Log-Zeile, Erklärtexte nur vorhanden |
| T139-1 | `FirstRunTests.A_fresh_install_asks_first_with_the_launchers_found_switched_on`, `FirstRunTests.A_launcher_not_found_cannot_be_switched_on` | teilweise: ohne Überschrift und Ausgrauen |
| T139-2 | `FirstRunTests.The_preview_shows_the_launchers_switched_on_as_tabs_and_follows_every_switch` | teilweise: ohne Datum und Uhrzeit, „Später änderbar“, „Fertig“ und Text der leeren Vorschau |
| T139-3 | `FirstRunTests.Only_the_launchers_left_on_become_tabs_also_after_a_restart_and_the_question_does_not_come_again` | teilweise: „Fertig“ mit ≡, nicht Enter oder Klick (prüft der Fenstertest, #192) |
| T139-4 | `FirstRunTests.Settings_of_a_version_without_a_choice_of_launchers_bring_the_question_and_keep_everything_else` | ganz |
| T139-5 | `FirstRunTests.Demo_skips_the_question_with_every_launcher_active_and_saves_no_choice`, `…First_run_asks_again_also_with_a_saved_choice`, `…Demo_with_first_run_keeps_the_answer_only_until_Arcadia_closes` | teilweise: Schalter ohne Kommandozeile, Test-Launcher statt Demo-Bibliothek |
| T139-7 | `FirstRunTests.Done_with_no_launcher_switched_on_shows_no_launcher_active`, `…Done_with_no_launcher_found_shows_no_launcher_active` | ganz |

Launcher-Fixtures (#193): Die echten Integrationen von Steam und Epic lesen nachgestellte Dateien in einem Temp-Ordner
(`Fixtures/SteamFolder`, `Fixtures/EpicFolder`, Formate nach `docs/launchers/`); was der Launcher selbst tut, zeigt nur
der echte Launcher im Abschluss-Test.

| Testfall | Test | Abdeckung |
|---|---|---|
| T1-1 | `SteamLibraryTests.The_Steam_tab_shows_the_games_of_all_libraries_with_demos_but_without_tools_redistributables_and_soundtracks` | ganz |
| T1-2 | `SteamLibraryTests.Size_last_played_and_playtime_are_Steams_and_the_install_date_is_the_game_folders_creation` | teilweise: gegen die Dateien, nicht gegen Steams Anzeige; unter Linux ohne eigenes Erstelldatum |
| T1-5 | `SteamLibraryTests.Without_Steam_there_is_no_Steam_tab_and_no_error` | ganz |
| T1-6 | `SteamLibraryTests.With_demo_the_Steam_tab_shows_the_real_games_and_only_the_others_are_demo_libraries` | ganz |
| T1-9 | `SteamLibraryTests.A_library_whose_drive_is_gone_leaves_out_its_games_without_an_error_and_they_come_back_with_the_drive` | teilweise: Ordner verschoben statt Laufwerk abgezogen |
| T41-3 | `EpicInstallTests.An_install_from_Arcadia_waits_for_confirmation_then_queues_downloads_and_ends_installed_without_a_restart` | teilweise: Epics Dialog nur als geöffnete URI |
| T41-4 | `EpicInstallTests.Pause_resume_and_cancel_in_Epic_show_in_Arcadia_and_install_is_locked_while_one_runs` | ganz |
| T41-5 | `EpicInstallTests.An_install_started_in_Epic_itself_shows_in_Arcadia_with_its_state` | ganz |
| T41-6 | `EpicLibraryTests.A_game_uninstalled_in_Epic_shows_as_not_installed_once_Arcadias_window_is_active_again`, `…once_it_is_selected` | teilweise: Aktivieren des Fensters wie `MainWindow.Activated`, nicht per Alt+Tab |
| T41-8 | `EpicInstallTests.A_paused_install_that_is_cancelled_loses_its_progress` | ganz |
| T41-10 | `EpicInstallTests.When_Epic_resumes_another_game_the_first_shows_as_paused_and_only_the_second_downloads` | ganz |
| T42-1 | `EpicLibraryTests.The_Epic_tab_shows_every_game_of_the_library_installed_or_not_but_no_Unreal_Engine_or_editor` | teilweise: gegen eine kleine Bibliothek, nicht gegen Epics 333 |
| T42-2 | `EpicLibraryTests.No_DLC_season_pass_beta_soundtrack_or_preorder_bonus_shows_as_a_game` | ganz |
| T42-3 | `EpicLibraryTests.Without_the_catalog_cache_the_tab_shows_the_installed_games_and_no_error` | ganz |
| T42-4 | `EpicLibraryTests.Titles_lose_the_question_marks_of_trademark_signs_and_umlauts_stay_broken` | ganz |
| T81-1 | `EpicLibraryTests.The_update_icon_shows_while_Epic_reports_an_update_and_goes_once_the_update_is_installed` | ganz |
| T81-2 | `EpicLibraryTests.Last_played_comes_from_Epics_log_and_stays_after_Epic_deleted_that_log` | ganz |
| T81-3 | `SteamInstallTests.A_Steam_download_shows_queue_progress_and_pause_and_a_new_install_ends_installed_with_a_note_without_a_restart` | teilweise: Warteschlange, % und Pause am Update eines installierten Spiels; eine neue Installation hat bis zum Ende keine Kachel |
| T81-4 | `SteamInstallTests.Of_two_Steam_downloads_only_the_one_Steam_works_on_downloads_and_the_other_waits` | ganz |
| T81-5 | `SteamInstallTests.Cancelling_a_new_Steam_install_shows_a_note`, `…Uninstalling_in_Steam_shows_a_note_and_the_game_leaves_the_tab`, `…An_update_of_a_Steam_tool_gives_no_note` | teilweise: der Hinweis zum Abbruch einer neuen Installation nennt „Spiel“, weil Arcadia den Titel nicht kennt |
| T81-6 | `SteamInstallTests.A_running_Steam_update_shows_as_updating_not_as_waiting_even_before_its_bytes_move`, `EpicInstallTests.An_Epic_update_shows_as_updating_without_a_percentage_and_not_as_almost_done` | ganz |
| T85-15 | `SteamControllerSetupTests.With_one_Steam_account_the_marker_is_written_without_asking_and_SISR_starts`, `…Without_any_Steam_account_the_install_says_so_at_once_and_leaves_Steam_and_the_option_alone`, `…Without_any_Steam_account_a_running_Steam_is_not_closed` | teilweise: Windows, SISR und Steams Neustart nachgestellt; läuft Steam, kommt „Steam beenden?“ vor der Meldung |
| T85-19 | `SteamControllerSetupTests.With_several_accounts_the_user_picks_with_the_D_pad_who_gets_the_marker_before_Steam_closes`, `…Without_a_tick_there_is_no_going_on_and_B_cancels_the_install`, `…With_only_another_account_chosen_SISR_does_not_start_and_the_settings_offer_to_add_the_entry`, `…With_one_account_listed_the_question_does_not_come_even_if_more_userdata_folders_exist` | teilweise: Windows und SISR nachgestellt, „Abbrechen“ nur per B |

Mit dem echten Fenster (Avalonia Headless mit Skia, `ArcadiaWindow`; #191, #192): Klicks, echte Tasten, Lage und Pixel.

| Testfall | Test | Abdeckung |
|---|---|---|
| T10-1 | `GameMenuTests.View_right_click_Options_and_the_menu_key_open_the_menu_B_View_and_a_click_beside_it_close_it`, `…The_menu_stands_beside_its_cover_left_of_it_in_the_last_column_and_always_inside_the_window` | ganz |
| T10-2 | `GameMenuTests.An_installed_game_lists_its_actions_with_the_update_note_disabled_ones_greyed_and_uninstall_in_red_after_a_line` | ganz |
| T10-4 | `KeyboardAndMouseTests.The_keyboard_plays_like_the_controller` (Leertaste), `…The_mouse_focuses_starts_holds_for_details_and_clicks_every_button_of_the_library` | teilweise: Ring sichtbar und blau, nicht wie er sich füllt (Animation läuft headless nicht); Leertaste statt Enter (Enter öffnet seit #127 die Einstellungen) |
| T11-1 | `DetailPageViewTests.Holding_A_or_Details_in_the_menu_opens_the_page_with_its_path_position_neighbours_and_footer` | teilweise: ohne weiches Ausblenden zum Rand |
| T11-2 | `DetailPageViewTests.LT_and_RT_slide_the_pages_and_clicks_wheel_and_keys_turn_the_carousel_too` | teilweise: dass ein Schieben angelegt ist, nicht Richtung und Ablauf (läuft headless nicht) |
| T11-6 | `DetailPageViewTests.A_click_on_the_path_goes_back_to_the_library_on_the_page_of_the_game_shown_last_with_the_focus_on_it` | ganz (mit dem VM-Test für B) |
| T11-7 | `DetailPageViewTests.The_buttons_stand_in_rows_inside_the_card_the_D_pad_skips_disabled_ones_and_shows_the_hint_below` | teilweise: ohne Steam-Controller- und Mausmodus-Reihe; Hinweis nur noch für „Spielen“, sonst Tooltip (Owner 2026-10-21) |
| T41-1 | `LibraryViewTests.A_game_not_installed_is_greyed_with_the_install_symbol_and_the_preview_offers_to_install_it` | ganz |
| T102-1 | `GridFadeTests.A_page_taller_than_the_grid_fades_only_towards_where_it_scrolls_and_the_end_rows_show_whole` | teilweise: „weich“ als angelegter Übergang, nicht wie er aussieht |
| T102-2 | `GridFadeTests.A_page_that_fits_the_grid_has_no_fade` | ganz |
| T102-3 | `GridFadeTests.The_focused_tile_never_lies_under_a_fade_and_the_wheel_switches_the_fades_too` | ganz |
| T102-4 | `GridFadeTests.No_line_of_unfaded_covers_is_left_at_the_edge_of_a_fade_also_at_a_scaled_size` (1745×1000, 125 %) | ganz |
| T127-1 | `DetailPageViewTests.The_focus_ring_touches_no_neighbour_and_the_key_figures_show_whole_also_with_an_update_note` | ganz |
| T127-5 | `HeaderTests.Few_launchers_the_strip_stands_in_the_middle_as_wide_as_needed_with_LB_and_RB_beside_it_and_no_dividers` | ganz |
| T127-6, G-7 | `HeaderTests.Many_launchers_the_strip_reaches_logo_and_status_turns_with_LB_RB_and_the_wheel_and_fades_where_tabs_follow` | teilweise: ohne Statusmeldung im Header |
| T127-7 | `KeyboardAndMouseTests.The_hints_show_controller_buttons_after_a_button_and_keys_after_a_key_or_click_but_not_after_a_mouse_move` | ganz |
| T127-8, G-10 | `KeyboardAndMouseTests.The_keyboard_plays_like_the_controller` | ganz |
| T127-10 | `DetailPageViewTests.A_title_with_descenders_shows_whole_also_on_two_lines` | ganz |
| T138-1 | `LibraryViewTests.Steam_and_Epic_show_their_logo_everywhere_a_launcher_is_named_and_others_keep_their_letter` | ganz |
| G-11 | `KeyboardAndMouseTests.The_mouse_focuses_starts_holds_for_details_and_clicks_every_button_of_the_library` | ganz |
| G-32 | `FocusRingTests` (Cover an den Ecken des Rasters, „Ansicht“, Einstellungen) | teilweise: Einstellungen, die es unter Linux nicht gibt (Steam Controller), werden gedimmt gezeichnet und übersprungen |
| G-33 | `GameMenuTests.View_R_and_a_click_on_Options_open_the_menu_over_the_Options_button_on_its_left_and_bottom_edges`, `…The_menu_stands_beside_its_cover…` | ganz |
| T8-1 | `KeyboardAndMouseTests.A_tab_chosen_by_click_is_the_same_after_a_restart` | ganz (mit dem VM-Test) |
| T12-1 | `SearchViewTests.X_opens_the_search_with_its_head_field_chips_empty_state_keyboard_and_controller_legend_F_and_the_lens_without_the_keyboard` | ganz (der Fokus startet auf „a“, nicht auf „q“) |
| T12-4 | `SearchViewTests.A_typed_prefix_searches_only_its_launchers_the_chips_follow_and_an_unknown_one_is_named_in_orange` | ganz |
| T12-5 | `SearchViewTests.Chips_and_the_L3_filter_switch_launchers_the_last_stays_on_and_Alle_Launcher_switches_all_on_a_typed_prefix_is_rewritten` | ganz |
| T12-6 | `SearchViewTests.R3_opens_the_layout_menu_over_the_hits_A_picks_a_layout_B_and_R3_close_it_first_and_the_layout_stays_after_a_restart` | ganz |
| T12-10 | `SearchViewTests.A_real_keyboard_types_erases_moves_through_the_hits_starts_holds_for_details_and_closes_but_never_reaches_the_on_screen_keys` | ganz |
| T12-12 | `SearchViewTests.The_keyboard_rolls_out_and_in_with_its_tab_on_its_top_edge_by_X_B_a_click_on_the_tab_or_a_real_key_and_the_hits_follow` | teilweise: das Rollen als angelegter Übergang, nicht wie es aussieht |
| T12-16 | `SearchViewTests.More_hits_than_fit_the_row_shows_faded_neighbours_and_turns_round_and_the_grid_scrolls_by_two_rows_and_stops_at_its_ends`, `…The_row_of_hits_fades_out_to_the_background_without_a_line_at_its_edges_also_at_a_scaled_size` (1745×1000, 1536×864 bei 125 %) | teilweise: das Gleiten nur als Ergebnis |
| T12-18 | `SearchViewTests.The_first_click_on_a_key_or_the_tab_acts_and_switches_the_legend_to_the_mouse_a_button_brings_the_controller_back` | ganz |
| T110-4 | `OnScreenKeyboardViewTests.In_the_mouse_mode_R3_opens_the_layout_menu_top_right_A_picks_B_closes_only_it_and_US_has_the_ANSI_keys_with_AltGr_greyed` | teilweise: Tastaturfenster des Mausmodus ohne `MouseModeHost`, „gilt auch in der Suche und umgekehrt“ nicht |
| T110-10 | `SearchViewTests.The_searchs_keyboard_is_the_mouse_modes_without_tab_and_edge_key_its_enter_starts_the_hit_and_Ausblenden_puts_it_in` | ganz (linker Stick über nachgestellte Gamepad-Abfragen) |
| T110-14 | `SearchViewTests.The_focus_ring_shows_whole_on_the_keys_at_the_keyboards_four_edges_in_the_search_and_in_the_mouse_mode` | ganz |
| T129-7 | `OnScreenKeyboardViewTests.A_click_on_a_key_types_the_character_shown_and_clicks_on_shift_and_AltGr_switch_the_level` | ganz |
| T15-1 | `SettingsViewTests.The_settings_cover_the_whole_page_and_Menu_B_Enter_Esc_the_chip_and_X_lead_back_to_the_same_tab_page_and_game_or_detail_page` | ganz |
| T15-2 | `SettingsViewTests.LB_RB_shift_Q_E_and_a_click_switch_the_category_round_from_the_last_to_the_first_the_strip_in_the_middle` | teilweise: Kategorien seit #137 mit ⇧ Q/⇧ E statt Q/E; Ausfaden der Leiste mit drei Kategorien nicht prüfbar |
| T15-4 | `SettingsViewTests.In_Controller_the_D_pad_walks_the_rows_with_their_headings_in_view_values_change_at_once_Aus_dims_and_all_stays_after_a_restart` | teilweise: „Beheben“, „Aktualisieren“, „SISR installieren“ nur in den VM-Tests zu #85 |
| T15-6 | `SettingsViewTests.Mausmodus_explains_itself_with_a_box_the_info_panel_fades_where_it_scrolls_RS_shift_W_S_page_keys_and_the_wheel_scroll_it` | ganz |
| T15-9 | `SettingsViewTests.Every_setting_is_reached_with_one_hand_on_the_keyboard_and_with_the_mouse_alone_which_focuses_a_row_without_changing_it` | ganz (Kategorien mit ⇧ Q/⇧ E) |
| G-31 | `SettingsViewTests.The_focused_setting_scrolls_into_view_in_every_category_and_B_closes` | ganz (mit dem VM-Test) |
| T64-11 | `SettingsViewTests.A_mouse_move_or_a_key_shows_the_X_at_the_top_right_of_each_dialog_which_closes_only_it_and_a_button_hides_it_again` | teilweise: von den SISR-Dialogen nur „Steam beenden?“; „So funktioniert's“ ist seit #15 eine Box im Infotext, kein Dialog |
| T92-3 | `SettingsViewTests.A_click_on_the_mode_in_the_preview_switches_it_and_back_to_the_start_value_nothing_is_saved` | ganz |
| T92-5 | `SettingsViewTests.The_settings_scroll_and_the_footer_stays_visible_at_the_bottom_also_scrolled_to_the_top_and_to_the_end` | ganz |
| T127-9 | `SettingsViewTests.The_mouse_mode_settings_draw_LS_and_L3_as_symbols_in_their_text_and_So_funktioniert_es_LS_RS_and_the_D_pad` | ganz |
| T136-2 | `LauncherViewTests.A_launcher_switched_off_goes_under_Nicht_aktiv_with_its_tab_and_hits_and_switched_on_again_comes_last_and_is_read` (◀, ▶, A, D, Klick) | ganz |
| T136-8 | `LauncherViewTests.One_hand_on_the_keyboard_lifts_moves_puts_down_cancels_and_reads_all_again_with_the_toast_over_the_page`, `…The_mouse_alone_drags_a_launcher_to_another_place_or_under_Nicht_aktiv_and_reads_all_again`, `…The_buttons_of_a_tab_without_games_and_of_no_launcher_active_work_with_the_keyboard_and_the_mouse` | teilweise: Controller in den VM-Tests zu #136; Scrollen der Launcher-Details (⇧ W/S, Mausrad) nicht |
| T137-1 | `LauncherPageViewTests.A_Space_or_a_click_opens_the_page_with_its_head_and_groups_and_B_Esc_or_Launcher_lead_back_to_it_in_the_list` (A, Leertaste, Klick) | ganz (der Zustand steht im Infopanel, nicht im Seitenkopf) |
| T137-4 | `LauncherPageViewTests.Writing_the_name_opens_the_keyboard_below_without_Tab_and_Dock_with_the_caret_in_the_field_and_the_footer_naming_its_keys`, `…With_the_controller_the_D_pad_and_A_type_X_erases_Y_spaces_R3_opens_the_layouts_Menu_takes_and_B_drops`, `…With_a_real_keyboard_keys_type_the_arrows_Home_and_End_move_the_caret_Enter_takes_and_Esc_drops`, `…While_the_name_is_written_the_mouse_focuses_no_other_row_and_clicks_type_on_the_keyboard` | teilweise: LB/RB (⇧/AltGr) nur in den Tests der Suche (T129-2, T129-3) |
| T137-8 | `LauncherPageViewTests.One_hand_on_the_keyboard_turns_the_launchers_switches_one_and_writes_a_keyword`, `…The_mouse_alone_turns_by_the_neighbours_with_their_whole_hover_ring_switches_and_points_at_rows` | teilweise: Controller in den VM-Tests zu #137; Infopanel und Logo nicht |
| T139-3 | `FirstRunViewTests.A_launcher_switched_off_then_Fertig_by_Enter_or_a_click_leaves_only_the_others_as_tabs_also_after_a_restart` (Enter, Klick) | ganz (mit dem VM-Test für ≡) |
| T139-6 | `FirstRunViewTests.B_does_nothing_and_the_controller_alone_switches_and_finishes`, `…Esc_does_nothing_and_one_hand_on_the_keyboard_switches_and_finishes`, `…The_mouse_alone_focuses_by_hovering_switches_with_a_click_on_the_switch_and_finishes_with_Fertig` | ganz |

„Teilweise“ heißt: Der Rest des Testfalls bleibt im Abschluss-Test. „Einmal rot“ zeigt, dass der Test eine
bestimmte Änderung erkennt, nicht dass er den Testfall ganz abdeckt.

Was sie nicht prüfen, bleibt im Abschluss-Test: wie Animationen ablaufen (headless laufen Keyframe-Animationen
und das Schieben der Detailseite nicht), echte Launcher und Windows. Die Bildschirmtastatur bedienen die Tests seit #188
mit D-Pad und A wie der Nutzer; nur Tests, die etwas anderes als die Tastatur prüfen, tippen wie eine echte Tastatur.
