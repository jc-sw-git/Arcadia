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

Vor Iteration 1 abgeschlossen; im Abschluss-Test als Regression mitgeprüft.

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

Vor Iteration 1 abgeschlossen; im Abschluss-Test als Regression mitgeprüft.

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

## Explorativer Test

Auftrag: –. Zeitrahmen: –. Funde: –.

## Abnahmetests (E2E)

Wird beim Abschluss-Test aus allen Testfällen mit `E2E: ja` erstellt und in der Abnahme-Iteration
umgesetzt.
