# Testplan Iteration 1

Meilenstein „Iteration 1“. Format und Regeln: [README](README.md). Die Häkchen setzt erst der
Abschluss-Test am installierten Release-Kandidaten.

**Abschluss-Test:** Release-Kandidat –, getestet von –, am –.

## #1 Steam-Bibliothek einlesen

- [ ] **T1-1** Der Steam-Tab zeigt alle installierten Spiele aus allen Steam-Bibliotheken (auch
      auf anderen Laufwerken), aber keine Werkzeuge, Redistributables, SDKs oder Soundtracks;
      Demos bleiben drin. · E2E: ja (Manifeste als Testdateien)
- [ ] **T1-2** Stichprobe gegen Steam: Größe, zuletzt gespielt und Spielzeit stimmen; das
      Installationsdatum ist das Erstelldatum des Spielordners. · E2E: ja (Testdateien)
- [ ] **T1-3** Das Update-Symbol erscheint nur bei Spielen, für die Steam ein Update anzeigt; A
      startet das Spiel, Steam installiert vorher das Update. · E2E: nein (echtes Steam)
- [ ] **T1-4** A, Enter und Doppelklick starten ein Steam-Spiel; Steam darf dabei im Hintergrund
      bleiben oder geschlossen sein. · E2E: nein (echtes Steam)
- [ ] **T1-5** Ohne Steam (nicht installiert) gibt es keinen Steam-Tab und keinen Fehler. · E2E: ja
- [ ] **T1-6** Mit `--demo` zeigt der Steam-Tab die echten Spiele statt Demo-Daten. · E2E: ja
- [ ] ~~**T1-7** Der Steam-Tab zeigt auch alle Spiele im Besitz, die nicht installiert sind.~~
      Entfällt in #1: Steams lokaler Cache enthält zurückerstattete Spiele; kommt mit #83. · E2E: ja
- [ ] ~~**T1-8** A auf einem nicht installierten Steam-Spiel öffnet Steams Installationsdialog.~~
      Entfällt in #1, kommt mit #83. · E2E: nein (echtes Steam)
- [ ] **T1-9** Eine Steam-Bibliothek, deren Laufwerk fehlt, erzeugt keinen Fehler; ihre Spiele
      fehlen im Tab und sind wieder da, sobald das Laufwerk zurück ist. · E2E: ja (Testordner)

Die SISR-Pause bei Steam-Spielen prüft G-24.

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T1-1, T1-2 | 2026-10-12 | Claude | PR-Branch, Linux, Scanner-Code gegen Kopien der echten Dateien des Owners (3 Bibliotheken, 77 Manifeste, `localconfig.vdf`), nicht in der App | ✅ 74 Spiele, ausgefiltert genau Steamworks Redistributables, Source SDK Base 2013 und ein Soundtrack; Update bei den 4 Spielen mit `StateFlags` 6/70 | Ansicht in Arcadia, Start und Update am PC des Owners |
| T1-7 | 2026-10-12 | Claude | PR-Branch, Linux, Leser gegen Kopien von `packageinfo.vdf`/`appinfo.vdf` des Owners, nicht in der App | ✅ 74 installierte + 394 nicht installierte Spiele; Abgleich mit der Zahl in Steam steht aus | Owner vergleicht mit seiner Steam-Bibliothek |
| T1-7 | 2026-10-12 | Juri | PR #80, PC des Owners | ❌ Zurückerstattete Spiele (007 First Light, Alchemy Factory) erscheinen: Steams Paket-Cache behält sie | Nicht installierte Steam-Spiele aus #80 entfernt; Steam-Anmeldung #83, diese Iteration |

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
- [ ] **T41-9** Ein Spiel in der Warteschlange zeigt eine Sanduhr statt des Ladekreisels, auf dem
      Cover und in der Vorschau. · E2E: ja (Testdateien)
- [ ] **T41-10** Läuft eine Installation und wird in Epic ein anderes Spiel fortgesetzt oder
      gestartet, zeigt Arcadia das erste als pausiert und nur das zweite als laufend. · E2E: ja (Testdateien)

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T41-1, T41-3 bis T41-6 | 2026-10-10 | nicht festgehalten | Commit `4bad13a`, PC des Owners, echte Epic-Installationen (laut #41 und `CLAUDE.md`) | ✅ | – |
| T41-8 | 2026-10-10 | nicht festgehalten | Commit `4bad13a`, PC des Owners | ❌ Fortschritt bleibt hängen | #58, Backlog |
| T41-9, T41-10 | 2026-10-12 | Juri | PR #79, PC des Owners, echte Epic-Installationen | ❌ Warteschlange hat denselben Kreisel; nach Fortsetzen eines anderen Spiels wirken beide aktiv (Epic bricht das laufende ohne Pause-Zeile ab) | In PR #79 behoben, erneut zu testen |

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

## #67 MainViewModel aufteilen

Refactoring: Installationsverfolgung (`InstallTracker`) und Neueinlesen (`LibraryRefresher`) in
Core mit Unit-Tests, Toasts und Statusleiste als eigene ViewModels. Berührt T41-3 bis T41-6,
T41-8, G-1, G-2, G-17, G-19, G-20 und G-30. Neu ist nur T67-1.

- [ ] **T67-1** Liefert ein Launcher beim Neueinlesen keine Spiele mehr (z. B. sein letztes Spiel
      wurde außerhalb von Arcadia deinstalliert), verschwindet sein Tab, sobald das Fenster wieder
      aktiv wird; war er ausgewählt, ist danach der erste Tab ausgewählt. · E2E: ja

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| G-1, G-20, G-30 | 2026-10-12 | Claude | PR-Branch, Linux mit virtuellem Display, `--demo` | ✅ Tabs, Uhr, Demo-Hinweis als Toast (verschwindet nach 4 s), Tab-Wechsel | Echte Epic-Installation und T67-1 am PC des Owners |

## #68 Launcher als ein Bündel registrieren

Keine neuen Testfälle: Umbau ohne sichtbare Änderung; dass deaktivierte Launcher weder eingelesen
noch überwacht werden, prüfen Unit-Tests (`LauncherRegistryTests`), die Auswahl selbst kommt mit
#15. Berührt G-1, G-2, G-7, G-17 bis G-20, T41-2 bis T41-6 und T42-1.

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| G-1, G-2, G-7, G-31 | 2026-10-12 | Claude | PR-Branch, Linux mit virtuellem Display, mit und ohne `--demo` | ✅ Tab-Reihenfolge wie vorher, Hinweis ohne Launcher | Epic-Start und -Installation am PC des Owners |

## #81 Gleiche Funktionen für Steam und Epic

- [ ] **T81-1** Ein Epic-Spiel, für das Epic ein Update anzeigt, zeigt in Arcadia das Update-Symbol;
      nach dem Update verschwindet der Hinweis. · E2E: ja (Epic-Log und Manifeste als Testdateien)
- [ ] **T81-2** Ein Epic-Spiel, das über Epic gespielt wurde, zeigt „zuletzt gespielt“; das Datum
      bleibt erhalten, auch wenn Epic das alte Log gelöscht hat. · E2E: ja (Testdateien)

- [ ] **T81-3** Eine Steam-Installation (aus Arcadia oder in Steam gestartet) zeigt in Arcadia
      Warteschlange, Fortschritt in %, Pause und am Ende den Hinweis; das Spiel ist danach ohne
      Neustart installiert. · E2E: ja (Manifeste als Testdateien)
- [ ] **T81-4** Laufen zwei Steam-Downloads, zeigt nur der aktive „lädt“, der andere die Sanduhr. · E2E: ja (Testdateien)
- [ ] **T81-5** Abbrechen einer neuen Steam-Installation und Deinstallieren in Steam zeigt Arcadia
      mit Hinweis an; Updates von Steam-Werkzeugen (z. B. Redistributables) erzeugen keinen Hinweis. · E2E: ja (Testdateien)
- [ ] **T81-6** Ein laufendes Update zeigt „Wird aktualisiert“: bei Steam als „lädt“ (nicht als Warteschlange),
      bei Epic ohne Prozentangabe (nicht „Fast fertig“). · E2E: ja (Testdateien)

Die Spielzeit für Epic kommt mit #82 (Epic-Anmeldung).

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T81-1, T81-2 | 2026-10-12 | Claude | PR #80, Linux, Log-Leser gegen Kopien der 4 Epic-Logs des Owners, nicht in der App | ✅ 4 Sitzungen, Update nur bei Terraforming Mars (wie in Epic) | Ansicht in Arcadia am PC des Owners |
| T81-3 bis T81-5 | 2026-10-12 | Juri + Claude | PR #80, PC des Owners: Age of Empires II und System Shock 2 installiert, pausiert, vorgezogen, abgebrochen, deinstalliert; Claude hat die Manifeste mitgelesen, Arcadia lief nicht | ✅ Abläufe als Unit-Tests nachgestellt | Ansicht in Arcadia am PC des Owners |
| T81-6 | 2026-10-12 | Juri | PR #80, PC des Owners, Arcadia: Update von Bloons TD 6 (Steam) und Terraforming Mars (Epic) über „Update starten“ | ❌ Steam „Warteschlange“ statt „lädt“, Epic durchgehend „Fast fertig“; beide Spiele starteten danach | Steam liest jetzt `content_log.txt`, Epic-Updates ohne Prozent; A heißt wieder „Spielen“ (Entscheidung des Owners, kein Launcher kann nur aktualisieren) |
| T81-6 | 2026-10-12 | Juri + Claude | PR #80, PC des Owners: `steam://validate` für Schedule I, Claude hat Manifest und `content_log.txt` mitgelesen | ✅ Log meldet `Update Running` sofort; das Manifest schreibt die Bytes nur zu Beginn und Ende | Unit-Test mit den echten Log-Zeilen |
| G-16 | 2026-10-12 | Juri | PR #80, PC des Owners, The Witcher 3 mit Update | ❌ Hinweis unter „Spielen“ abgeschnitten; Update-Symbol auf der Kachel passt nicht zu den anderen, Badge auf der Detailseite wirkt wie ein Button | Hinweise umbrechen, Kachel-Symbol im Stil der anderen, Detailseite zeigt „Update verfügbar“ als Text unter dem Titel |
| G-16 | 2026-10-12 | Claude | PR #80, Linux unter Xvfb, `--demo`, 1920×1080 | ✅ Symbol und Text wie geplant (Screenshot); Umbruch nur per Einstellung, mit langen Namen nicht gesehen | Ansicht am PC des Owners |

## #86 Refinement-Skill

Keine Testfälle: Arbeitsablauf für Claude ohne Änderung an Arcadia; er wird beim nächsten Issue
im Refinement selbst erprobt.

## #87 Diagnose-Skill

Keine Testfälle: Arbeitsablauf für Claude ohne Änderung an Arcadia; er wird beim nächsten
hartnäckigen Fehler erprobt.

## #88 Launcher-Befunde aus CLAUDE.md auslagern

Keine Testfälle: nur Dokumentation verschoben (`docs/launchers/`, `docs/controller-support.md`),
der Inhalt ist unverändert.

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
- [ ] **G-16** Ein Spiel mit Update zeigt das Update-Symbol, „Spielen“ und darunter den Hinweis,
      dass der Launcher das Update ggf. vorher installiert; A startet das Spiel. · E2E: nein (echter Launcher)

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
| G-21 bis G-24 | 2026-10-09 | nicht festgehalten | PC des Owners (laut `docs/controller-support.md`) | ✅ | – |

## Explorativer Test

Auftrag: –. Zeitrahmen: –. Funde: –.

## Abnahmetests (E2E)

Wird beim Abschluss-Test aus allen Testfällen mit `E2E: ja` erstellt und in der Abnahme-Iteration
umgesetzt.
