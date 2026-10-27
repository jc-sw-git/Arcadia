# #167 Cover: Update- und Startanzeige überlappen, wenn der Launcher vor dem Start aktualisiert

- [ ] **T167-1** Ein Steam-Spiel mit Update (Update-Symbol auf dem Cover) mit A starten, Steam lädt erst das Update:
      Das Cover zeigt nur das Abzeichen des Updates („Lädt“ bzw. Prozent) mit Fortschrittsbalken, kein zweites
      Abzeichen darunter; sobald das Spiel läuft, nur „Läuft“. In der Suche (Treffer) ebenso. · E2E: nein (echtes Update im Launcher)
- [ ] **T167-2** Wie T167-1, dabei Vorschau und Detailseite ansehen: „Wird gestartet …“ und der Update-Fortschritt
      sind beide lesbar, nichts überlappt; B geht zurück. · E2E: nein (echtes Update im Launcher)
- [ ] **T167-3** Dasselbe mit einem Epic-Spiel, das ein Update hat. · E2E: nein (echtes Update im Launcher)
- [ ] **T167-4** Ein Spiel mit einem Update, das länger als 2 min lädt, mit A starten: „Wird gestartet …“ (Vorschau)
      bleibt, bis das Spiel läuft. Spiel beenden: Arcadia kommt nach vorne (#94). · E2E: nein (echtes Update im Launcher)
- [ ] **T167-5** Ein Spiel mit Update mit A starten und das Update im Launcher abbrechen, ohne das Spiel zu starten:
      „Wird gestartet …“ endet spätestens 2 min danach, A heißt wieder „Spielen“. · E2E: nein (echtes Update im Launcher)

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T167-1 | 2026-10-24 | owner | rc.1, Abschluss-Test #74, Steam (Dispatch) | ❌ „Lädt“ liegt über „Startet“ | #167 |
| T167-1 (nur Cover der Bibliothek) | 2026-10-24 | Claude | Branch `fix/update-und-startanzeige-ueberlappen-167`, Linux, Xvfb 1920×1080 `--demo`; lokal nicht eingecheckt: dem laufenden Demo-Spiel eine Installation „Lädt“ gegeben | ✅ das Cover zeigt nur „Lädt“ mit Balken, „Läuft“ liegt nicht mehr darunter | Echtes Update in Steam/Epic, „Startet“, Suche, Vorschau und Detailseite am PC des Owners |
| T167-4, T167-5 (Logik) | 2026-10-24 | Claude | `dotnet test` (`RunningGamesTrackerTests`), Linux | ✅ „Wird gestartet …“ bleibt während einer Installation, danach 2 min | Echtes Update am PC des Owners |
