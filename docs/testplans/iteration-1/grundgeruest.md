# Grundgerüst (ohne Issue)

Alles, was Arcadia schon kann, aber zu keinem Issue gehört (Grundgerüst aus `docs/roadmap.md`,
Steam Controller Support, Fehlerbehandlung). IDs `G-<n>`. Wo nichts anderes steht, mit
`--demo` und nur mit einem Xbox-Controller.

### Start, Fenster und Daten

- [x] **G-1** Start mit `--demo` zeigt Tabs für alle Launcher mit Spielen; der erste Tab ist
      ausgewählt und ein Spiel fokussiert. · E2E: ja
- [x] **G-2** Start ohne Launcher mit Spielen (ohne `--demo`, frisches System) zeigt den Hinweis
      „Arcadia hat keinen unterstützten Launcher … gefunden“, kein Fehler. · E2E: ja
- [x] **G-3** Das Big-Picture-Layout skaliert ohne abgeschnittene Inhalte: Fenster klein, maximiert,
      F11 Vollbild, 1080p und 4K. · E2E: nein (echte Bildschirme)
- [x] **G-4** Eine kaputte `settings.json` wird als `settings.json.broken` aufbewahrt, Arcadia
      startet mit Standardwerten. · E2E: ja
- [x] **G-5** `arcadia.log` protokolliert Tasten, Gamepad-Tasten und SISR-Start/-Stopp, wird ab
      5 MB rotiert und enthält keine Konto- oder persönlichen Daten. · E2E: ja

### Navigation mit dem Controller

- [x] **G-6** Steuerkreuz und linker Stick bewegen den Fokus im Raster; gedrückt halten wiederholt
      die Bewegung; der fokussierte Spieleintrag bleibt sichtbar. · E2E: ja
- [x] **G-7** LB/RB wechseln den Launcher-Tab; bei vielen Tabs dreht sich die Leiste mit und blendet nur
      dort aus, wo es weitergeht (#127). · E2E: ja
- [x] **G-8** LT/RT blättern die Seiten; die Seitenleiste zeigt die aktuelle Seite. · E2E: ja
- [x] **G-9** Der Controller funktioniert in jedem der vier XInput-Plätze und nach Aus- und
      Wiedereinstecken während Arcadia läuft. · E2E: nein (echte Hardware)
- [x] **G-32** Der Fokusrahmen ist überall vollständig sichtbar und nicht abgeschnitten: Spielkachel,
      „Ansicht“ (Sortierung und Seitengröße), Einstellungen und ihre Knöpfe. · E2E: ja
- [x] **G-33** Kontextmenü in der Bibliothek (Owner, 2026-10-21): Rechtsklick auf eine Kachel öffnet es neben der
      Kachel; ⧉, R und ein Klick auf „Optionen“ in der Vorschau öffnen es über dem Button „Optionen“ (linke und
      untere Kante auf denen des Buttons). · E2E: ja

### Tastatur und Maus

- [x] **G-10** Tasten (#127): WASD/Pfeile, Leertaste, Esc/Rücktaste, Umschalt+Q/E und Strg+(Umschalt+)Tab
      für Tabs, Q/E und Bild ↑/↓ für Seiten, V für „Ansicht“, Enter/F2 für Einstellungen, R für Optionen,
      F11 Vollbild. · E2E: ja
- [x] **G-11** Maus: Klick auf ein Spiel fokussiert, Doppelklick startet, die linke Taste 1 s gehalten öffnet
      die Details (mit Ring); LB/RB/LT/RT, Seitenzahlen,
      Tabs, „Ansicht“, Suche und Einstellungen sind klickbar. · E2E: ja

### Bibliothek

- [x] **G-12** Seitengrößen 10/25/50/100, Standard 25; eine Größe ist nur wählbar, wenn der Tab
      mehr Spiele hat als die nächstkleinere Größe. · E2E: ja
- [x] **G-13** Sortierung nach Name, Installationsdatum, Zuletzt gespielt, jeweils auf- und
      absteigend; Spiele ohne Datum stehen immer am Ende. · E2E: ja
- [x] **G-14** „Ansicht“ (Y) ist komplett per Controller bedienbar, eine Auswahl wirkt sofort,
      B schließt. · E2E: ja
- [x] **G-15** Das Vorschau-Panel zeigt zuletzt gespielt, Spielzeit, Größe, Launcher und
      Speicherort des fokussierten Spiels. · E2E: ja
- [x] **G-16** Ein Spiel mit Update zeigt das Update-Symbol, „Spielen“ und darunter den Hinweis,
      dass der Launcher das Update ggf. vorher installiert; A startet das Spiel. · E2E: nein (echter Launcher)

### Spiele starten

- [x] **G-17** Ein installiertes Epic-Spiel startet, auch wenn Epic nicht läuft: Hinweis „Epic
      wird im Hintergrund gestartet …“, Arcadia bleibt vorne, dann startet das Spiel. · E2E: nein
      (echter Epic Launcher)
- [x] **G-18** Läuft Epic schon, startet das Spiel direkt. · E2E: nein (echter Epic Launcher)
- [x] **G-19** Startet ein Spiel nicht (z. B. Launcher hängt), zeigt Arcadia nach spätestens
      etwa einer Minute „… ist nicht gestartet – bitte in Epic nachsehen“ und bleibt bedienbar. · E2E: nein
- [x] **G-20** Ein Demo-Spiel zeigt beim Starten den Demo-Hinweis statt eines Fehlers. · E2E: ja

### Steam Controller (SISR)

Mit echtem Steam Controller und laufendem Steam. Alle E2E: nein (Steam, SISR, Treiber, UAC).

- [ ] ~~**G-21** Einschalten in den Einstellungen installiert SISR (und usbip-win2 über die
      Windows-Abfrage) mit Ladeanzeige; danach steuert der Steam Controller Arcadia als Gamepad.~~
      Ersetzt durch T85-1 bis T85-4: Installiert wird seit #85 über „SISR installieren“. · E2E: nein
- [x] **G-22** Ein Nicht-Steam-Spiel aus Arcadia erkennt den Steam Controller als Xbox-Controller. · E2E: nein
- [x] **G-23** Ausschalten beendet SISR; der Steam Controller ist wieder Maus und Tastatur, Arcadia
      bleibt im Vordergrund. · E2E: nein
- [x] **G-24** Ein Spiel aus dem Steam-Tab pausiert SISR (keine doppelten Eingaben) und nach
      dem Beenden des Spiels läuft SISR wieder. · E2E: nein
- [ ] ~~**G-25** Pro Spiel lässt sich der Support nur mit dem Controller auf Standard → immer an →
      immer aus schalten, und Arcadia hält sich beim nächsten Start daran.~~
      Ersetzt durch T92-2 bis T92-4 (zwei Modi statt drei); mit dem Controller kommt es mit #10. · E2E: nein
- [x] **G-26** Probleme (Steam läuft nicht, Debug-Schnittstelle aus, Steam-Eintrag fehlt, Treiber
      veraltet) erscheinen in den Einstellungen und als Symbol in der Statusleiste; „Beheben“ löst
      sie. · E2E: nein
- [x] **G-27** Eine neue SISR-Version wird angezeigt und lässt sich aus den Einstellungen
      installieren. · E2E: nein
- [x] **G-28** Beim Beenden von Arcadia wird SISR sauber beendet; es bleibt kein SISR-Prozess
      zurück. · E2E: nein
- [x] **G-29** Ein Xbox-Controller verhält sich gleich, egal ob der Support an oder aus ist und
      ob Steam läuft. · E2E: nein

### Statusleiste und Einstellungen

- [x] **G-30** Die Uhr läuft; die Symbole für Controller-Warnung, SISR-Update und Arcadia-Update
      erscheinen nur bei Bedarf und öffnen die Einstellungen. · E2E: ja
- [x] **G-31** Die Einstellungen (Menü-Taste ≡ / F2) sind komplett per Controller bedienbar, der
      fokussierte Eintrag scrollt ins Bild, B schließt. · E2E: ja

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| G-21 bis G-24 | 2026-10-09 | nicht festgehalten | PC des Owners (laut `docs/controller-support/`) | ✅ | – |
| G-32 | 2026-10-17 | owner | PC des Owners, „Ansicht“ | ❌ Fokusrahmen oben und unten abgeschnitten, auch an anderen Stellen | Fix ohne Issue (Fluent-Button schneidet auf seine Größe zu) |
| G-32 | 2026-10-17 | Claude | Branch `fix/focus-ring-clipped`, Linux, Xvfb 1920×1080 `--demo` | ✅ Kachel (auch oberste Reihe), Seitengröße in „Ansicht“ und erste Einstellungszeile mit vollem Rahmen | Kontrolle am PC des Owners |
| G-32 | 2026-10-17 | owner | PC des Owners, Branch `fix/focus-ring-clipped` | ❌ Sortier-Schalter in „Ansicht“ noch oben und unten abgeschnitten; Kachelrahmen unten dunkler | `ItemsControl` schneidet ebenfalls zu; Schlagschatten lag über dem Rahmen |
| G-32 | 2026-10-17 | Claude | Branch `fix/focus-ring-clipped`, Linux, Xvfb 1920×1080 `--demo` | ✅ Sortier-Schalter mit vollem Rahmen, Kachelrahmen unten so hell wie oben | Kontrolle am PC des Owners |
| G-33 | 2026-10-21 | Claude | Branch `feat/laufende-spiele-anzeigen-94-95`, Linux, Xvfb `--demo`, Maus und Tastatur per xdotool | ✅ Rechtsklick: Menü rechts neben der Kachel; R und Klick auf „Optionen“: Menü über dem Button, unten und links bündig. ⧉ am Controller nicht gesehen (kein XInput unter Linux), läuft aber über denselben Weg wie R | Controller am PC des Owners |
| G-1 bis G-33 (ohne die gestrichenen G-21 und G-25) | 2026-10-24 | owner | 0.1.0-rc.1 installiert, PC des Owners | ✅ | – |
