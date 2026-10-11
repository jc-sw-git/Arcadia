# Testplan Iteration 1

Meilenstein „Iteration 1“. Format und Regeln: [README](README.md). Die Häkchen setzt erst der
Abschluss-Test am installierten Release-Kandidaten.

**Abschluss-Test:** Release-Kandidat –, getestet von –, am –.

## #23 Installer, Self-Update, Release-Pipeline

- [ ] **T23-1** `Arcadia-win-Setup.exe` installiert ohne Adminrechte nach
      `%LOCALAPPDATA%\Arcadia\current\Arcadia.exe`, legt einen Startmenü-Eintrag mit Icon an und
      startet Arcadia. · E2E: ja
- [ ] **T23-2** Ohne neueres Release: „Nach Updates suchen“ meldet, dass Arcadia aktuell ist;
      „Jetzt aktualisieren“ und das Update-Symbol in der Statusleiste fehlen. · E2E: ja
- [ ] **T23-3** Mit neuerem Release: Update-Symbol in der Statusleiste, „Jetzt aktualisieren“ in
      Einstellungen → „Über Arcadia“. Nur mit Controller auslösen: Arcadia schließt, installiert
      ohne Eingabe und startet in der neuen Version. · E2E: ja
- [ ] **T23-4** „Was ist neu“ listet die Punkte aller Releases zwischen laufender und neuester
      Version; B geht zurück. · E2E: ja
- [ ] **T23-5** Einstellungen (`%APPDATA%\Arcadia\settings.json`) und Logs überleben das Update. · E2E: ja
- [ ] **T23-6** Update, während ein Spiel läuft: SISR läuft weiter, eine Pause für das laufende
      Spiel bleibt bestehen, Arcadia erscheint hinter dem Spiel statt davor. · E2E: nein (echtes
      Spiel, SISR und Steam Controller)
- [ ] **T23-7** Deinstallation über die Windows-Einstellungen fragt, ob SISR, VIIPER und
      usbip-win2 mit entfernt werden (Standard nein nach 20 s); „Nein“ lässt sie stehen, „Ja“
      entfernt sie. · E2E: nein (Windows-Einstellungen, UAC von usbip-win2; nicht per Controller bedienbar)
- [ ] **T23-8** Ein Tag `vX.Y.Z` erzeugt ein GitHub-Release mit Setup, Paketen, Update-Feed und
      Release Notes. · E2E: nein (prüft der Release-Workflow selbst)

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T23-2, T23-3, T23-4 | 2026-10-11 | Claude | PR #65, Linux mit virtuellem Display, simuliertes Update | ✅ nur UI, kein echtes Update | Echter Update-Pfad braucht `v0.1.0` und ein Folge-Release |

## #41 Nicht installierte Spiele anzeigen und installieren

- [ ] **T41-1** Nicht installierte Spiele sind ausgegraut und tragen das Installieren-Symbol; die
      Vorschau zeigt „Nicht installiert“, die Hauptaktion heißt „Installieren“. · E2E: ja
- [ ] **T41-2** A auf einem nicht installierten Epic-Spiel öffnet den Installationsdialog im Epic
      Launcher, auch wenn Epic vorher nicht lief. · E2E: nein (echter Epic Launcher)
- [ ] **T41-3** Installation aus Arcadia: „Warte auf Bestätigung“, dann Platz in der Warteschlange,
      Fortschritt in %, am Ende ein Hinweis und das Spiel ist ohne Neustart installiert. · E2E: ja
      (Epic-Log und Warteschlange als Testdateien)
- [ ] **T41-4** Pausieren, Fortsetzen und Abbrechen in Epic zeigt Arcadia an; solange eine
      Installation läuft, ist „Installieren“ gesperrt. · E2E: ja (Testdateien)
- [ ] **T41-5** Eine direkt in Epic gestartete Installation erscheint in Arcadia mit Status. · E2E: ja (Testdateien)
- [ ] **T41-6** Ein außerhalb von Arcadia deinstalliertes Spiel erscheint als nicht installiert,
      sobald das Arcadia-Fenster wieder aktiv wird oder das Spiel ausgewählt wird. · E2E: ja
- [ ] **T41-7** Sortierung nach Installationsdatum: nicht installierte Spiele stehen immer am Ende. · E2E: ja
- [ ] **T41-8** Wird eine pausierte Installation abgebrochen, verschwindet ihr Fortschritt. · E2E: ja (Testdateien)

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T41-1, T41-3 bis T41-6 | 2026-10-10 | nicht festgehalten | Commit `4bad13a`, PC des Owners, echte Epic-Installationen (laut #41 und `CLAUDE.md`) | ✅ | – |
| T41-8 | 2026-10-10 | nicht festgehalten | Commit `4bad13a`, PC des Owners | ❌ Fortschritt bleibt hängen | #58, Backlog |

## #42 Ganze Epic-Bibliothek einlesen

- [ ] **T42-1** Der Epic-Tab zeigt alle Spiele der Epic-Bibliothek. Erlaubte Abweichung: Unreal
      Engine und Unreal Editor for Fortnite fehlen. · E2E: ja (`catcache.bin` als Testdatei)
- [ ] **T42-2** Keine DLCs, Season Pässe, Betas, Soundtracks oder Vorbesteller-Boni im Tab. · E2E: ja
- [ ] **T42-3** Ohne `catcache.bin` (Epic nie gestartet) zeigt der Tab nur die installierten Spiele
      und keinen Fehler. · E2E: ja
- [ ] **T42-4** Titel ohne störende `?` von Markenzeichen; Umlaute bleiben bis #43 bekannt kaputt. · E2E: ja

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T42-1 | 2026-10-10 | nicht festgehalten | Commit `4bad13a`, PC des Owners | ✅ 331 statt 333, Abweichung erklärt | – |

## Grundgerüst (ohne Issue)

Alles, was Arcadia schon kann, aber zu keinem Issue gehört (Grundgerüst aus `docs/roadmap.md`,
Steam Controller Support, Fehlerbehandlung). IDs `G-<n>`. Wo nichts anderes steht, mit
`--demo` und nur mit einem Xbox-Controller.

### Start, Fenster und Daten

- [ ] **G-1** Start mit `--demo` zeigt Tabs für alle Launcher mit Spielen; der erste Tab ist
      ausgewählt und ein Spiel fokussiert. · E2E: ja
- [ ] **G-2** Start ohne Launcher mit Spielen (ohne `--demo`, frisches System) zeigt den Hinweis
      „Arcadia hat keinen unterstützten Launcher … gefunden“, kein Fehler. · E2E: ja
- [ ] **G-3** Das Big-Picture-Layout skaliert ohne abgeschnittene Inhalte: Fenster klein, maximiert,
      F11 Vollbild, 1080p und 4K. · E2E: nein (echte Bildschirme)
- [ ] **G-4** Eine kaputte `settings.json` wird als `settings.json.broken` aufbewahrt, Arcadia
      startet mit Standardwerten. · E2E: ja
- [ ] **G-5** `arcadia.log` protokolliert Tasten, Gamepad-Tasten und SISR-Start/-Stopp, wird ab
      5 MB rotiert und enthält keine Konto- oder persönlichen Daten. · E2E: ja

### Navigation mit dem Controller

- [ ] **G-6** Steuerkreuz und linker Stick bewegen den Fokus im Raster; gedrückt halten wiederholt
      die Bewegung; der fokussierte Spieleintrag bleibt sichtbar. · E2E: ja
- [ ] **G-7** LB/RB wechseln den Launcher-Tab; bei vielen Tabs scrollt die Leiste mit, die
      Randpfeile erscheinen nur, wenn es dort weitergeht. · E2E: ja
- [ ] **G-8** LT/RT blättern die Seiten; die Seitenleiste zeigt die aktuelle Seite. · E2E: ja
- [ ] **G-9** Der Controller funktioniert in jedem der vier XInput-Plätze und nach Aus- und
      Wiedereinstecken während Arcadia läuft. · E2E: nein (echte Hardware)

### Tastatur und Maus

- [ ] **G-10** Tasten: Pfeile, Enter/Leertaste, Esc/Rücktaste, Q/E und Strg+(Umschalt+)Tab für
      Tabs, Bild ↑/↓ für Seiten, V für „Ansicht“, F2 für Einstellungen, F11 Vollbild. · E2E: ja
- [ ] **G-11** Maus: Klick auf ein Spiel fokussiert, Doppelklick startet; LB/RB/LT/RT, Seitenzahlen,
      Tab-Pfeile, „Ansicht“ und Einstellungen sind klickbar. · E2E: ja

### Bibliothek

- [ ] **G-12** Seitengrößen 10/25/50/100, Standard 25; eine Größe ist nur wählbar, wenn der Tab
      mehr Spiele hat als die nächstkleinere Größe. · E2E: ja
- [ ] **G-13** Sortierung nach Name, Installationsdatum, Zuletzt gespielt, jeweils auf- und
      absteigend; Spiele ohne Datum stehen immer am Ende. · E2E: ja
- [ ] **G-14** „Ansicht“ (Y) ist komplett per Controller bedienbar, eine Auswahl wirkt sofort,
      B schließt. · E2E: ja
- [ ] **G-15** Das Vorschau-Panel zeigt zuletzt gespielt, Spielzeit, Größe, Launcher und
      Speicherort des fokussierten Spiels. · E2E: ja
- [ ] **G-16** Ein Spiel mit Update zeigt „Update starten“ statt „Spielen“; A startet das Update
      im Launcher. · E2E: nein (echter Launcher)

### Spiele starten

- [ ] **G-17** Ein installiertes Epic-Spiel startet, auch wenn Epic nicht läuft: Hinweis „Epic
      wird im Hintergrund gestartet …“, Arcadia bleibt vorne, dann startet das Spiel. · E2E: nein
      (echter Epic Launcher)
- [ ] **G-18** Läuft Epic schon, startet das Spiel direkt. · E2E: nein (echter Epic Launcher)
- [ ] **G-19** Startet ein Spiel nicht (z. B. Launcher hängt), zeigt Arcadia nach spätestens
      etwa einer Minute „… ist nicht gestartet – bitte in Epic nachsehen“ und bleibt bedienbar. · E2E: nein
- [ ] **G-20** Ein Demo-Spiel zeigt beim Starten den Demo-Hinweis statt eines Fehlers. · E2E: ja

### Steam Controller Support

Mit echtem Steam Controller und laufendem Steam. Alle E2E: nein (Steam, SISR, Treiber, UAC).

- [ ] **G-21** Einschalten in den Einstellungen installiert SISR (und usbip-win2 über die
      Windows-Abfrage) mit Ladeanzeige; danach steuert der Steam Controller Arcadia als Gamepad. · E2E: nein
- [ ] **G-22** Ein Nicht-Steam-Spiel aus Arcadia erkennt den Steam Controller als Xbox-Controller. · E2E: nein
- [ ] **G-23** Ausschalten beendet SISR; der Steam Controller ist wieder Maus und Tastatur, Arcadia
      bleibt im Vordergrund. · E2E: nein
- [ ] **G-24** Ein Spiel aus dem Steam-Tab pausiert SISR (keine doppelten Eingaben) und nach
      dem Beenden des Spiels läuft SISR wieder. · E2E: nein
- [ ] **G-25** Pro Spiel lässt sich der Support nur mit dem Controller auf Standard → immer an →
      immer aus schalten, und Arcadia hält sich beim nächsten Start daran. · E2E: nein
- [ ] **G-26** Probleme (Steam läuft nicht, Debug-Schnittstelle aus, Steam-Eintrag fehlt, Treiber
      veraltet) erscheinen in den Einstellungen und als Symbol in der Statusleiste; „Beheben“ löst
      sie. · E2E: nein
- [ ] **G-27** Eine neue SISR-Version wird angezeigt und lässt sich aus den Einstellungen
      installieren. · E2E: nein
- [ ] **G-28** Beim Beenden von Arcadia wird SISR sauber beendet; es bleibt kein SISR-Prozess
      zurück. · E2E: nein
- [ ] **G-29** Ein Xbox-Controller verhält sich gleich, egal ob der Support an oder aus ist und
      ob Steam läuft. · E2E: nein

### Statusleiste und Einstellungen

- [ ] **G-30** Die Uhr läuft; die Symbole für Controller-Warnung, SISR-Update und Arcadia-Update
      erscheinen nur bei Bedarf und öffnen die Einstellungen. · E2E: ja
- [ ] **G-31** Die Einstellungen (View-Taste / F2) sind komplett per Controller bedienbar, der
      fokussierte Eintrag scrollt ins Bild, B schließt. · E2E: ja

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| G-21 bis G-24 | 2026-10-09 | nicht festgehalten | PC des Owners (laut `CLAUDE.md`, Abschnitt Steam / Steam Controller) | ✅ | – |

## Explorativer Test

Auftrag: –. Zeitrahmen: –. Funde: –.

## Abnahmetests (E2E)

Wird beim Abschluss-Test aus allen Testfällen mit `E2E: ja` erstellt und in der Abnahme-Iteration
umgesetzt.
