# #167 Cover: Update- und Startanzeige überlappen, wenn der Launcher vor dem Start aktualisiert

- [ ] **T167-1** Ein Steam-Spiel mit Update (Update-Symbol auf dem Cover) mit A starten, Steam lädt erst das Update:
      Das Cover zeigt nur das Abzeichen des Updates („Lädt“ bzw. Prozent) mit Fortschrittsbalken, kein zweites
      Abzeichen darunter; sobald das Spiel läuft, nur „Läuft“. In der Suche (Treffer) ebenso. · E2E: nein (echtes Update im Launcher)
- [ ] **T167-2** Wie T167-1, dabei Vorschau und Detailseite ansehen: „Wird gestartet …“ und der Update-Fortschritt
      sind beide lesbar, nichts überlappt; B geht zurück. · E2E: nein (echtes Update im Launcher)
- [ ] **T167-3** Dasselbe mit einem Epic-Spiel, das ein Update hat. · E2E: nein (echtes Update im Launcher)

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T167-1 | 2026-10-24 | Juri | rc.1, Abschluss-Test #74, Steam (Dispatch) | ❌ „Lädt“ liegt über „Startet“ | #167 |
| T167-1 (nur Cover der Bibliothek) | 2026-10-24 | Claude | Branch `fix/update-und-startanzeige-ueberlappen-167`, Linux, Xvfb 1920×1080 `--demo`; lokal nicht eingecheckt: dem laufenden Demo-Spiel eine Installation „Lädt“ gegeben | ✅ das Cover zeigt nur „Lädt“ mit Balken, „Läuft“ liegt nicht mehr darunter | Echtes Update in Steam/Epic, „Startet“, Suche, Vorschau und Detailseite am PC des Owners |
