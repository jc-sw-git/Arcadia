# #81 Gleiche Funktionen für Steam und Epic

- [ ] **T81-1** Ein Epic-Spiel, für das Epic ein Update anzeigt, zeigt in Arcadia das Update-Symbol;
      nach dem Update verschwindet der Hinweis. · E2E: ja (Epic-Log und Manifeste als Testdateien)
- [ ] **T81-2** Ein Epic-Spiel, das über Epic gespielt wurde, zeigt „zuletzt gespielt“; das Datum
      bleibt erhalten, auch wenn Epic das alte Log gelöscht hat. · E2E: ja (Testdateien)

- [ ] **T81-3** Eine Steam-Installation (aus Arcadia oder in Steam gestartet) zeigt in Arcadia
      Warteschlange, Fortschritt in %, Pause und am Ende den Hinweis; das Spiel ist danach ohne
      Neustart installiert. · E2E: ja (Manifeste als Testdateien)
- [ ] **T81-4** Laufen zwei Steam-Downloads, zeigt nur der aktive „lädt“, der andere die Sanduhr. · E2E: ja (Testdateien)
- [ ] **T81-5** Abbrechen einer neuen Steam-Installation und Deinstallieren in Steam zeigt Arcadia
      mit Hinweis an; Updates von Steam-Werkzeugen (z. B. Redistributables) erzeugen keinen Hinweis. · E2E: ja (Testdateien)
- [ ] **T81-6** Ein laufendes Update zeigt „Wird aktualisiert“: bei Steam als „lädt“ (nicht als Warteschlange),
      bei Epic ohne Prozentangabe (nicht „Fast fertig“). · E2E: ja (Testdateien)

Die Spielzeit für Epic kommt mit #82 (Epic-Anmeldung).

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T81-1, T81-2 | 2026-10-12 | Claude | PR #80, Linux, Log-Leser gegen Kopien der 4 Epic-Logs des Owners, nicht in der App | ✅ 4 Sitzungen, Update nur bei Terraforming Mars (wie in Epic) | Ansicht in Arcadia am PC des Owners |
| T81-3 bis T81-5 | 2026-10-12 | Juri + Claude | PR #80, PC des Owners: Age of Empires II und System Shock 2 installiert, pausiert, vorgezogen, abgebrochen, deinstalliert; Claude hat die Manifeste mitgelesen, Arcadia lief nicht | ✅ Abläufe als Unit-Tests nachgestellt | Ansicht in Arcadia am PC des Owners |
| T81-6 | 2026-10-12 | Juri | PR #80, PC des Owners, Arcadia: Update von Bloons TD 6 (Steam) und Terraforming Mars (Epic) über „Update starten“ | ❌ Steam „Warteschlange“ statt „lädt“, Epic durchgehend „Fast fertig“; beide Spiele starteten danach | Steam liest jetzt `content_log.txt`, Epic-Updates ohne Prozent; A heißt wieder „Spielen“ (Entscheidung des Owners, kein Launcher kann nur aktualisieren) |
| T81-6 | 2026-10-12 | Juri + Claude | PR #80, PC des Owners: `steam://validate` für Schedule I, Claude hat Manifest und `content_log.txt` mitgelesen | ✅ Log meldet `Update Running` sofort; das Manifest schreibt die Bytes nur zu Beginn und Ende | Unit-Test mit den echten Log-Zeilen |
| G-16 | 2026-10-12 | Juri | PR #80, PC des Owners, The Witcher 3 mit Update | ❌ Hinweis unter „Spielen“ abgeschnitten; Update-Symbol auf der Kachel passt nicht zu den anderen, Badge auf der Detailseite wirkt wie ein Button | Hinweise umbrechen, Kachel-Symbol im Stil der anderen, Detailseite zeigt „Update verfügbar“ als Text unter dem Titel |
| G-16 | 2026-10-12 | Claude | PR #80, Linux unter Xvfb, `--demo`, 1920×1080 | ✅ Symbol und Text wie geplant (Screenshot); Umbruch nur per Einstellung, mit langen Namen nicht gesehen | Ansicht am PC des Owners |
