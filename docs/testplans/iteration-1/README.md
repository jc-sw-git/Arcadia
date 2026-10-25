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
| [189-abnahmetests-einstellungen-controller-updates.md](189-abnahmetests-einstellungen-controller-updates.md) | #189 | Abnahmetests View-Model: Einstellungen, Mausmodus, Controller-Support und Updates | 0 |

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

Automatisiert in `tests/Arcadia.App.Tests` (Abnahme-Iteration 1, #184, #189; Regeln im [README](../README.md)):

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
| T12-3 | `SearchTests.Hits_come_from_all_launchers_by_relevance_whatever_the_case_accents_and_trademark_signs`, `SearchTests.An_accent_or_trademark_sign_in_the_title_or_the_term_does_not_matter_and_no_hit_shows_the_empty_state` | teilweise: ohne ® und ©, ohne Text des leeren Zustands |
| T12-9 | `SearchTests.B_first_puts_the_keyboard_away_then_goes_back_to_the_library_as_it_was_without_erasing` | teilweise: ohne zweimal schnell B |
| T15-1 | `SettingsTests.Menu_opens_the_settings_and_B_goes_back_to_the_same_tab_page_and_game` | teilweise: Controller, nicht Enter/Esc und Klick |
| G-31 | `SettingsTests.The_settings_are_worked_with_the_controller_alone_rows_categories_and_values` | teilweise: Scrollen ins Bild nicht |
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
| T139-1 | `FirstRunTests.A_fresh_install_asks_first_with_the_launchers_found_switched_on`, `FirstRunTests.A_launcher_not_found_cannot_be_switched_on` | teilweise: ohne Spielzahl und Texte |
| T139-3 | `FirstRunTests.Only_the_launchers_left_on_become_tabs_also_after_a_restart_and_the_question_does_not_come_again` | teilweise: „Fertig“ mit ≡, nicht Enter oder Klick |

„Teilweise“ heißt: Der Rest des Testfalls bleibt im Abschluss-Test. „Einmal rot“ zeigt, dass der Test eine
bestimmte Änderung erkennt, nicht dass er den Testfall ganz abdeckt.

Was sie nicht prüfen, bleibt im Abschluss-Test: Aussehen und Animationen, Maus und echte Tastatur
(Headless-Tests, #180), Tippen auf der Bildschirmtastatur mit dem Controller (die Tests tippen wie eine
echte Tastatur; das Raster prüfen `KeyGridTests`), echte Launcher und Windows.
