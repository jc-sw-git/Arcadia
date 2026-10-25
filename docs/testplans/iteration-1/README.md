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

Automatisiert in `tests/Arcadia.App.Tests` (Abnahme-Iteration 1, #184; Regeln im [README](../README.md)):

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
| T139-1 | `FirstRunTests.A_fresh_install_asks_first_with_the_launchers_found_switched_on`, `FirstRunTests.A_launcher_not_found_cannot_be_switched_on` | teilweise: ohne Spielzahl und Texte |
| T139-3 | `FirstRunTests.Only_the_launchers_left_on_become_tabs_also_after_a_restart_and_the_question_does_not_come_again` | teilweise: „Fertig“ mit ≡, nicht Enter oder Klick |

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

„Teilweise“ heißt: Der Rest des Testfalls bleibt im Abschluss-Test. „Einmal rot“ zeigt, dass der Test eine
bestimmte Änderung erkennt, nicht dass er den Testfall ganz abdeckt.

Was sie nicht prüfen, bleibt im Abschluss-Test: Aussehen und Animationen, Maus und echte Tastatur
(Headless-Tests, #180), Tippen auf der Bildschirmtastatur mit dem Controller (die Tests tippen wie eine
echte Tastatur; das Raster prüfen `KeyGridTests`), echte Launcher und Windows.
