# #15 Einstellungsseite mit Kategorien im Header

- [x] **T15-1** In der Bibliothek auf Seite 2 ein Spiel fokussieren, ≡ drücken: Die Einstellungen ersetzen die ganze
      Seite. B führt zurück auf denselben Tab, dieselbe Seite und dasselbe Spiel. Ebenso mit Enter und Esc, mit einem
      Klick auf den Chip und ✕, und von einer Detailseite aus (B führt auf die Detailseite zurück). · E2E: ja
- [x] **T15-2** LB/RB, Q/E und ein Klick auf eine Kategorie wechseln die Kategorie; nach der letzten kommt die erste.
      Der gewählte Tab steht in der Mitte; folgen mehr Kategorien, als Platz haben, fadet die Leiste an diesem Ende aus.
      Den Fadeout erst prüfbar, wenn es genug Kategorien gibt (#136 und später). · E2E: ja
- [x] **T15-3** Steam Controller Support an, Steam beenden: Neben „Controller“ steht das Warnsymbol. Ein Spiel mit
      „Steam Input“ starten (SISR pausiert, nur Info): kein Symbol. Ein Fehler (SISR stürzt wiederholt ab) zeigt das
      rote X. · E2E: nein (Steam, SISR)
- [x] **T15-4** Nur mit dem Controller in „Controller“: ▲ ▼ fokussieren Zeile für Zeile und halten am Ende an, die Zeile
      bleibt im Bild samt Gruppenüberschrift. ◀ ▶ ändern Schalter und Auswahlen sofort, A schaltet um bzw. zählt weiter,
      bei Aktionen („Beheben“, „Aktualisieren“, „SISR installieren“) löst A sie aus. „Mausmodus: Aus“ graut die
      abhängigen Zeilen aus, ihre Werte bleiben. Nach einem Neustart sind alle Änderungen noch da. Nur Einstellungen
      und Aktionen sind Zeilen; Status, „So funktioniert es“, Nachteile und SISR-Version stehen als eigene Boxen im
      Infopanel von „Steam Controller Kompatibilität“, in dieser Reihenfolge unter seiner Beschreibung. · E2E: ja
- [x] **T15-5** Der Footer nennt nur die Tasten der fokussierten Zeile („◀▶ Ändern“, „A Umschalten“, „A Beheben“ …),
      dazu „LB RB Kategorie“ und „B Zurück“; nach einer Taste oder einem Klick die Tasten und die Maus. · E2E: ja
- [x] **T15-6** „Mausmodus“ fokussieren: Das Infopanel zeigt die Beschreibung und darunter in einer Box „So funktioniert
      es“ mit den Tasten und den Grenzen, unten fadet
      es aus, der Footer nennt RS. RS, Umschalt+W/S, Bild auf/ab und das Mausrad scrollen; oben fadet es dann aus. Eine
      andere Zeile beginnt das Infopanel wieder oben. · E2E: ja
- [x] **T15-7** „SISR installieren“ bei laufendem Steam: „Steam beenden?“ erscheint als Dialog über der Seite; B schließt
      nur den Dialog, die Seite bleibt. Während Steam oder SISR starten, lassen sich „Steam Controller Kompatibilität“
      und die Aktionen zu SISR nicht auslösen; ▲ ▼, die übrigen Einstellungen, B und LB/RB gehen weiter. · E2E: nein (SISR, Steam)
- [x] **T15-8** „Über Arcadia“ im installierten älteren Release-Kandidaten: Version, „Nach Updates suchen“, „Jetzt
      aktualisieren“ und „Was ist neu“ (Versionen im Infopanel) sind Zeilen; A auf „Jetzt aktualisieren“ aktualisiert.
      · E2E: nein (Update)
- [x] **T15-9** Mit einer Hand an der Tastatur (W/S, A/D, Leertaste, Esc, Q/E, Umschalt+W/S) und allein mit der Maus
      (Klick auf Zeilen, Pfeile neben den Werten, Kategorien, ✕, Mausrad im Infopanel) ist jede Einstellung erreichbar.
      Die Maus über einer Zeile fokussiert sie, ohne sie zu ändern, und das Infopanel erklärt sie; ruht die Maus,
      während die Zeilen mit Tasten scrollen, bleibt der Fokus bei den Tasten. · E2E: ja
- [x] **T15-10** Die Symbole im Header der Bibliothek öffnen die passende Kategorie: Controller-Warnung und
      SISR-Update „Controller“, Arcadia-Update „Über Arcadia“. · E2E: ja
- [ ] **T15-11** Wählschalter laufen an den Enden um (owner, 2026-10-24, ohne Issue): „Zeigergeschwindigkeit“ auf 1,
      ◀ (Taste A, Klick auf den linken Pfeil): 9; ▶ auf 9: 1. Kein Pfeil ist am Ende ausgegraut. Ein Schalter („An“/„Aus“,
      „Aktiv“ eines Launchers, SISR, Ersteinrichtung) wechselt mit beiden Pfeilen; „Aktiv“ eines nicht installierten
      Launchers bleibt gesperrt. · E2E: ja

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T15-1, T15-2, T15-4 bis T15-6, T15-9 | 2026-10-22 | Claude | Branch `feat/einstellungsseite-mit-kategorien-im-header-15`, Linux, Avalonia headless gerendert 1920×1080, `--demo`-Bibliothek, Controller-Aktionen ins View-Model, Tastatur über `NoteKeyboard`, Klicks über Headless-Eingaben | ⚠️ Seite, Kategorien, Gruppen, Werte, Ausgrauen, Infopanel mit Scrollen und Fades, Footer für Controller und Tastatur, Klick auf Pfeil, Kategorie und ✕ wie erwartet; echte Tasten und echter Controller nicht, Fadeout der Leiste mangels Kategorien nicht | Lauf am PC des Owners |
| T15-4, T15-6, T15-7, T15-9 (Juris erstes Feedback) | 2026-10-22 | Claude | wie oben; für die Boxen „Status“ und „SISR-Version“ lokal „nicht verfügbar“ abgeschaltet | ⚠️ Boxen in der Reihenfolge Beschreibung, Status, So funktioniert es, Nachteile, SISR-Version; ▼ verlässt die nicht verfügbare SISR-Zeile; Mausmodus mit Box „So funktioniert es“; Maus über einer Zeile fokussiert sie, ▼ geht von dort weiter. Echtes SISR-Starten nicht | Lauf am PC des Owners |
| T15-3, T15-7, T15-8 | 2026-10-22 | Claude | wie oben | ⚠️ Unter Linux ist Steam Controller Support „nur unter Windows verfügbar“, kein Update: nur `HealthIssuesTests` (Symbol nur bei Warnung oder Fehler) und `ListFocusTests` | Lauf am PC des Owners |
| T15-1 bis T15-10 | 2026-10-24 | Juri | 0.1.0-rc.1 installiert, PC des Owners | ✅ | – |
| T15-11 | 2026-10-24 | Claude | Branch `feat/wrap-value-selectors`, Linux, Xvfb 1920×1080, Release-Build, `--demo`, Klicks und Taste D über xdotool | ✅ Zeigergeschwindigkeit 5 → ▶×4 → 9 → ▶ → 1 → ◀ → 9, D auf 9 → 1, beide Pfeile an den Enden aktiv; „Bei System-Dialogfenstern“ ◀ zweimal: Aus, An; Launcher-„Aktiv“ mit beiden Pfeilen aktiv. SISR (nur Windows) und Ersteinrichtung nicht angesehen, echter Controller nicht | Lauf am PC des Owners |
