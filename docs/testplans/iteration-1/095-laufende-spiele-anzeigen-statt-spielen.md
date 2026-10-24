# #95 Laufende Spiele anzeigen statt „Spielen“ anzubieten

- [x] **T95-1** Ein Steam-Spiel mit A starten: Kachel „Startet“, Vorschau und Detailseite „Wird gestartet …“, dann
      „Läuft“ (Kachel) bzw. „Läuft seit HH:MM“ (Vorschau, Detailseite, Suchtreffer); A, Footer und Kontextmenü heißen
      „Zurück ins Spiel“, kein Update-Symbol. Nach dem Beenden zeigt alles wieder „Spielen“. · E2E: nein (echter
      Launcher, echtes Spiel)
- [x] **T95-2** Wie T95-1 mit einem Epic-Spiel. · E2E: nein (echter Launcher, echtes Spiel)
- [x] **T95-3** Ein Spiel direkt in Steam starten, dann zu Arcadia wechseln: Es zeigt nach höchstens 2 s „Läuft“;
      nach dem Beenden wieder „Spielen“. Dasselbe mit Epic. · E2E: nein (echter Launcher, echtes Spiel)
- [x] **T95-4** „Zurück ins Spiel“ nur mit dem Controller, je einmal über A in der Bibliothek, den Button der
      Detailseite und das Kontextmenü: Das Spielfenster kommt nach vorne, auch wenn es minimiert war. Ein Spiel ohne
      Fenster (noch im Ladebildschirm des Launchers) bringt einen Toast. · E2E: nein (echtes Spiel)
- [x] **T95-5** Ein Spiel, das als Administrator läuft (z. B. per Rechtsklick „Als Administrator ausführen“ aus seinem
      Ordner gestartet): „Zurück ins Spiel“ holt es nach vorne oder meldet per Toast, dass es nicht ging; Ergebnis im
      Verlauf notieren. · E2E: nein (UAC)
- [x] **T95-6** Während „Wird gestartet …“ nochmals A drücken: Toast „… wird bereits gestartet …“, das Spiel startet
      nicht ein zweites Mal. · E2E: nein (echter Launcher)
- [x] **T95-7** Während ein Spiel läuft, ein anderes aus Arcadia starten: Es startet (sofern der Launcher es
      zulässt), beide zeigen „Läuft“. · E2E: nein (echte Spiele)
- [x] **T95-8** Demo-Daten (`--demo`): In jedem Launcher läuft ein Spiel (Kachel „Läuft“, Vorschau „Läuft seit …“,
      25 min vor dem Start von Arcadia). Vorschau: „Zurück ins Spiel“ steht in voller Größe ganz im Button, mit
      Abstand zur Rundung, mit Controller wie mit Tastatur; „Optionen“ steht darunter. Detailseite: Chip „LÄUFT SEIT …“, Button „Zurück ins Spiel“, „Deinstallieren …“
      ausgegraut mit Tooltip „Erst wenn das Spiel beendet ist“; Kontextmenü ebenso. A darauf bringt den Toast „Arcadia
      findet kein Fenster …“. · E2E: ja
- [x] **T95-9** Mit angeschlossenem Steam Controller (SISR an), während ein Spiel läuft: Der Steam-Controller-Modus in
      Vorschau, Detailseite und Kontextmenü ist ausgegraut, Tooltip bzw. Hinweis „Gilt erst beim nächsten Start – änderbar,
      wenn das Spiel beendet ist“; „Individueller Mausmodus“ bleibt änderbar. Nach dem Spielende ist der Modus wieder
      änderbar. · E2E: nein (Steam Controller)
- [x] **T95-10** Nach dem Beenden eines Steam- und eines Epic-Spiels stimmen „Zuletzt gespielt“ und die Spielzeit in
      Vorschau und Detailseite (bei Steam spätestens 30 s nach dem Ende); mit der Sortierung „Zuletzt gespielt · neueste
      zuerst“ steht das Spiel vorne. · E2E: nein (echte Spiele)
- [ ] **T95-11** Programm mit nicht lesbarem Pfad, aus Arcadia gestartet: zeigt „Läuft“ und danach wieder „Spielen“.
      Erst prüfbar, sobald ein Launcher ohne eigenes Signal kommt (Steam und Epic melden laufende Spiele selbst, die
      Prozesssuche läuft für sie nicht); bis dahin decken `ProcessGamesTests` und `RunningGamesTrackerTests` es ab.
      · E2E: nein (noch kein Launcher ohne eigenes Signal)
- [ ] **T95-12** Steams Flag prüfen: Während ein Steam-Spiel läuft,
      `Get-ItemProperty HKCU:\Software\Valve\Steam\Apps\<appid> | Select Running` zeigt 1, nach dem Ende 0; auch nach
      einem Absturz (Spiel im Task-Manager beenden). „Läuft seit“ stimmt mit der Startzeit überein. Ergebnis in
      `docs/launchers/steam.md` festhalten. · E2E: nein (echter Launcher)
- [x] **T95-13** CPU von Arcadia neben einem aus Arcadia gestarteten Spiel, 60 s, wie in T103-3: unter 1 %. ·
      E2E: nein (Messung am PC des Owners)
- [x] **T95-14** Arcadia auf dem zweiten Monitor, nicht im Vordergrund: Ein Spiel direkt in Steam bzw. Epic starten
      zeigt nach höchstens 2 s „Läuft“, sein Ende nach höchstens 2 s wieder „Spielen“, ohne dass Arcadia den Fokus
      bekommt. · E2E: nein (echter Launcher, zwei Monitore)

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T95-8 | 2026-10-21 | Claude | Branch `feat/laufende-spiele-anzeigen-94-95`, Linux, Xvfb 1920×1080 `--demo`, Tastatur per xdotool (Pfeile, R, Leertaste gehalten, Leertaste, F, Tippen) | ✅ Screenshots im Projektordner `screenshots/laufende-spiele-94-95/`: Kachel- und Treffer-Badge „Läuft“, Vorschau „Läuft seit 18:19“, „Zurück ins Spiel“ in Button und Footer, Detailseite mit Chip und ausgegrautem „Deinstallieren …“, Kontextmenü, Toast ohne Fenster. „Wird gestartet …“ nicht zu sehen, weil Demo-Spiele nicht starten | Echte Launcher am PC des Owners |
| T95-8 (Vorschau-Button) | 2026-10-21 | Juri | Screenshot des Laufs oben | ❌ „Zurück ins Spiel“ in der Vorschau nicht ganz im Button: verkleinert, stößt aber rechts an die Rundung (der Button hatte keinen Innenabstand) | Innenabstand; „Optionen“ gab sein Wort ab, wenn der Text den Platz brauchte |
| T95-8 (Vorschau-Button) | 2026-10-21 | Juri | Screenshot der Lösung mit „Optionen“ als Symbol | ❌ Lösung gefällt nicht | Buttons untereinander statt nebeneinander |
| T95-8 (Vorschau-Button) | 2026-10-21 | Claude | Branch `feat/laufende-spiele-anzeigen-94-95`, Linux, Xvfb `--demo`, Tastatur per xdotool | ✅ Buttons untereinander in voller Breite: „Zurück ins Spiel“ und „Installieren“ mit Tastatur, „Spielen“ mit Controller in voller Größe ganz im Button, „⧉/R Optionen“ ausgeschrieben darunter; nichts überlappt die Angaben darüber | — |
| T95-1 bis T95-3, T95-11 (Logik) | 2026-10-21 | Claude | `dotnet test`, Linux: `EpicRunningGamesTests`, `SteamRunningGamesTests` (Registry nachgebildet), `ProcessGamesTests`, `RunningGamesTrackerTests` | ⚠️ Start-/Stop-Zeilen, halbe Zeilen, neues Log, Registry-Flag und `RunningAppID`, Prozesse aus dem Spielordner und mit nicht lesbarem Pfad, „Wird gestartet …“ mit 2-min-Grenze grün; echte Launcher nicht | Lauf am PC des Owners |
| T95-13 (Prozesssuche) | 2026-10-21 | Claude | Release-Build, Linux, nachgebildete Prozesstabelle mit 351 Prozessen, 5.000 Spiele | ⚠️ ohne die Windows-Aufrufe je Prozess: Ordner der 5.000 Spiele aufbauen 3,2 ms (nur bei einer neuen Bibliothek), erste Runde 0,5 ms, jede weitere Runde ohne neue Prozesse 40 µs; Windows fragt nur neue Prozesse ab. Läuft für Steam und Epic gar nicht | CPU neben dem Spiel am PC des Owners |
| T95-4, T95-13 | 2026-10-21 | Juri | PC des Owners, Branch `feat/laufende-spiele-anzeigen-94-95` (Stand 25197b1), Debug aus VS Code (Arcadia als Teil des .NET-Host-Prozesses) | ✅ „Zurück ins Spiel“ holt das Spiel nach vorne. CPU laut Task-Manager meist 0–0,5 %, neben dem Spiel Spitzen bis 0,7 %; RAM knapp 200 MB. ⚠️ Debug-Build im .NET-Host, nicht der Release-Build | CPU im installierten Release-Build nachmessen |
| T95-3, T95-14 | 2026-10-21 | Juri | PC des Owners, Stand 25197b1, Debug aus VS Code, Arcadia auf dem zweiten Monitor | ❌ Ein in Steam/Epic gestartetes Spiel erst gesehen, als Arcadia den Fokus bekam; ebenso sein Ende, „Zurück ins Spiel“ dazwischen meldete einen fehlenden Prozess | Im Hintergrund alle Launcher fragen, nicht nur für aus Arcadia gestartete Spiele |
| T95-14 (Logik) | 2026-10-21 | Claude | `dotnet test`, Linux: `RunningGamesTrackerTests` | ⚠️ Jede Runde fragt jeden Launcher und sucht Prozesse, auch ohne aus Arcadia gestartetes Spiel; echte Launcher und CPU nicht | Lauf und CPU-Messung (T95-13) am PC des Owners |
| T95-14, T95-13 | 2026-10-21 | Juri | PC des Owners, Stand 271511b, Debug aus VS Code, Arcadia auf dem zweiten Monitor | ✅ Spiel in Steam/Epic gestartet und beendet: „Läuft“ und wieder „Spielen“ ohne Fokus auf Arcadia. CPU laut Task-Manager unverändert, Sprünge unter 1 %. ⚠️ Debug-Build im .NET-Host | CPU im Release-Build bei der Abnahme |
| T95-1 bis T95-10, T95-14 | 2026-10-24 | Juri | 0.1.0-rc.1 installiert, PC des Owners | ✅ | – |
| T95-11 | 2026-10-24 | Juri | 0.1.0-rc.1 installiert, PC des Owners | ⏭️ Noch nicht prüfbar: Steam und Epic melden laufende Spiele selbst, es gibt noch keinen Launcher ohne eigenes Signal | Bleibt offen, bis ein solcher Launcher kommt |
| T95-13 | 2026-10-24 | Juri | 0.1.0-rc.1 installiert, PC des Owners, neben einem aus Arcadia gestarteten Steam-Spiel, 60 s | ✅ 0,13 % CPU | – |
