# #66 Abschluss-Review Iteration 1

Nur die Korrekturen aus dem Review; die Befunde selbst stehen im Issue.

- [x] **T66-1** `arcadia.log` nennt beim Start Arcadias Version, die Windows-Version und ob Arcadia installiert ist;
      kein Pfad im Log enthält den Windows-Benutzernamen (statt `C:\Users\<Name>` steht `%USERPROFILE%`). · E2E: nein (echtes Benutzerprofil)
- [x] **T66-2** Mit `--demo --demo-games 1000` und `--demo-games 5000` (Release-Build) nennt die Zeile `Bibliothek: …` im Log
      die Zeit vom Prozessstart bis zur gezeigten Bibliothek (Budget < 2 s bei 1.000 Spielen) und den Arbeitsspeicher
      (Budget < 400 MB bei 5.000); beide Werte liegen im Budget, Blättern und Suche bleiben flüssig. · E2E: nein (Messung am PC des Owners)
- [x] **T66-3** Ist `settings.json` beim Start gesperrt (z. B. in einem Editor exklusiv geöffnet), startet Arcadia mit den
      Standardwerten und überschreibt die Datei nicht; beim nächsten Start ohne Sperre sind alle Einstellungen wieder da. · E2E: nein (Dateisperre unter Windows)
- [x] **T66-4** „Nach Updates suchen“ ohne Netzwerk endet nach höchstens 30 s mit einer Meldung, statt endlos zu suchen. · E2E: nein (installierter Build ohne Netzwerk)
- [x] **T66-5** Wechselt man bei laufendem Arcadia zu einem anderen Programm (z. B. Browser) und zu einem Spiel, nennen die
      Zeilen `Vordergrund: …` in `arcadia.log` nur „Arcadia“, „Spiel“, „kein Spiel“ oder „kein Fenster“, nie den
      Programmnamen; ein Wechsel zwischen zwei Nicht-Spielen schreibt keine neue Zeile. · E2E: nein (echte Fensterwechsel unter Windows)
- [ ] **T66-6** Arcadia mit `--demo` aus `bin/` (nicht installiert) starten und beenden: die Logs liegen in
      `%APPDATA%\Arcadia\logs`, `%LOCALAPPDATA%\Arcadia` gibt es nicht; das Setup installiert danach ohne
      „bereits installiert“. Lagen noch Logs in `%LOCALAPPDATA%\Arcadia\logs` (rc.1), sind sie nach dem Start
      umgezogen. Deinstallieren fragt nach SISR (falls installiert) und nach `%APPDATA%\Arcadia`, jede Frage nennt
      ihre Wartezeit; „Ja“ löscht den Ordner, „Nein“ oder keine Antwort lässt ihn stehen, Arcadia ist in beiden Fällen deinstalliert. · E2E: nein (Setup und Deinstallation unter Windows)
- [ ] **T66-7** SISR mit Marker eingerichtet, Steam läuft: Deinstallieren, SISR-Frage mit „Ja“. Die Frage sagt, dass Steam kurz
      beendet wird; Steam schließt sich und startet wieder, der Eintrag „SISR Marker“ ist in keinem Steam-Konto mehr da
      (auch nicht nach einem Neustart von Steam), SISR, VIIPER und ihre Ordner sind weg. · E2E: nein (Steam und SISR unter Windows)

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T66-1 | 2026-10-23 | Claude | Branch `td/abschluss-review-iteration-1-66`, Linux, Release, Xvfb, `--demo`; Ersetzung per Unit-Test (`LogPrivacyTests`) | ✅ Version, OS und „nicht installiert“ im Log | Windows-Pfade am PC des Owners |
| T66-2 | 2026-10-23 | Claude | dito, Container ohne GPU (Software-Rendering) | ✅ 1.001 Spiele: 2,0 s, 223 MB; 5.001 Spiele: 2,0 s, 237 MB (Einlesen 0,35 s, Anzeigen 0,06 s). Suche 0,2–0,5 ms pro Taste, Sortieren 4–6 ms bei 5.001 Spielen | Messung am PC des Owners |
| T66-3 | 2026-10-23 | Claude | dito, Unit-Test mit exklusiv gesperrter Datei (`SettingsServiceTests`) | ✅ Datei unverändert, kurze Sperre wird abgewartet | Sperre unter Windows am PC des Owners |
| T66-5 | 2026-10-23 | Claude | Branch `td/vordergrund-log-nur-spiel-66`, Linux, nur Build und Tests (unter Linux gibt es keinen Windows-Vordergrund) | ➖ nicht ausführbar | Fensterwechsel am PC des Owners |
| T66-6 | 2026-10-24 | Claude | Branch `fix/einstellungen-beim-deinstallieren`, Linux, Unit-Tests (`LogLocationTests`: Umzug aus beiden alten Ordnern, mit und ohne Installation) | ✅ Umzug und Aufräumen | Setup und Deinstallation am PC des Owners |
| T66-7 | 2026-10-24 | Claude | Branch `fix/einstellungen-beim-deinstallieren`, Linux, Unit-Test (`ControllerSupportRemovalTests`: Marker aus allen Konten, andere Einträge bleiben) | ✅ Marker in 2 von 3 Konten entfernt | Steam schließen und Deinstallation am PC des Owners |
| T66-1 bis T66-5 | 2026-10-24 | Juri | 0.1.0-rc.1 installiert, PC des Owners | ✅ | – |
| T66-6, T66-7 | 2026-10-24 | Juri | 0.1.0-rc.1 installiert, PC des Owners | Nicht im Lauf: Datenordner, Deinstallationsfragen und Marker kamen mit #162/#163 nach rc.1 | Mit dem nächsten Release-Kandidaten |
