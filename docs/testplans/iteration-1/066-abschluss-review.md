# #66 Abschluss-Review Iteration 1

Nur die Korrekturen aus dem Review; die Befunde selbst stehen im Issue.

- [ ] **T66-1** `arcadia.log` nennt beim Start Arcadias Version, die Windows-Version und ob Arcadia installiert ist;
      kein Pfad im Log enthält den Windows-Benutzernamen (statt `C:\Users\<Name>` steht `%USERPROFILE%`). · E2E: nein (echtes Benutzerprofil)
- [ ] **T66-2** Mit `--demo --demo-games 1000` und `--demo-games 5000` (Release-Build) nennt die Zeile `Bibliothek: …` im Log
      die Zeit vom Prozessstart bis zur gezeigten Bibliothek (Budget < 2 s bei 1.000 Spielen) und den Arbeitsspeicher
      (Budget < 400 MB bei 5.000); beide Werte liegen im Budget, Blättern und Suche bleiben flüssig. · E2E: nein (Messung am PC des Owners)
- [ ] **T66-3** Ist `settings.json` beim Start gesperrt (z. B. in einem Editor exklusiv geöffnet), startet Arcadia mit den
      Standardwerten und überschreibt die Datei nicht; beim nächsten Start ohne Sperre sind alle Einstellungen wieder da. · E2E: nein (Dateisperre unter Windows)
- [ ] **T66-4** „Nach Updates suchen“ ohne Netzwerk endet nach höchstens 30 s mit einer Meldung, statt endlos zu suchen. · E2E: nein (installierter Build ohne Netzwerk)

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T66-1 | 2026-10-23 | Claude | Branch `td/abschluss-review-iteration-1-66`, Linux, Release, Xvfb, `--demo`; Ersetzung per Unit-Test (`LogPrivacyTests`) | ✅ Version, OS und „nicht installiert“ im Log | Windows-Pfade am PC des Owners |
| T66-2 | 2026-10-23 | Claude | dito, Container ohne GPU (Software-Rendering) | ✅ 1.001 Spiele: 2,0 s, 223 MB; 5.001 Spiele: 2,0 s, 237 MB (Einlesen 0,35 s, Anzeigen 0,06 s). Suche 0,2–0,5 ms pro Taste, Sortieren 4–6 ms bei 5.001 Spielen | Messung am PC des Owners |
| T66-3 | 2026-10-23 | Claude | dito, Unit-Test mit exklusiv gesperrter Datei (`SettingsServiceTests`) | ✅ Datei unverändert, kurze Sperre wird abgewartet | Sperre unter Windows am PC des Owners |
