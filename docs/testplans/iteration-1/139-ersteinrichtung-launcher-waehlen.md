# #139 Ersteinrichtung: Launcher wählen

- [x] **T139-1** Frische Installation (keine `settings.json`): Statt der Bibliothek erscheint „Welche Launcher soll Arcadia
      nutzen?“. Unter „Gefunden“ stehen die installierten Launcher auf „An“ mit ihrer Spielzahl, unter „Nicht gefunden“
      die übrigen ausgegraut mit „Nicht installiert“; ◀ ▶ tun dort nichts, der Fokus hält trotzdem an ihnen. · E2E: ja
- [x] **T139-2** Rechts oben steht wie in den Launcher-Einstellungen die Vorschau „Launcher-Reihenfolge“ mit den
      eingeschalteten Launchern als Tabs (der fokussierte umrandet); sie folgt jedem Umschalten, ohne Launcher „Kein
      Launcher aktiv – die Bibliothek bleibt leer.“. Darunter der fokussierte Launcher (An · wird Tab N / Aus · bekommt
      keinen Tab, Spiele, Fundort, Such-Kürzel), „Später änderbar“ und „Fertig“. Oben rechts Datum und Uhrzeit wie auf
      jeder Seite. · E2E: ja
- [x] **T139-3** Einen gefundenen Launcher ausschalten, dann „Fertig“ (≡, Enter oder Klick): Nur die eingeschalteten
      erscheinen als Tab, auch nach einem Neustart, und die Ersteinrichtung erscheint nicht wieder. · E2E: ja
- [x] **T139-4** Bestehende Installation mit `settings.json` ohne `ActiveLaunchers` (Version vor #136/#139): Nach dem
      Update erscheint die Ersteinrichtung. · E2E: ja
- [x] **T139-5** `--demo` zeigt sie nicht, alle Launcher sind aktiv, `ActiveLaunchers` wird nicht gespeichert.
      `--first-run` zeigt sie auch mit gespeicherter Liste; mit `--demo --first-run` gilt die Auswahl nur bis zum Beenden.
      · E2E: ja
- [x] **T139-6** B (Esc) tut auf der Seite nichts; alles aus T139-1 bis T139-3 allein mit dem Controller (▲ ▼, ◀ ▶ oder A,
      ≡), mit einer Hand an der Tastatur (W/S, A/D, Enter) und allein mit der Maus (Hover fokussiert, Klick auf den
      Schalter, Klick auf „Fertig“). · E2E: ja
- [x] **T139-7** Kein Launcher eingeschaltet (oder keiner gefunden), „Fertig“: Die Bibliothek zeigt „Kein Launcher aktiv“.
      · E2E: ja

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T139-1 bis T139-3, T139-5 bis T139-7 | 2026-10-23 | Claude | Branch `feat/ersteinrichtung-launcher-waehlen-139`, Debug, Linux in der Cloud mit Xvfb 1920×1080, `--demo --first-run` (Steam und Epic dort „nicht gefunden“) und ohne `--demo` mit frischer `settings.json` (kein Launcher gefunden), Tasten und Klicks über xdotool | ⚠️ Seite wie im Design, W/S, A/D, Esc, Enter, Hover, Klick auf Schalter und „Fertig“, danach nur gewählte Tabs; `--demo` schreibt keine `settings.json`; ohne `--demo` `ActiveLaunchers: []` gespeichert und nach Neustart direkt „Kein Launcher aktiv“; kein echter Controller, keine echten Launcher | Lauf am PC des Owners |
| T139-2 (owner's Feedback) | 2026-10-23 | Claude | wie oben, `--demo --first-run` | ⚠️ Datum neben der Uhrzeit, Vorschau „Launcher-Reihenfolge“ statt Text, folgt dem Abschalten, fokussierter Launcher umrandet; Logos wie in den Einstellungen | Lauf am PC des Owners |
| T139-3 bis T139-5 | 2026-10-23 | Claude | `FirstRunSetupTests` | ✅ Fällig ohne Liste, auch nach Update; speichert, nicht wieder nach Neustart; `--demo` speichert nichts; `--first-run`; fehlgeschlagenes Speichern beendet die Frage trotzdem | – |
| T139-1 bis T139-7 | 2026-10-24 | owner | 0.1.0-rc.1 installiert, PC des Owners | ✅ | – |
