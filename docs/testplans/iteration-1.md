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
| T23-7 | 2026-10-16 | Juri + Claude | PC des Owners, SISR v0.6.2 per Windows-Apps deinstalliert (nicht über Arcadia) | ❌ `%APPDATA%\SISR.exe` (WebView2-Daten) und `%APPDATA%\VIIPER` blieben; Arcadias Deinstallation hätte nur `%APPDATA%\SISR` entfernt | In PR #96 behoben: beide Ordner kommen mit |

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

## #27 Spielstart robust machen

- [ ] **T27-1** Ein Spiel, dessen Start scheitert (z. B. die Exe eines Epic-Spiels umbenannt oder
      gesperrt), zeigt einen Hinweis „… konnte nicht gestartet werden“; Arcadia läuft weiter und ein
      zweiter Start mit A versucht es erneut. · E2E: ja (Fake-Launcher, der beim Start wirft)
- [ ] **T27-2** Startet der Epic Games Launcher nicht innerhalb einer Minute (z. B. ohne Netzwerk
      oder im Task-Manager sofort beendet), zeigt Arcadia „Epic Games Launcher reagiert nicht …“ und
      später „… ist nicht gestartet – bitte in Epic nachsehen“, statt still „bereit“ zu melden. · E2E: nein (echter Epic Launcher)
- [ ] **T27-3** Nach einem unerwarteten Fehler steht er mit Stacktrace in `arcadia.log`, ein Hinweis
      nennt das Log, und Arcadia bleibt mit dem Controller bedienbar. · E2E: ja (Fake-Launcher, der beim Start wirft)

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T27-1, T27-3 | 2026-10-12 | Claude | Branch `claude/project-thread-y4c95v`, Linux, Unit-Tests mit Fake-Launcher (`GameLaunchTests`), nicht in der App | ✅ Fehler gemeldet und geloggt, zweiter Start läuft | Ansicht in Arcadia am PC des Owners |
| T27-2 | 2026-10-12 | Claude | dito, Unit-Test mit Fake-Prozessen und `FakeTimeProvider` | ✅ Stufen `StartingLauncher → LauncherNotReady → NotConfirmed`, URI zweimal geschickt | Echter Epic Launcher am PC des Owners |

## #28 Steam Controller Support serialisieren

Mit echtem Steam Controller und laufendem Steam.

- [ ] **T28-1** Steam Controller Support in den Einstellungen schnell hintereinander mehrmals aus- und
      einschalten: Am Ende entspricht der Zustand dem Schalter (aus = kein SISR-Prozess, an = Controller
      als Gamepad), es bleibt kein zweiter SISR-Prozess. · E2E: nein (echter Steam Controller, SISR)
- [ ] **T28-2** Ein Steam-Spiel aus Arcadia starten (SISR pausiert), während es läuft den Support
      ausschalten, dann das Spiel beenden: SISR startet nicht wieder. Wieder einschalten, während ein
      solches Spiel läuft: Status „Pausiert“, SISR startet erst nach dem Spiel. · E2E: nein (echter Steam Controller, SISR)
- [ ] ~~**T28-3** Ohne Netzwerk Arcadia starten und den Support einschalten (SISR nicht installiert): nach
      spätestens etwa 20 Sekunden eine Meldung in den Einstellungen, Arcadia bleibt bedienbar.~~
      Ersetzt durch T85-6: Einschalten installiert seit #85 nichts mehr, das macht „SISR installieren“. · E2E: nein (SISR-Installation)

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T28-1 bis T28-3 | 2026-10-12 | Claude | Branch `claude/project-thread-y4c95v`, Linux, Unit-Tests mit Fake-SISR, Fake-API und `FakeTimeProvider` (`ControllerSupportServiceTests`), nicht in der App | ✅ überlappende Ticks starten SISR einmal; Ausschalten während Tick oder Pause lässt SISR aus; GitHub-Timeout und abgebrochener Download melden sich; nach 3 Abstürzen in 5 min kein Neustart | Echter Steam Controller am PC des Owners |

## #85 Steam Controller Support erst einschaltbar, wenn alles bereit ist

Mit echtem Steam Controller. Für die Fälle ohne SISR vorher in Arcadia deinstallieren oder
`%LOCALAPPDATA%\SISR` umbenennen.

- [ ] **T85-1** Ohne SISR (oder ohne usbip-Treiber, ohne `.cef-enable-remote-debugging` im
      Steam-Ordner oder ohne „SISR Marker“ in der Steam-Bibliothek) zeigt die Einstellung „SISR installieren“ statt des Schalters. · E2E: ja (Fake-Plattform)
- [ ] **T85-2** Steam läuft: A auf „SISR installieren“ öffnet über den Einstellungen das Modal „Steam beenden?“;
      nur dessen Schaltflächen haben den Fokus. „Abbrechen“ oder B: das Modal schließt, nichts wird
      installiert, Steam läuft weiter; erneutes A fragt wieder. · E2E: ja (Fake-Plattform)
- [ ] **T85-3** Steam läuft, „Steam beenden und installieren“: Steam schließt, SISR und Treiber werden
      installiert (Windows-Abfrage), Steam startet wieder im Hintergrund, die Option ist danach an und
      der Steam Controller steuert Arcadia als Xbox 360 Controller. Nur mit dem Controller. · E2E: nein (echtes Steam, UAC)
- [ ] **T85-4** Steam läuft nicht: „SISR installieren“ installiert ohne Rückfrage; Steam bleibt aus,
      die Meldung „Steam läuft nicht“ mit „Steam starten“ erscheint. · E2E: nein (UAC)
- [ ] **T85-5** Ein Steam-Spiel läuft: „SISR installieren“ installiert nichts und öffnet das Modal
      „Steam-Spiel läuft“ mit dem Namen des Spiels. Spiel beenden, während das Modal offen ist: es wechselt
      von selbst zu „Steam beenden?“. Schließen (B) und die Einstellungen neu öffnen: kein alter Hinweis
      bleibt stehen. · E2E: nein (echtes Steam-Spiel)
- [ ] **T85-6** Ohne Netzwerk „SISR installieren“: nach spätestens etwa 20 Sekunden eine Meldung, die
      Option bleibt aus, Steam läuft wieder, Arcadia bleibt bedienbar. · E2E: nein (SISR-Installation)
- [ ] **T85-7** Windows-Abfrage des Treibers ablehnen: Meldung „… abgelehnt“, die Option bleibt aus,
      Steam läuft wieder. · E2E: nein (UAC)
- [ ] **T85-8** Bei eingeschalteter Option den usbip-Treiber deinstallieren: Die Option bleibt an und
      zeigt das Problem mit „Installieren“. · E2E: nein (Treiber)
- [ ] **T85-9** Unter Linux (bzw. ohne Steam unter Windows) ist die Option ausgegraut und sagt
      „Momentan nur unter Windows verfügbar.“ bzw. „Benötigt Steam.“; A tut nichts. · E2E: ja
- [ ] **T85-10** Option aus, Steam beendet: Einschalten startet zuerst Steam, mit Spinner und
      „Steam wird gestartet …“; erst wenn Steam läuft, startet SISR. Kein Vollbildfenster von SISR
      („Steam läuft nicht“) erscheint. Bis dahin reagieren die Einstellungen nur auf B. · E2E: nein (echtes Steam)
- [ ] **T85-11** Startet Steam beim Einschalten nicht innerhalb von 60 Sekunden (z. B. Steam-Ordner
      umbenannt): Modal „Steam startet nicht“; „Abbrechen“ lässt die Option aus, „Erneut versuchen“
      versucht es noch einmal. Ebenso, wenn Steam läuft, aber bei „Nutzerdaten werden geladen“ hängen
      bleibt: SISR startet dann nicht. · E2E: ja (Fake-Plattform)
- [ ] **T85-12** Option an, dann Steam beenden: SISR endet innerhalb von etwa 2 Sekunden mit, Steam
      startet nicht wieder; die Einstellungen zeigen „Steam läuft nicht“ mit „Steam starten“. Danach
      ausschalten: Steam bleibt aus. Steam selbst starten: SISR startet von allein wieder. · E2E: nein (echtes Steam)
- [ ] **T85-13** Bei keinem Ein- oder Ausschalten (auch mit beendetem Steam) und bei der Einrichtung
      erscheint ein Fenster von SISR (Ersteinrichtung, „Steam läuft nicht“, Marker hinzufügen). Nach der
      Einrichtung gibt es genau einen „SISR Marker“ in der Steam-Bibliothek. · E2E: nein (echtes Steam)
- [ ] **T85-14** Solange Steam, SISR oder VIIPER starten oder beenden (auch von selbst, z. B. nach dem
      Beenden von Steam), zeigt die Einstellung einen Spinner und reagiert nur auf B; der Schalter lässt sich
      erst danach umlegen. Hängt Steam beim Anmelden, bleibt der Schalter bedienbar. · E2E: ja (Fake-Plattform)
- [ ] **T85-15** Mit einem Steam-Konto: „SISR installieren“ legt den „SISR Marker“ ohne Frage an, die Option
      geht an und SISR startet; kein wiederholtes „Anlegen“. Ohne je angemeldetes Konto: die Meldung „kein
      Steam-Konto gefunden“ kommt sofort, Steam wird nicht beendet, nichts wird installiert, die Option bleibt aus.
      Mehrere Konten: T85-19. · E2E: ja (Fake-Plattform)
- [ ] **T85-16** Hängt Steam länger als 30 Sekunden bei „Nutzerdaten werden geladen“: die Einstellungen zeigen
      „Steam meldet sich noch an“ mit „Steam neu starten“; ein laufendes SISR wird dabei nicht beendet. · E2E: ja (Fake-Plattform)
- [ ] **T85-17** Option an, SISR läuft, Standby und wieder aufwachen bzw. kurz das Netzwerk trennen, während ein
      Nicht-Steam-Spiel läuft: der Controller bleibt als Xbox 360 Controller erhalten. · E2E: nein (Standby, echtes Steam)
- [ ] **T85-18** Steam im Offline-Modus starten, Option an: SISR startet, sobald Steam offline bereit ist (falls
      nicht: Befund notieren, Steam schreibt dann vermutlich kein `Success` ins Login-Log). · E2E: nein (echtes Steam)
- [ ] **T85-19** PC mit mehreren Steam-Konten, „SISR installieren“: vor „Steam beenden?“ fragt „Steam-Eintrag für
      SISR anlegen?“ mit allen Konten (zuletzt angemeldetes zuerst und vorausgewählt, Konten mit Eintrag ausgegraut
      und „bereits eingerichtet“), per D-Pad und A wählbar; „Weiter“ nur mit mindestens einem Haken (eingerichtete
      Konten zählen mit: wählt man dann nichts Neues, wird kein Eintrag geschrieben). Danach steht „SISR Marker“
      genau in den gewählten Konten in Steam. Ist das angemeldete Konto nicht dabei, startet SISR nicht und die
      Einstellungen zeigen „Steam-Eintrag für SISR fehlt“ mit „Anlegen“. „Abbrechen“ oder B bricht die
      Installation ab, nichts wird installiert. Nur Konten aus Steams Anmeldeliste erscheinen. Mit nur einem Konto
      kommt die Frage nicht. · E2E: ja
- [ ] **T85-20** Option einschalten, während Steam beim Anmelden hängt: nach einer Minute „Steam meldet sich nicht
      an“ mit „Steam neu starten“ (statt „Steam startet nicht“); der Neustart schaltet die Option danach ein. Die
      Einstellungen während der Minute schließen und wieder öffnen: die Meldung erscheint, sobald der Start
      scheitert, nicht erst beim nächsten Öffnen. · E2E: nein (echtes Steam, hängende Anmeldung)

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T85-10 bis T85-12 | 2026-10-16 | Juri | Branch `feat/steam-controller-setup-and-naming-85-92` (Stand d2375c7), Windows, echtes Steam | ❌ Einschalten ohne Steam zeigte SISRs Vollbildfenster „Steam läuft nicht“; nach Steam beenden und Ausschalten startete Steam | SISR nur mit laufendem Steam, Steam beim Einschalten starten |
| T85-12, T85-13 | 2026-10-16 | Juri | Branch `feat/steam-controller-setup-and-naming-85-92` (Stand 32c4ef1), Windows, echtes Steam | ❌ Nach dem Beenden startete Steam wieder; beim Einschalten ohne Steam fragte SISR jedes Mal, einen Marker hinzuzufügen | SISR ohne Steam hart beenden, auf Anmeldung warten, Marker und Ersteinrichtung selbst |
| T85-12 | 2026-10-16 | Juri | Branch `feat/steam-controller-setup-and-naming-85-92` (Stand 314f649), Windows, echtes Steam | ❌ Manchmal startete Steam nach dem Beenden trotzdem wieder; Log: SISR wurde regulär beendet, während steam.exe noch herunterfuhr | Ohne bereites Steam immer hart beenden |
| T85-12, T85-13 | 2026-10-16 | Claude | Branch `feat/steam-controller-setup-and-naming-85-92`, Linux, Unit-Tests mit Fake-Plattform und Fake-SISR | ✅ Ohne Steam wird SISR beendet statt beendet-mit-Aufräumen, Neustart mit Steam zählt nicht als Absturz, ohne Marker kein SISR-Start, die Einrichtung legt den Marker bei geschlossenem Steam an | Echter Ablauf am PC des Owners |
| T85-10 bis T85-12 | 2026-10-16 | Claude | Branch `feat/steam-controller-setup-and-naming-85-92`, Linux, Unit-Tests mit Fake-Plattform (`ControllerSupportServiceTests`); Modal „Steam startet nicht“ per Xvfb-Screenshot mit lokalem Eingriff | ✅ Steam vor SISR, Timeout schaltet aus, ohne Steam kein SISR-Start und kein Absturzzähler, Ausschalten ohne Steam sendet nichts an Steam | Echter Ablauf am PC des Owners |
| T85-11 | 2026-10-16 | Juri | Branch `feat/steam-controller-setup-and-naming-85-92` (Stand e32a90b), Windows, echtes Steam | ❌ Steam hing bei „Nutzerdaten werden geladen“ (auch ohne Arcadia; laut Steam-Logs antwortete der Steam Controller nicht, erst ein Windows-Neustart half), trotzdem startete SISR und fand keinen Controller | Bereit erst nach abgeschlossener Anmeldung (`steamui_login.txt`) |
| T85-2, T85-5 | 2026-10-16 | Claude | Branch `feat/steam-controller-setup-and-naming-85-92`, Linux, Xvfb 1920×1080, `--demo`, Modal per lokalem Eingriff erzwungen (unter Linux ist die Option ausgegraut) | ✅ Beide Modals erscheinen über den abgedunkelten Einstellungen, Fokus nur auf ihren Schaltflächen; per Unit-Test geprüft sind nur die frische Abfrage des Spiels und `ClearError`, der Wechsel des Modals beim Spielende nicht | Echter Ablauf mit Steam-Spiel am PC des Owners |
| T85-1, T85-3 bis T85-7 | 2026-10-16 | Claude | Branch `feat/steam-controller-setup-and-naming-85-92`, Linux, Unit-Tests mit Fake-Plattform und Fake-SISR (`ControllerSupportServiceTests`), nicht in der App | ✅ Reihenfolge Steam beenden → CEF → SISR → Treiber → Steam starten → an; Steam-Spiel, Timeout, abgelehnter Treiber und nicht beendbares Steam melden sich und lassen die Option aus | Ansicht und echter Ablauf am PC des Owners |
| T85-14 bis T85-18 | 2026-10-16 | Claude | Branch `feat/steam-controller-setup-and-naming-85-92` (zweites Review), Linux, Unit-Tests mit Fake-Plattform und Fake-SISR (`ControllerSupportServiceTests`, `SteamLoginLogTests`, `SteamInstallationTests`, `UsbipPortsTests`), nicht in der App | ✅ Hängende Anmeldung stoppt SISR nicht und warnt nach 30 s mit „Steam neu starten“; Neustart über Arcadia; Marker fürs zuletzt angemeldete Konto; VIIPER-Aufräumen nach jedem Stopp; gleichzeitige Einstellungsänderungen gehen nicht verloren | Echter Ablauf und `usbip port`-Format am PC des Owners |
| T85-19 | 2026-10-16 | Claude | Branch `feat/steam-controller-setup-and-naming-85-92`, Linux, Xvfb 1920×1080 `--demo`, Abfrage mit drei erfundenen Konten per lokalem Eingriff erzwungen; Unit-Tests (`ControllerSupportServiceTests`, `SteamInstallationTests`) | ✅ Abfrage, Vorauswahl, „bereits eingerichtet“, D-Pad und A wie beschrieben; Marker nur für gewählte Konten (nur Unit-Test, Schreiben in echte `shortcuts.vdf` mehrerer Konten ungetestet) | Echter Ablauf mit mehreren Konten am PC des Owners |
| T85-15, T85-19, T85-20 | 2026-10-16 | Claude | Branch `feat/steam-controller-setup-and-naming-85-92` (Nach-Review), Linux, Unit-Tests (`ControllerSupportServiceTests`, `SteamInstallationTests`), nicht in der App | ✅ Kein Konto: Abbruch vor Steam-Beenden und Treiber; Marker nur für ein anderes Konto: SISR startet nicht, „Anlegen“ erscheint; alle Konten eingerichtet: nichts geschrieben; Kontenliste nur aus `loginusers.vdf`. T85-20 nur gebaut, nicht ausgeführt | T85-20 und echte Konten am PC des Owners |
| T85-14 bis T85-18 (VIIPER-Aufräumen) | 2026-10-17 | Juri / Claude | Windows, `usbip port` am PC des Owners bei laufendem SISR (Screenshot); Ausgabe als Unit-Test (`UsbipPortsTests`) übernommen | ✅ Format wie angenommen, der Parser findet Port 01 von `usbip://localhost:3241` | Aufräumen nach einem echten SISR-Abbruch am PC beobachten |

## #92 „Steam Controller Support“ verständlich benennen und beschreiben

- [ ] **T92-1** Die Einstellung heißt „Steam Controller als Xbox 360 Gamepad verwenden“; Beschreibung,
      die beiden Modi und der SISR-Hinweis stehen wie in #92. Kein Text sagt mehr „Steam Controller
      Support“ (Einstellungen, Statusleiste, Vorschau, Deinstallationsfrage). · E2E: ja
- [ ] **T92-2** Die Vorschau zeigt „STEAM CONTROLLER“ mit „Steam Input“ für Steam-Spiele und
      „Xbox 360 Controller (SISR)“ für alle anderen, als Wert ohne Dropdown-Pfeil. · E2E: ja
- [ ] **T92-3** Ein Klick auf den Wert wechselt zwischen den beiden Modi; zurück auf den Startwert
      wird nichts gespeichert (`ControllerSupportOverrides` in `settings.json` leer). · E2E: ja
- [ ] **T92-4** Der Eintrag in der Vorschau fehlt bei ausgeschalteter Option und, bei eingeschalteter,
      ohne eingeschalteten Steam Controller, auch wenn nur der Funk-Dongle steckt; er erscheint
      innerhalb von etwa 2 Sekunden (plus der Zeit, die SISR für den virtuellen Controller braucht) nach
      dem Einschalten des Controllers und verschwindet ebenso nach dem Ausschalten. · E2E: nein (echter Steam Controller)
- [ ] **T92-5** Die Einstellungen sind so lang, dass sie scrollen: die Fußzeile mit D-Pad/A/B bleibt
      unten sichtbar, auch ganz oben und ganz unten gescrollt. · E2E: ja

Mit dem Controller erreichbar wird die Einstellung pro Spiel erst mit #10.

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T92-4 | 2026-10-16 | Juri | Branch `feat/steam-controller-setup-and-naming-85-92` (Stand d2375c7), Windows, Funk-Dongle mit ausgeschaltetem Steam Controller | ❌ Modi in der Vorschau sichtbar, obwohl der Controller aus ist (der Dongle reichte) | Erkennung über SISR statt über die HID-Liste |
| T92-4, T92-5 | 2026-10-16 | Claude | Branch `feat/steam-controller-setup-and-naming-85-92`, Linux, Unit-Test (`ControllerSupportServiceTests`) und Xvfb-Screenshot 1920×1080 `--demo` | ✅ Vorschau folgt dem Running/NoController-Zustand (nur Unit-Test); Fußzeile bleibt unter dem scrollenden Inhalt sichtbar | Echter Controller am PC des Owners |
| T92-3 | 2026-10-16 | Claude | Branch `feat/steam-controller-setup-and-naming-85-92`, Linux, Unit-Test (`ControllerSupportTests`), nicht in der App | ✅ Zurück auf den Startwert entfernt die Abweichung | Ansicht am PC des Owners |

## #8 Ansicht und Auswahl speichern

- [ ] **T8-1** Sortierung und Seitengröße in „Ansicht“ (Y) ändern, Arcadia beenden und neu starten:
      Sortierung, Seitengröße und der zuletzt gewählte Tab (LB/RB oder Klick) sind wie zuvor, der Fokus
      liegt auf dem ersten Spiel von Seite 1. · E2E: ja
- [ ] **T8-2** Sortierung und Seitengröße gelten in allen Tabs gleich; „Ansicht“ sagt „gültig für alle
      Launcher“. Ein Tab mit zu wenigen Spielen zeigt die nächstkleinere erlaubte Größe, ein großer Tab
      danach wieder die gewählte. · E2E: ja
- [ ] **T8-3** Fehlt der zuletzt gewählte Launcher beim Start (z. B. deinstalliert), startet Arcadia im
      ersten Tab ohne Fehler; kommt er wieder, startet Arcadia wieder in ihm. · E2E: ja

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T8-1 | 2026-10-17 | Claude | Branch `feat/save-view-and-tab-8`, Linux, Xvfb 1920×1080 `--demo`, Tastatur (E, V, Pfeile, Enter) | ✅ Nach Neustart EA app, „Zuletzt gespielt · neueste zuerst“, 10 pro Seite, Fokus auf dem ersten Spiel | – |
| T8-3 | 2026-10-17 | Claude | Branch `feat/save-view-and-tab-8`, Linux, Xvfb `--demo`, `LastTabLauncherId` von Hand auf einen unbekannten Launcher gesetzt | ✅ Start im ersten Tab (GOG Galaxy), der gespeicherte Wert bleibt erhalten | Echter fehlender Launcher am PC des Owners |
| T8-1, T8-3 | 2026-10-17 | Claude | Branch `feat/save-view-and-tab-8`, Unit-Tests (`SettingsServiceTests`) | ✅ Neustart, alte `settings.json` ohne die Felder, unbekannte Werte, gleichzeitiges Speichern aus zwei Threads (geht ohne Lock rot) | – |

## #10 Kontextmenü für Spiele

- [ ] **T10-1** ⧉ (View-Taste), Rechtsklick auf ein Cover, „⧉ Optionen“ in der Vorschau und die
      Kontextmenü-Taste der Tastatur öffnen das Menü des Spiels; Hoch/Runter überspringen deaktivierte
      Einträge, B und ⧉ schließen es, ein Klick daneben auch. Das Menü steht rechts neben dem Cover,
      in der rechten Spalte links davon, immer ganz im Fenster. ≡ (Menü-Taste) öffnet die Einstellungen. · E2E: ja
- [ ] **T10-2** Installiertes Spiel: Spielen, Details anzeigen, Zu Favoriten hinzufügen und Artwork
      ändern (beide deaktiviert, „Folgt in einer späteren Version“), In <Launcher> öffnen,
      Trennstrich, Deinstallieren … in Rot. Kein „Controller-Layout anzeigen“ (Owner, 2026-10-18: Steam öffnet es
      nicht zuverlässig, siehe `docs/launchers/steam.md`). Mit Update steht unter
      „Spielen“ der Hinweis, dass der Launcher ggf. vorher aktualisiert; es gibt keinen Update-Eintrag. · E2E: ja
- [ ] **T10-3** Nicht installiertes Spiel: Installieren, Details anzeigen, Zu Favoriten hinzufügen,
      In <Launcher> öffnen. Während einer Installation steht statt „Installieren“ der Status
      (z. B. „Wird installiert · 42 %“), deaktiviert. · E2E: ja
- [ ] **T10-4** A kurz drücken startet das Spiel beim Loslassen. A halten: nach etwa 0,2 s füllt sich
      ein blauer Rahmen um die Vorschau links, nach 1 s öffnet sich die Detailseite, ohne loszulassen.
      Loslassen dazwischen bricht ab, nichts passiert. Enter verhält sich gleich. · E2E: ja
- [ ] **T10-5** Steam-Spiel: „In Steam öffnen“ zeigt die Seite des Spiels in der Steam-Bibliothek. · E2E: nein (echter Steam-Client)
- [ ] **T10-6** Steam-Spiel deinstallieren: Arcadia fragt nach (Fokus auf „Abbrechen“, A dort
      bricht ab); „Deinstallieren“ öffnet Steams eigene Deinstallationsfrage, danach verschwindet das
      Spiel aus der Bibliothek. · E2E: nein (echter Steam-Client)
- [ ] **T10-7** Epic-Spiel: „In Epic Games öffnen“ öffnet die Epic-Bibliothek; „Deinstallieren …“
      sagt „Nur in Epic Games möglich – öffnet Epic Games“ und öffnet die Bibliothek ohne Rückfrage. · E2E: nein (echter Epic-Client)
- [ ] **T10-8** Mit eingeschalteter Option und verbundenem Steam Controller zeigt das Menü
      „Steam Controller: <Modus>“; A wechselt den Modus, das Menü bleibt offen und zeigt den neuen
      Modus, die Vorschau ebenso. Ohne Steam Controller fehlt der Eintrag. · E2E: nein (echter Steam Controller)
- [ ] **T10-9** Liest Arcadia die Bibliothek neu ein, während A gedrückt ist (z. B. wenn es nach dem
      Ende eines Spiels nach vorn kommt), zählt der Druck weiter: Tippen startet das Spiel, Halten
      öffnet die Detailseite, der Rahmen läuft durch. · E2E: nein (der Zeitpunkt des Einlesens lässt sich nicht steuern)

Die Steam-Links `steam://nav/games/details` und `steam://uninstall` sowie
der Epic-Link `com.epicgames.launcher://store/library` sind noch nicht am echten Client geprüft (T10-5
bis T10-7).

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T10-1, T10-2, T10-4 | 2026-10-17 | Claude | Branch `feat/context-menu-and-detail-page-10-11`, Linux, Xvfb 1920×1080 `--demo`, Tastatur (Pfeile, Kontextmenü-Taste, Enter gehalten) | ✅ Menü eines GOG-Spiels mit deaktivierten Einträgen und Begründung; Enter 1,2 s gehalten öffnet (noch) den Platzhalter der Detailseite, der Rahmen füllt sich sichtbar | Controller am PC des Owners |
| T10-1 | 2026-10-17 | Claude | wie oben, Menü an der Kachel | ✅ Menü rechts neben dem Cover in der Mitte, links neben dem Cover in der rechten Spalte | – |
| T10-4 | 2026-10-17 | Juri | PC des Owners, PR #101 | ❌ Der Rahmen bricht manchmal auf den letzten 5–10 % ab | Haltezeit lief ab dem Drücken, die Animation erst ab ihrem (verspäteten) Start |
| T10-4 | 2026-10-17 | Claude | PR #101, Unit-Test (geht vorher rot) und Xvfb `--demo`, Enter 0,93 s gehalten | ✅ Rahmen geschlossen, bevor die Detailseite öffnet | Kontrolle am PC des Owners |
| T10-6 | 2026-10-17 | Claude | wie oben | ✅ Rückfrage mit Fokus auf „Abbrechen“ (Demo-Spiel, ohne echten Launcher) | Steam am PC des Owners |
| T10-4 | 2026-10-17 | Claude | Unit-Tests (`HoldGestureTests`) | ✅ Tippen, Abbrechen, Halten, wiederholte KeyDown-Ereignisse, verspätete Timer (geht ohne Zählung der Drücke rot) | – |
| T10-9 | 2026-10-17 | Claude | Xvfb `--demo`, Debug-Build mit vorübergehendem Einlesen 0,1 s nach jedem Druck auf Enter (wieder entfernt) | ✅ Enter 0,12 s: Spiel startet (Toast); Enter 1,5 s gehalten: Rahmen läuft durch, Detailseite öffnet | – |
| T10-2 | 2026-10-18 | Claude | Xvfb 1745×1000 `--demo`, Kontextmenü-Taste | ✅ Menü ohne „Controller-Layout anzeigen“ | – |

## #11 Spieldetails als Karussell

- [ ] **T11-1** A 1 s halten auf einer Kachel und ⧉ → „Details anzeigen“ öffnen die Detailseite des
      Spiels: Pfadleiste „B Bibliothek / <Launcher> / <Titel>“, Position („3 von 31“) und Sortierung
      oben, das Spiel in der Mitte, links und rechts angeschnitten das vorherige und das nächste,
      abgedunkelt und zum Rand hin weich ausgeblendet, ohne sichtbare Kante. Footer: D-Pad Aktion wählen, A Auswählen, Y Ansicht, LB/RB Launcher,
      LT/RT Spiel, B Zurück. · E2E: ja
- [ ] **T11-2** LT/RT schieben die Seiten animiert nach rechts bzw. links und zeigen das vorherige bzw.
      nächste Spiel in der Sortierung der Bibliothek, über Seitengrenzen hinweg; vor dem ersten steht
      das letzte, nach dem letzten das erste. Schnelles mehrfaches Drücken springt ohne Lücken weiter.
      Ein Klick auf einen Nachbarn oder auf LT/RT am Rand tut dasselbe, ebenso Bild ↑/↓, Umschalt+←/→
      und das Mausrad (eine Raste, ein Spiel; nach unten das nächste). · E2E: ja
- [ ] **T11-3** Ein Tab mit zwei Spielen zeigt das andere links und rechts; mit einem Spiel gibt es
      keine Nachbarn, keine Abdunklung und kein LT/RT. · E2E: ja
- [ ] **T11-4** LB/RB (und ein Klick auf einen Tab) wechseln den Launcher, die Detailseite bleibt offen
      und zeigt das erste Spiel des Tabs. · E2E: ja
- [ ] **T11-5** Y öffnet „Ansicht“ mit dem Hinweis, dass die Detailseite immer ein Spiel zeigt; eine
      andere Sortierung lässt das aktuelle Spiel in der Mitte, Nachbarn und Position folgen der neuen
      Sortierung. · E2E: ja
- [ ] **T11-6** B (und ein Klick auf die Pfadleiste) führt zurück in die Bibliothek, auf die Seite des
      zuletzt gezeigten Spiels mit Fokus darauf. · E2E: ja
- [ ] **T11-7** Schaltflächen wie im Kontextmenü, ohne „Details anzeigen“, in drei Reihen (Owner, 2026-10-21):
      oben „A Spielen“ (fokussiert), „Zu Favoriten“ (deaktiviert), „In <Launcher> öffnen“, „Artwork ändern“
      (deaktiviert); darunter die Einstellungen des Spiels, mit verbundenem Steam Controller
      „STEAM CONTROLLER <Modus>“ (A wechselt) und bei „Individuell“ „INDIVIDUELLER MAUSMODUS“ (ohne beide fehlt die
      Reihe, ↑/↓ überspringen sie); zuletzt allein „Deinstallieren …“ in Rot mit derselben Rückfrage wie im Menü.
      Nichts ragt aus der Karte, auch mit beiden Einstellungen und „Standard (deaktiviert)“. Das D-Pad
      überspringt deaktivierte Schaltflächen; unter den Reihen steht der Hinweis der fokussierten
      Schaltfläche (z. B. „Nur in Epic Games möglich – öffnet Epic Games“). ⧉ öffnet hier kein Menü. · E2E: ja
- [ ] **T11-8** Mit Update: Chip „UPDATE VERFÜGBAR“, „Spielen“ bleibt, darunter „<Launcher> installiert
      das Update eventuell, bevor das Spiel startet.“ Nicht installiert: „Installieren“, abgedunkeltes
      Cover, „Nicht installiert“ unter „Installiert am“, nur die erste Reihe. Während einer Installation
      zeigt die erste Schaltfläche den Fortschritt und ist deaktiviert. · E2E: ja
- [ ] **T11-9** Kennzahlen: Zuletzt gespielt (relativ und mit Datum und Uhrzeit), Spielzeit („Laut
      <Launcher>“), Installiert am (mit Pfad), Größe (mit Laufwerk). Größe und Speicherort stehen nur
      dort, nicht unter dem Titel (Owner, 2026-10-17). · E2E: ja
- [ ] **T11-10** LT/RT bis zur sichtbaren Bewegung bleibt unter 50 ms (Release-Build, PC des Owners). · E2E: nein (Messung auf echter Hardware)

Die Version und Downloadgröße des Updates aus dem Entwurf zeigt Arcadia nicht: kein Launcher meldet
sie bisher.

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T11-1, T11-2, T11-4 bis T11-9 | 2026-10-17 | Claude | Branch `feat/context-menu-and-detail-page-10-11`, Linux, Xvfb 1920×1080 `--demo`, Tastatur | ✅ Seite wie im Entwurf; LT/RT mit Animation (Zwischenbild geprüft), 25× RT und B landen auf Seite 3 mit Fokus auf dem Spiel; Q/E bleiben in der Detailansicht; Sortierung hält das Spiel in der Mitte; Enter gehalten öffnet die Seite; nicht installiertes Spiel und Spiel mit Update | Controller am PC des Owners |
| T11-3 | 2026-10-17 | Claude | Unit-Tests (`CarouselTests`) | ✅ Nachbarn rotieren, zwei Spiele stehen beidseitig, ein Spiel hat keine Nachbarn | Sichtprüfung am PC des Owners (die Demo hat keinen Tab mit 1–2 Spielen) |
| T11-10 | 2026-10-17 | Claude | Release-Build, Linux-Container ohne GPU, Xvfb | ✅ Taste bis fertiges Layout 14–39 ms; Öffnen der Seite 199 ms (erstes Mal) | Messung am PC des Owners |
| T11-8, T11-9 | 2026-10-17 | Claude | wie oben, nach dem Code Review | ✅ Unter dem Titel keine Zeile mehr; installiertes Spiel mit Update und nicht installiertes Spiel geprüft | – |
| T11-2 | 2026-10-18 | Juri | PC des Owners, PR #101, Tastatur | ❌ Spiel lässt sich per Tastatur nicht wechseln (Bild ↑/↓ gab es, aber ohne Hinweis); Wunsch: Umschalt+Pfeile und Mausrad | Umschalt+←/→ und Mausrad ergänzt |
| T11-2, T11-7 | 2026-10-18 | Claude | Xvfb 1745×1000 `--demo`, Umschalt+→, Mausrad runter und hoch, Umschalt+← | ✅ 1 → 2 → 3 → 2 → 1 von 31; untere Reihe nur noch „Artwork ändern“ und „Deinstallieren …“ | Mausrad am PC des Owners |

## #12 Suche über alle Launcher

- [ ] **T12-1** X öffnet die Suche mit Bildschirmtastatur: Kopf „ARCADIA Suche“ mit „Schließen“, Suchfeld
      mit Platzhalter „Spiele suchen …“ (ohne Kürzel-Hinweis), Zeile „Suche in allen Launchern“, in einer Zeile
      die Chips „Alle Launcher“ und je Launcher mit Kürzel sowie rechts „L3 Sortiert nach Relevanz“, mittig der
      leere Zustand „Tippe einen Suchbegriff ein“ mit Grafik in einem runden Schein ohne Kante, die Tastatur (Layout DE, Fokus auf „q“) und unten
      die Legende mit den Controller-Tasten. F oder die Lupe öffnen sie ohne Tastatur, die Legende zeigt dann Maus und Tastatur. · E2E: ja
- [ ] **T12-2** Nur mit dem Controller: D-Pad und linker Stick bewegen den Fokus über die Tasten (hoch/runter
      zur nächstgelegenen Taste, an den Rändern bleibt er stehen), A tippt das Zeichen; Y tippt ein
      Leerzeichen. X löscht ein Zeichen, gehalten (oder A auf „Löschen“ gehalten) löscht es nach kurzer Pause
      eins nach dem anderen. · E2E: ja
- [ ] **T12-3** Treffer kommen aus allen eingeschalteten Launchern, sortiert nach Relevanz: Titelanfang vor
      Wortanfang vor Wortmitte, Akzente und Groß-/Kleinschreibung egal. Kopf „N Treffer in M Launchern“
      bzw. der leere Zustand „Keine Treffer“ mit Grafik; Cover mit Launcher-Logo oben links und Launchername
      darunter. ™, ® und © zählen nicht: „™“ allein findet nichts (kein Batman, kein Hitman), „F1 24“ findet
      „F1® 24“. · E2E: ja
- [ ] **T12-4** Präfix (nur mit echter Tastatur): `/gog holl` sucht nur in GOG, `/gog-xbox holl` in beiden,
      die Chips folgen; Zeile „Per Kürzel gefiltert: …“. `/xyz holl` zeigt in Orange „· Unbekanntes
      Kürzel: /xyz“. · E2E: ja
- [ ] **T12-5** Chips (Maus) und L3-Filter schalten Launcher aus oder ein (der letzte bleibt an), „Alle
      Launcher“ schaltet alle ein. Ohne Präfix bleibt der Text unverändert und die Zeile zeigt „Suche in: …“;
      ein getipptes Präfix wird neu geschrieben, „Alle Launcher“ entfernt es. · E2E: ja
- [ ] **T12-6** R3 (oder Klick auf die Globus-Taste) öffnet das Menü „TASTATURLAYOUT“ mit DE, US, UK, FR und
      ES über den Treffern (nichts überlappt es), das aktive mit Haken; D-Pad und A wählen, die Tasten wechseln
      sofort. B oder R3 schließt erst das Menü. Das Layout gilt nach einem Neustart weiter. · E2E: ja
- [ ] **T12-7** Auf dem fokussierten Treffer: ≡ startet das Spiel (beim Loslassen, nicht installiert:
      Installation), ≡ 1 s halten füllt einen Ring ums Cover und öffnet die Detailseite, ⧉ (Rechtsklick,
      Umschalt+F10) öffnet das Kontextmenü neben dem Cover. Mit der Maus wählt ein Klick den Treffer, ein
      Doppelklick startet ihn, die linke Taste 1 s gehalten füllt den Ring und öffnet die Detailseite. Die Legende
      nennt das. · E2E: ja
- [ ] **T12-8** Detailseite aus der Suche: Pfadleiste „B Suche / <Titel>“, Position „2 von 3“, keine Tabs, kein
      Y und keine LB/RB im Footer. LT/RT rotieren durch die Treffer in ihrer Reihenfolge; LB/RB und Y tun
      nichts. B führt zurück zur Suche mit unverändertem Text, der Fokus steht auf dem zuletzt gezeigten
      Spiel. · E2E: ja
- [ ] **T12-9** B bei ausgefahrener Tastatur fährt sie ein, ein zweites B führt in die Bibliothek zurück
      (zweimal schnell B: direkt zurück), Tab, Seite und Fokus wie vorher; B löscht keine Zeichen. · E2E: ja
- [ ] **T12-10** Echte Tastatur: Tippen schreibt ins Suchfeld (auch Umlaute, Großbuchstaben und Leerzeichen),
      Rücktaste löscht (gehalten wiederholt), die Pfeiltasten bewegen den Fokus durch die Treffer, Enter startet,
      Enter halten öffnet die Details, Esc schließt. In die Bildschirmtastatur kommt man mit ihr nicht. · E2E: ja
- [ ] **T12-11** Wird die Bibliothek bei offener Suche neu eingelesen (z. B. Spiel im Launcher installiert),
      bleiben Text und Fokus, die Treffer folgen. · E2E: nein
- [ ] **T12-12** Bildschirmtastatur ein- und ausfahren: Sie rollt sichtbar (kurz) von unten herein und wieder
      hinaus, die Lasche mittig an ihrer Oberkante fährt mit (X bzw. B, Tastatursymbol und Pfeil auf einer Linie). Ein Klick auf die Lasche fährt sie aus bzw. ein;
      jede Taste der echten Tastatur fährt sie ein; B fährt sie ein, X wieder aus.
      Eingefahren stehen die Treffer im Raster, D-Pad bewegt den Fokus durch sie, A startet, A gehalten öffnet
      die Details; die Legende passt sich an (ausgefahren bzw. eingefahren, Controller bzw. Maus und Tastatur). · E2E: ja
- [ ] **T12-13** Groß/Klein: LB kurz tippen schaltet Großbuchstaben ein (Taste „⇧ Groß“ hervorgehoben, Tasten
      zeigen Großbuchstaben) und bleibt so, nochmal tippen schaltet aus. LB gehalten gilt nur, solange es
      gehalten wird: Buchstaben dabei groß, nach dem Loslassen wieder klein. · E2E: ja
- [ ] ~~**T12-14** RB kurz tippen wechselt auf die Sonderzeichen-Seite (u. a. ! ? & + ( ) [ ] @ % : ; ' " € ½, ohne
      ™, ® und …; Taste zeigt „ABC“) und bleibt dort, nochmal tippen zurück. RB gehalten zeigt sie nur, solange
      es gehalten wird. Die Seite ist für alle Layouts gleich.~~
      Ersetzt durch T129-3 (RB schaltet auf AltGr, die Sonderzeichen-Seite entfällt). · E2E: ja
- [ ] **T12-15** L3 (oder Klick auf „Sortiert nach …“, oder die Taste „Filter“) öffnet „Filter und Sortierung“:
      Launcher als Mehrfachauswahl, Sortierung nach Relevanz (Standard), Name, Installationsdatum und
      Zuletzt gespielt, je auf- und absteigend. Jede Wahl wirkt sofort, der Kopf zeigt „Sortiert nach …“;
      B oder L3 schließt. Eine neue Suche beginnt wieder mit Relevanz. · E2E: ja
- [ ] **T12-16** Mehr Treffer als sichtbar, Tastatur ausgefahren: links und rechts stehen die Nachbarn angeschnitten
      und laufen weich aus (ohne harte Kante und ohne Streifen am Rand, auch bei krummen Fenstergrößen), daneben Schaltflächen „LT“/„RT“ (mit der Maus nur Pfeile), über den Treffern „RS Treffer
      wählen“, der Kopf zeigt „8–14 von 105“. Der rechte Stick bewegt den Fokus einzeln (die Reihe gleitet mit),
      LT/RT gleiten eine ganze Reihe weiter und vom Ende wieder zum Anfang (und umgekehrt). Eingefahren scrollt
      das Raster senkrecht mit verblassten Reihen oben und unten, LT/RT und Bild ↑/↓ blättern um zwei Reihen und
      bleiben am Anfang und Ende stehen (kein Sprung ans andere Ende, wie Mausrad und Bibliothek); Mausrad und
      Klick auf einen angeschnittenen Treffer scrollen ebenso. Eine Scrollleiste zeigt, wo man steht: unter der
      Reihe waagrecht, neben dem Raster senkrecht; Ziehen oder ein Klick auf die Leiste scrollt. · E2E: ja
- [ ] **T12-17** Viele Launcher (mit der Maus sichtbar): Passen die Chips nicht neben „Sortiert nach …“, fallen
      bei allen zugleich zuerst die Kürzel weg, dann werden die Namen gekürzt („Ubis…“), zuletzt bleibt nur das
      Logo; wird es eng vor der Sortierung, erscheint dort ein Trennstrich. · E2E: nein
- [ ] **T12-18** Wechsel zwischen Controller und Maus: Bei ausgefahrener Tastatur und Controller-Legende wirkt
      schon der erste Klick auf eine Taste oder die Lasche; die Legende wechselt dabei auf Maus und Tastatur,
      die Controller-Zeichen auf den Tasten und neben „Schließen“ verschwinden, die Tastatur bleibt stehen.
      Ein Druck auf dem Controller holt die Controller-Legende zurück. · E2E: ja
- [ ] **T12-19** Fenster mit anderem Seitenverhältnis als 16:9 (breit im Fenster- oder Vollbild-Fenstermodus, und
      hoch): Legende und Bildschirmtastatur der Suche reichen bis an die Fensterränder, ebenso die Linie über dem
      Footer der Bibliothek und die Abdunklung hinter Dialogen; neben der Suche schimmert nichts von der Bibliothek
      durch. · E2E: nein

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T12-1, T12-3, T12-4, T12-6, T12-10 | 2026-10-18 | Claude | Branch `feat/search-across-launchers-12`, Linux, Xvfb 1920×1080 `--demo`, Fenster 1600×900, Tastatur per xdotool, Klick auf Globus | ✅ „holl“ 3 Treffer in 3 Launchern, „e“ 112 mit „+105 weitere“, `/gog-xyz e` nur GOG plus Warnung, UK gewählt und nach Neustart noch aktiv, „ezzzz“ „Keine Treffer“ | Controller (R3, LT, ≡) am PC des Owners |
| T12-7, T12-8 | 2026-10-18 | Claude | wie oben, Enter halten, Bild ↓, Esc, Umschalt+F10 | ✅ Ring füllt sich, Detailseite „Suche / Hollow Kingdom II“ „2 von 3“ ohne Tabs und Y, Bild ↓ zum nächsten Treffer, Esc zurück mit Fokus darauf, Kontextmenü neben dem Cover | Controller am PC des Owners |
| T12-2, T12-3 | 2026-10-18 | Juri | PC des Owners, PR #104 (Rough-Test) | ❌ Filter per „/“ zu prominent, keine Sortierung, Platzhalter irreführend, kein Groß/Klein, keine Sonderzeichen, echte Tastatur kommt in die Bildschirmtastatur, Löschen ohne Wiederholung, „Suchen“-Taste sinnlos | Umbau nach Rückfragen im Thread (T12-12 bis T12-16 neu) |
| T12-1, T12-2, T12-5 bis T12-10, T12-12 bis T12-16 | 2026-10-18 | Claude | Branch `feat/search-across-launchers-12` nach dem ersten Umbau, Linux, Xvfb 1920×1080 `--demo`; Gamepad nur nachgestellt (lokale Strg+Alt-Tasten, die `HandleGamepad` aufrufen, nicht eingecheckt), echte Tastatur und Maus per xdotool | ✅ X mit Tastatur und Legende, Lupe ohne; X 1,3 s gehalten löscht „6epp“ ganz; LT tippen: Großbuchstaben bleiben; RT Sonderzeichen; L3: GOG aus, „Name Z–A“, 82 Treffer; RB 8–14, LB zweimal 99–105; ≡ 1,4 s: Detailseite „1 von 82“, B zurück; Pfeil ↓: zwei Reihen ohne Tastatur, RB danach blendet sie nur ein; Enter startet (Demo-Hinweis), Enter 1,4 s: Detailseite, Umschalt+F10 Menü; Klick auf den Xbox-Chip: „holl“ 2 Treffer, Text unverändert; LT gehalten + A: „Q“, danach „q“; R3-Menü, B, B | Echter Controller (Stick, LT gehalten, L3, ≡) am PC des Owners |
| T12-6, T12-9, T12-12, T12-14, T12-16 | 2026-10-18 | Juri | PC des Owners, PR #104 (zweiter Rough-Test) | ❌ Layout-Menü von Treffern überdeckt, Blättern in zwei Reihen seltsam, Sortierung nicht in der Launcher-Zeile, leere Zustände „schweben“, unnötige Sonderzeichen, RT nur Umschalter, ohne Tastatur nur per Maus bedienbar, „™“ findet Batman/Hitman | Zweiter Umbau (T12-17 neu) |
| T12-1, T12-3, T12-6, T12-9, T12-12, T12-14, T12-16, T12-17 | 2026-10-18 | Claude | Branch nach dem zweiten Umbau, Linux, Xvfb 1920×1080 `--demo`; Gamepad nachgestellt wie oben (nicht eingecheckt), bis zu 12 zusätzliche Demo-Launcher lokal nachgestellt, Tastatur und Maus per xdotool, Animation per ffmpeg mit 30 Bildern/s aufgenommen | ✅ Layout-Menü über den Treffern; Tastatur rollt in etwa 5 Bildern herein; RB gleitet zu 9–15; B fährt ein, Raster mit verblassten Reihen, RB 15–28; B, B zurück in die Bibliothek; Klick auf die Lasche fährt aus, ohne Legende; „xyzq“ zeigt „Keine Treffer“; Chips mit 6 bis 18 Launchern wie beschrieben; `dotnet test` deckt ™/®/© ab | Echter Controller am PC des Owners |
| T11-1, T12-1, T12-3, T12-12, T12-16 | 2026-10-19 | Juri | PC des Owners, PR #104 (dritter Rough-Test) | ❌ Ausblendung der Trefferzeile und der Nachbarn auf der Detailseite beginnt mit harter Kante, X/B und Tastatursymbol in der Lasche nicht auf einer Linie, Schein der leeren Zustände oben hart abgeschnitten | Verläufe, Lasche und Schein überarbeitet |
| T11-1, T12-1, T12-12, T12-16 | 2026-10-19 | Claude | Branch nach dem dritten Umbau, Linux, Xvfb 1920×1080 `--demo`, Fenster 1600×900; Gamepad nachgestellt wie oben (nicht eingecheckt) | ✅ Trefferzeile und Detailseite blenden ohne Kante aus, Nachbarn der Detailseite gleichmäßig abgedunkelt (auch nach RT); Schein hinter der Grafik ohne Kante (mit angehobenem Kontrast geprüft); X/B, Tastatursymbol und Pfeil auf einer Linie | Sichtprüfung am PC des Owners |
| T12-3, T12-16 (Leistung) | 2026-10-19 | Claude | Branch nach dem Code-Review (P1/P2), Linux-Container, Xvfb mit Software-Rendering, Release-Build, Demo lokal auf 5.004 bzw. 1.001 Spiele hochgesetzt, Gamepad nachgestellt (nicht eingecheckt); Zeit von der Eingabe bis zum fertigen Layout bzw. bis zum übernächsten Frame. `GameSearch` allein mit 5.000 Titeln (Ø 32 Zeichen) per Benchmark | ✅ Suche je Taste vorher 25–32 ms (Spitzen 66 ms), jetzt 0,3–0,8 ms, Vorbereitung beim Öffnen und Rescan einmal 10–25 ms. In der App bei 5.000 Spielen (Median / 90 %): RS in der Reihe vorher 50–125 ms, jetzt 6 / 13 ms; D-Pad im Raster vorher 120–200 ms, jetzt 22 / 43 ms (ein Ausreißer 84 ms); Tippen vorher 60–140 ms, jetzt 26 / 30 ms; X gehalten löscht alle 60 ms ohne verworfene Schritte; Detailseite aus der Suche drehen 14 / 21 ms (wie #101). Offen: der erste Buchstabe nach dem Öffnen braucht einmalig 0,35 s (vorher 0,43 s), vor allem fürs erste Bauen der Kacheln | Gegenmessung am PC des Owners mit echter Bibliothek |
| T12-7, T12-12, T12-13, T12-16 | 2026-10-19 | Juri | PC des Owners, PR #104 (vierter Rough-Test) | ❌ Bei Controller-Legende wirkt der erste Klick auf eine Taste oder die Lasche nicht; die Legende verschwindet bei Maus und die Tastatur rutscht; Treffer blättern mit LB/RB, Detailseite und Bibliothek mit LT/RT; je nach Fenstergröße 1 px Streifen am Rand der seitlichen Ausblendung | Legende immer da, Treffer mit LT/RT, Groß/Sonderzeichen mit LB/RB, Maustaste halten für Details, Verläufe über den Rand (T12-18 neu) |
| T12-1, T12-7, T12-12, T12-13, T12-14, T12-16, T12-18, G-11 | 2026-10-19 | Claude | Branch nach dem vierten Umbau, Linux, Xvfb 1920×1080 `--demo`, Fenster 1600×900 und 7 krumme Größen (1111×625 bis 1711×963) mit Skalierung 1, 1,25 und 1,5; Gamepad nachgestellt wie oben (nicht eingecheckt), Maus und Tastatur per xdotool | ✅ Mit Controller-Legende wirkt der erste Klick („ABC“, dann „e“), Legende wechselt auf Maus/Tastatur, Tastatur bleibt stehen, Zeichen auf den Tasten weg; RB schaltet die Sonderzeichen; Klick auf die Lasche fährt ein, Legende für das Raster; Bild ↓ blättert 15–28; Maustaste 1,4 s gehalten auf einem Treffer und einem Cover der Bibliothek öffnet die Details (Ring sichtbar), ein Klick wählt nur aus. Den Streifen am Rand konnte ich hier nicht nachstellen (Randspalte bei allen Größen gleich der Spalte daneben, vorher wie nachher) | Sichtprüfung am PC des Owners, Streifen bei seiner Fenstergröße |
| T12-16, T12-19 | 2026-10-19 | Juri | PC des Owners, PR #104 (fünfter Rough-Test), Vollbild-Fenstermodus | ❌ Bild ↑/↓ springt im Raster ans andere Ende, das Mausrad nicht; keine Scrollleiste; Footer der Suche links und rechts abgeschnitten (nicht mit F11) | Raster ohne Umlauf, Scrollleiste, Bänder bis an den Fensterrand (T12-19 neu) |
| T12-16, T12-19 | 2026-10-19 | Claude | Branch nach dem fünften Umbau, Linux, Xvfb 1920×1080 `--demo`, Fenster 1900×800 (breit) und 960×1000 (hoch); Gamepad nachgestellt wie oben (nicht eingecheckt); `HitWindowTests` für Raster-Enden und Scrollleiste | ✅ Legende, Tastatur, Footer-Linie und Abdunklung der Einstellungen reichen im breiten Fenster bis an den Rand, im hohen bis nach unten; neben der Suche keine Linie der Bibliothek; Scrollleiste unter der Reihe und neben dem Raster sichtbar (dünn, wie in der Bibliothek). Ziehen der Leiste nur mechanisch geprüft | Sichtprüfung am PC des Owners im Vollbild-Fenstermodus |

## #102 Ausfaden am Rand des Listings

- [ ] **T102-1** Ein Tab, dessen Seite nicht ins Raster passt (z. B. 31 Spiele mit 50 pro Seite): ganz oben
      faden nur unten die angeschnittenen Kacheln aus, in der Mitte oben und unten, ganz unten nur oben;
      die erste bzw. letzte Reihe ist dann vollständig sichtbar. Der Effekt blendet weich ein und aus. · E2E: ja
- [ ] **T102-2** Passt die Seite ganz ins Raster (z. B. 10 pro Seite), gibt es kein Ausfaden. · E2E: ja
- [ ] **T102-3** Die fokussierte Kachel liegt mit D-Pad nie im ausgeblendeten Bereich, ihr Rahmen bleibt
      voll sichtbar. Mausrad-Scrollen schaltet das Ausfaden genauso. · E2E: ja
- [ ] **T102-4** Kein Streifen unverblasster Cover am äußersten Rand des Ausfadens, auch nicht bei einer
      Fenstergröße, bei der die Oberfläche skaliert wird (z. B. 1745×1000 oder 125 % Windows-Skalierung). · E2E: ja

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T102-1, T102-3 | 2026-10-17 | Claude | Branch `feat/context-menu-and-detail-page-10-11`, Linux, Xvfb 1920×1080 `--demo`, GOG 31 Spiele, 50 pro Seite, Pfeiltasten | ✅ Oben nur unten, Mitte beidseitig, unten nur oben; Fokus jeweils frei vom Ausfaden | Mausrad und Controller am PC des Owners |
| T102-4 | 2026-10-18 | Juri | PC des Owners, PR #101 | ❌ Am unteren Rand 1–2 px Cover ohne Ausfaden | Bei skaliertem Fenster nachgestellt (Xvfb 1745×1000): 1 px Spalt zwischen Ausschnitt des Rasters und Verlauf |
| T102-4 | 2026-10-18 | Claude | Xvfb 1745×1000 `--demo`, oben und unten vergrößert | ✅ Kein Streifen mehr, der Verlauf reicht 2 px über das Raster hinaus | Kontrolle am PC des Owners |

## #115 CI: Installer seltener hochladen

Keine Testfälle: nur die CI geändert; geprüft wird sie durch ihre eigenen Läufe (PR ohne Upload, `main` mit Upload).

## #86 Refinement-Skill

Keine Testfälle: Arbeitsablauf für Claude ohne Änderung an Arcadia; er wird beim nächsten Issue
im Refinement selbst erprobt.

## #87 Diagnose-Skill

Keine Testfälle: Arbeitsablauf für Claude ohne Änderung an Arcadia; er wird beim nächsten
hartnäckigen Fehler erprobt.

## #88 Launcher-Befunde aus CLAUDE.md auslagern

Keine Testfälle: nur Dokumentation verschoben (`docs/launchers/`, `docs/controller-support.md`),
der Inhalt ist unverändert.

## #113 Steam-Tab zeigt Spiele, die Steam nicht mehr listet

- [ ] **T113-1** Steam-Tab am PC des Owners: Half-Life 2: Episode One, Episode Two, Lost Coast und
      Plan B: Terraform fehlen, Half-Life 2 ist da. Die Anzahl installierter Spiele stimmt mit Steams
      Bibliothek (Filter „Installiert“) überein. · E2E: nein (echte Steam-Bibliothek mit verwaisten Manifesten)

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T113-1 | 2026-10-20 | Juri | PC des Owners, `main` | ❌ 80 statt 78 Spiele, die vier oben zu viel | #113 |
| T113-1 | 2026-10-20 | Claude | Juris Manifeste nur gelesen (83 in 3 Bibliotheken), Regel mechanisch nachgezählt, App nicht gestartet | ⚠️ Die vier haben als einzige `LastOwner 0` und fallen weg; 83 − 3 Werkzeuge − 4 = 76, Steam nennt 78 | Zählweise in Steam mit Juri klären, Lauf am PC des Owners |

## #64 Controller als Maus nutzen (Mausmodus per Back + Start)

- [ ] **T64-1** Back + Start gehalten, Arcadia vorne: ab 0,2 s füllt sich der Rand des Primärmonitors in der
      Akzentfarbe von oben Mitte über beide Seiten bis unten Mitte, nach 3 s leuchtet er kurz auf, oben in der
      Mitte steht 2 s „Mausmodus an“, der Controller vibriert kurz; danach bleibt nichts stehen. Nochmal 3 s
      halten: „Mausmodus aus“, mit demselben Rand, ebenso bei jedem weiteren Umschalten. Früher loslassen: der Rand
      läuft schnell zurück, nichts schaltet. · E2E: nein
- [ ] **T64-2** Wie T64-1, aber mit dem Explorer oder dem Epic Games Launcher vorne (Arcadia im Hintergrund):
      Umschalten in beide Richtungen geht, das Overlay nimmt weder Fokus noch Klicks (das Fenster vorne bleibt
      aktiv, ein Klick neben Rand und Text trifft das Fenster darunter); kein Eintrag in Taskleiste und Alt+Tab. · E2E: nein
- [ ] **T64-3** Im Mausmodus in fremden Fenstern: linker Stick bewegt den Zeiger (Totzone, langsam anfahrend),
      RT oder A klickt links, gehalten zieht es (Datei im Explorer verschieben), LT oder X klickt rechts, der
      rechte Stick scrollt senkrecht und waagrecht, Steuerkreuz sind Pfeiltasten (gehalten wiederholt), B ist
      Esc, RB Tab, LB Umschalt+Tab, Menu Enter. Arcadia reagiert dabei auf nichts davon. · E2E: nein
- [ ] **T64-4** L3 antippen: Zeiger ein Drittel so schnell, nochmal antippen: wieder normal. Kurz antippen, während
      der Stick bewegt wird, schaltet genauso um. L3 halten (ab 0,5 s) und den Stick bewegen: langsam, solange
      gehalten, danach normal. · E2E: nein
- [ ] **T64-5** View gehalten holt Arcadia aus dem Mausmodus nach vorne (auch aus dem minimierten Zustand, siehe T64-13). Mit „Mausmodus beenden,
      wenn Arcadia im Vordergrund ist“ (Standard) endet der Mausmodus dabei mit „Mausmodus aus“; in Arcadia von Hand
      eingeschaltet bleibt er an und bedient Arcadia wie eine Maus. Option aus: er bleibt auch beim Wechsel an.
      Die Option gilt nur bei „Von Hand“ und ist sonst abgeblendet. · E2E: nein
- [ ] **T64-6** „Mausmodus“ auf „Automatisch“: Wechsel per Alt+Tab zum Explorer → „Mausmodus an“ ohne Rand und
      ohne Vibrieren; Arcadia wieder vorne → „Mausmodus aus“. Ein Spiel aus der Bibliothek starten (auch direkt im
      Launcher gestartet) → bleibt aus; erscheint erst der Epic-Launcher und dann das Spiel, geht er mit dem Spiel
      wieder aus. Im Explorer ⧉ + ≡ gehalten → „Mausmodus aus“, er bleibt aus, bis Arcadia wieder vorne war. In einem
      Spiel mit ⧉ + ≡ eingeschaltet bleibt er an; der Text sagt dann „das Spiel sieht die Eingaben weiterhin“. · E2E: nein
- [ ] **T64-7** Einstellungen → Controller → Box „Mausmodus“: „Mausmodus“ wechselt mit links/rechts oder A zwischen
      „Aus“, „Von Hand“ (Standard) und „Automatisch“, der Text darunter erklärt die Wahl. Haltedauer 1 und 9 s und Zeigergeschwindigkeit 1 und 9 wirken sofort;
      mit dem Controller links/rechts ändern, A zählt hoch und fängt nach 9 wieder bei 1 an. Alles bleibt
      nach einem Neustart erhalten, der Mausmodus selbst nicht (Arcadia startet immer im normalen Modus). · E2E: ja
- [ ] **T64-8** „So funktioniert's“ öffnet sich mit A, zeigt die Belegung und die Grenzen (UAC, Administrator-
      Fenster, exklusives Vollbild), B schließt es, der Fokus steht wieder auf
      „So funktioniert's“. · E2E: ja
- [ ] **T64-9** Zwei Controller: jeder schaltet mit Back + Start um (vibriert nur der, der umschaltet), beide
      bewegen den Zeiger. · E2E: nein
- [ ] **T64-10** Normaler Modus in Arcadia: View (Kontextmenü) und Menu (Einstellungen) wirken beim Loslassen
      oder nach 0,2 s allein gehalten; ≡ 1 s gehalten auf einem Suchtreffer öffnet weiter die Details. Beide
      zusammen gedrückt öffnen nichts. Alle anderen Tasten verhalten sich wie bisher. · E2E: ja
- [ ] **T64-11** Maus bewegen oder eine Taste drücken: „Ansicht“, „Filter und Sortierung“, Einstellungen, die
      SISR-Dialoge und „So funktioniert's“ zeigen oben rechts ein ✕, ein Klick darauf schließt nur diesen Dialog.
      Nach einer Controller-Taste ist das ✕ wieder weg. „So funktioniert's“ hat keine Schaltfläche „Schließen“
      mehr; A oder B schließt es. · E2E: ja
- [ ] **T64-12** „Mausmodus“ auf „Aus“: ⧉ + ≡ und der Fokuswechsel tun nichts, alle Zeilen der Box sind abgeblendet
      und behalten ihre Werte; war der Mausmodus an, endet er sofort mit „Mausmodus aus“. Wieder „Von Hand“ oder
      „Automatisch“: alles wirkt wie vorher eingestellt. · E2E: ja
- [ ] **T64-13** „Mit ⧉ zurück zu Arcadia“ (Standard: an, 1 Sekunde), Arcadia im Hintergrund (Explorer, Launcher,
      ein Spiel; mit und ohne Mausmodus): View gehalten zeigt ab 0,2 s oben „Zurück zu Arcadia“, dessen Rahmen sich
      über die Haltedauer füllt, dann kommt Arcadia nach vorne, ohne dass sich das Menü öffnet. Früher loslassen:
      der Rahmen läuft zurück. „Sofort“: kurzes Drücken genügt, ohne Fenster. Back + Start löst es nie aus. Aus:
      View tut im Hintergrund nichts. In Arcadia öffnet View weiter das Menü. · E2E: nein
- [ ] **T64-14** Steam läuft (Steam Controller oder Xbox-Pad mit Steam Input), „Steams Desktop-Belegung ignorieren“
      (eigene Box unter „Zurück zu Arcadia“, wirkt auch bei Mausmodus „Aus“) an: Back + Start im Explorer öffnet kein Alt+Tab-Fenster mehr, im Mausmodus kommen nur Arcadias Zeiger und
      Tasten an, in Arcadia bewegt das Steuerkreuz den Fokus nur einmal. Echte Maus und Tastatur gehen dabei normal.
      In einem Spiel aus der Bibliothek ohne Mausmodus wirkt Steams Belegung für das Spiel wie gewohnt, nur ⧉ + ≡
      gehalten macht dort kein Alt+Tab mehr (Steam-Spiel mit Xbox-Controller, z. B. Castlevania). Option aus:
      Steams Desktop-Belegung wirkt wieder. `arcadia.log` nennt beim Ausschalten, wie viele Eingaben verworfen
      wurden, und dass der Filter neben einem Spiel nur bei ⧉ + ≡ wirkt; ohne angeschlossenen Controller ist er aus.
      Mit einer Maus mit 1.000 Hz im Spiel bleibt Arcadias CPU-Last gleich, ob die Option an oder aus ist. · E2E: nein
- [ ] **T64-16** Ein Spiel läuft, mehrmals zwischen Spiel und Arcadia wechseln (Alt+Tab, ⧉ halten, Klick) und im
      Spiel von Hand zwischen Mausmodus und normalem Modus umschalten: Arcadia reagiert nie auf Eingaben, die
      dem Spiel gelten, und ⧉ gehalten holt Arcadia jedes Mal nach vorne. Ebenso mit einem Spiel im randlosen
      Vollbild: Arcadia ist danach wirklich zu sehen, nicht nur im Vordergrund, und wird nie unsichtbar gesteuert.
      ⧉ und ≡ mit mehr als 0,2 s Abstand gedrückt öffnen in Arcadia erst Menü bzw. Einstellungen (bewusst, damit ≡
      gehalten auf einem Suchtreffer die Details öffnet). Mit mehreren Monitoren, Arcadia und Spiel auf verschiedenen:
      Mausmodus im Spiel einschalten setzt den Zeiger in die Mitte des Spielfensters. Ein aus Arcadia gestartetes
      Spiel mit Anti-Cheat (z. B. Easy Anti-Cheat) zählt als Spiel: das Log nennt es beim Vordergrundwechsel „Spiel“. `arcadia.log` nennt jeden
      Wechsel des Vordergrunds („Vordergrund: …“) und wohin ⧉ gehalten geführt hat; bei einem Fehler das Log
      mitschicken. · E2E: nein
- [ ] **T64-17** Steam Controller Support an, Steam Controller verbunden, ein Steam-Spiel auf „Steam Input“ starten:
      SISR pausiert, die Zeile „Steam Controller“ in der Vorschau und auf der Detailseite bleibt sichtbar und
      umschaltbar. Die Nachteile des Steam Controller Supports nennen, dass Mausmodus und „Zurück zu Arcadia“ mit dem
      Steam Controller in Spielen auf „Steam Input“ nicht wirken. Gegenprobe mit dem Xbox-Controller, für das Spiel in
      Steam auf „Steam Input“ gestellt: Mausmodus und ⧉ gehalten wirken weiter (abgeleitet: Steam kann den echten
      Pad vor XInput nicht verstecken).
      Ebenso nach einem Update-Neustart von Arcadia, während das Spiel läuft (Ergebnis im Verlauf notieren). · E2E: nein
- [ ] **T64-18** Mausmodus an, Windows-Bildschirmtastatur (osk.exe) offen: mit A auf Tasten klicken. Noch nicht
      gesehen, Ergebnis im Verlauf notieren: der Filter verwirft vermutlich auch ihre Tasten, weil sie sie wie Steam
      per `SendInput` schickt. · E2E: nein
- [ ] **T64-19** „Von Hand“, „Beenden des Mausmodus in Spielen erzwingen“: „Nein“: ⧉ + ≡ schaltet
      den Mausmodus auch in einem Spiel aus der Bibliothek ein. „Ja“: in so einem Spiel zeigt ⧉ + ≡ gehalten „Mausmodus
      ist in diesem Spiel deaktiviert“ (ohne Vibrieren), und ein eingeschalteter Mausmodus endet, sobald das Spiel nach
      vorne kommt („Mausmodus aus – in diesem Spiel deaktiviert“); im Explorer geht er weiter. „Individuell“: die Zeile
      „Standardeinstellung bei „Individuell““ wird bedienbar, Vorschau und Detailseite zeigen „Individueller
      Mausmodus“ („Standard (erlaubt)“ bzw. „Standard (deaktiviert)“ nach der Standardeinstellung → Erlaubt →
      Deaktiviert, mit A oder Klick), und jedes Spiel folgt seiner Wahl oder bei „Standard“ der Standardeinstellung. Bei „Automatisch“ oder „Aus“ sind beide Zeilen abgeblendet und die
      Schaltfläche fehlt. Alles bleibt nach einem Neustart erhalten. · E2E: nein
- [ ] **T64-15** Die Boxen „Mausmodus“ und „Zurück zu Arcadia“ und „So funktioniert's“ nennen Tasten über ihr
      Symbol (⧉, ≡), nirgends „Back“, „Start“, „View“ oder „Menu“. Die Symbole stehen im Text in einer runden
      Umrandung wie in der Legende; in der Tabelle von „So funktioniert's“ auch A, B, X, LB, RB, LT, RT und L3. · E2E: ja

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T64-7, T64-8 | 2026-10-20 | Claude | Branch `feat/controller-als-maus-nutzen-64`, Linux, Xvfb 1920×1080 `--demo`, Tastatur per xdotool (Pfeiltasten, Enter, Esc) | ✅ Box unter dem Steam Controller, Haltedauer abgeblendet bei Hauptschalter aus, Stepper mit links/rechts und Enter (9 → 1), „So funktioniert's“ öffnet und schließt, Fokus zurück auf der Zeile; Speichern nur per `SettingsServiceTests` geprüft | Controller am PC des Owners |
| T64-1 (nur Overlay) | 2026-10-20 | Claude | wie oben, Overlay lokal per Testcode ausgelöst (nicht eingecheckt) | ⚠️ Rand füllt sich über die Haltedauer von oben Mitte bis unten Mitte, läuft beim Abbruch zurück, Text „Mausmodus an“ 2 s; Hintergrund unter Xvfb schwarz (keine Transparenz) | Windows: Transparenz, Klicks durchs Overlay, Vibrieren, `SendInput`, Vordergrund-Erkennung am PC des Owners |
| T64-1 bis T64-10 | 2026-10-20 | Juri | PC des Owners, PR #120 (Rough-Test) | ❌ „Schließen“ in „So funktioniert's“ unpassend, besser ✕ oben rechts in allen Dialogen; Windows reagiert vereinzelt selbst auf Controller-Eingaben | ✕ in allen Dialogen (T64-11 neu) |
| T64-11 | 2026-10-20 | Claude | Branch nach dem ✕, Linux, Xvfb 1920×1080 `--demo`, Maus und Tastatur per xdotool | ✅ ✕ in Einstellungen, „Ansicht“ und „So funktioniert's“ nach Taste oder Mausbewegung; Klick schließt Einstellungen bzw. nur die Hilfe; Enter schließt die Hilfe. Ausblenden nach Controller-Taste nur im Code geprüft, SISR-Dialoge nicht geöffnet | Controller am PC des Owners |
| T64-1 | 2026-10-20 | Juri | PC des Owners, PR #120, Steam läuft | ❌ Back + Start gehalten öffnet Steams Alt+Tab-Fenster (Steams Desktop-Belegung) | Hauptschalter, „Zurück zu Arcadia“ mit Haltedauer, Steams Desktop-Belegung ignorieren (T64-12 bis T64-14 neu) |
| T64-12, T64-13 (nur Einstellungen und Fenster) | 2026-10-20 | Claude | Branch nach den neuen Einstellungen, Linux, Xvfb 1920×1080 `--demo`, Tastatur per xdotool; Fenster lokal per Testcode ausgelöst und mit 5 Bildern/s aufgenommen (nicht eingecheckt) | ✅ Hauptschalter aus blendet alle Zeilen der Box ab; Haltedauer von „Zurück zu Arcadia“ mit links bis „Sofort“; „Zurück zu Arcadia“ oben in der Mitte, der Rahmen füllt sich in 3 s von oben Mitte über die runden Ecken. View, Hooks und Steam unter Windows nicht geprüft, Logik per `ReturnWithViewTests`, `InjectedInputGateTests`, `MouseModeTests` | T64-13, T64-14 am PC des Owners |
| T64-1 bis T64-10 (Logik) | 2026-10-20 | Claude | `dotnet test` (`MouseModeTests`, `SoloButtonsTests`, `MouseModeAutomationTests`, `StickCurveTests`, `GameFoldersTests`, `SettingsServiceTests`) | ✅ Umschalten, Abbruch, Belegung, Wiederholung, L3, mehrere Controller, Automatik, Spielordner, Einstellungen | Echte Eingaben unter Windows nicht geprüft |
| T64-4 | 2026-10-20 | Juri | PC des Owners, PR #120 | ❌ L3 kurz geklickt, während der Stick bewegt wird, wirkt wie gehalten: der Zeiger wird beim Loslassen wieder schnell | Bewegen macht einen Druck nicht mehr zum Halten, nur noch die Dauer (0,5 s); Test `L3_clicked_briefly_while_pointing_toggles_the_slow_pointer` (vorher rot) |
| T64-15 | 2026-10-20 | Claude | Branch nach der Symbol-Umbenennung, Linux, Xvfb 1920×1080 `--demo`, Tastatur per xdotool | ✅ Beide Boxen und „So funktioniert's“ zeigen ⧉ und ≡, kein Tastenname mehr; die Symbole sind im Fließtext klein, aber lesbar | Schrift unter Windows nicht geprüft |
| T64-5, T64-6, T64-12 | 2026-10-20 | Juri | PC des Owners, PR #120 | ⚠️ ⧉ + ≡ von Hand und „Automatisch einschalten“ nebeneinander wirken schlecht zusammen | „Mausmodus“ ist jetzt eine Wahl aus „Aus“, „Von Hand“ und „Automatisch“; ⧉ + ≡ übersteuert die Automatik, bis Arcadia wieder vorne war (Test `Switched_off_by_hand_it_stays_off_until_arcadia_has_been_in_front_again`) |
| T64-7, T64-12 (nur Einstellungen) | 2026-10-20 | Claude | Branch nach der Dreier-Wahl, Linux, Xvfb 1920×1080 `--demo`, Tastatur per xdotool | ✅ „Mausmodus“ wechselt mit links/rechts zwischen „Aus“, „Von Hand“ und „Automatisch“, der Text darunter wechselt mit; „Aus“ blendet die Zeilen darunter ab, „Automatisch“ nur „Beenden, wenn Arcadia nach vorne kommt“. Fokuswechsel und ⧉ + ≡ unter Windows nicht geprüft, Logik per `MouseModeAutomationTests` | T64-6 am PC des Owners |
| T64-1, T64-15 | 2026-10-20 | Juri | PC des Owners, PR #120 | ❌ Rand und „Mausmodus an/aus“ nur beim ersten Umschalten richtig, danach kein Rand beim Halten, der alte Text flackert kurz auf; Symbole im Text ohne Umrandung schlecht als Taste erkennbar | Das Blinken hielt die Deckkraft 0 fest (`FillMode="Forward"`), das Overlay versteckt sich jetzt erst nach einem leeren Bild; Symbole als Badges im Text |
| T64-1 (nur Overlay), T64-15 | 2026-10-20 | Claude | Branch nach dem Fix, Linux, Xvfb 1920×1080 `--demo`; Overlay lokal per Testcode dreimal nacheinander ausgelöst und mit 5 Bildern/s aufgenommen (nicht eingecheckt) | ✅ Vor dem Fix blieb der Rand ab dem zweiten Halten unsichtbar (Deckkraft 0 im Log), danach füllt er sich jedes Mal; Badges in beiden Boxen und in „So funktioniert's“ lesbar. Das Aufflackern des alten Texts ist unter Xvfb nicht sichtbar, nur unter Windows prüfbar | T64-1 am PC des Owners |
| T64-14, T64-16, T64-17 | 2026-10-20 | Juri | PC des Owners, PR #120, Steam-Spiel (Castlevania), Xbox-Controller und Steam Controller | ❌ Im Steam-Spiel macht Steam aus ⧉ + ≡ Alt+Tab; Arcadia reagiert manchmal auf Eingaben im Spiel und ⧉ holt es nicht zurück; Steam Controller auf „Steam Input“: ⧉ + ≡ und ⧉ halten wirken nicht; die Steam-Controller-Zeile verschwindet, solange SISR pausiert | Filter wirkt neben Spielen bei ⧉ + ≡; „vorne“ über das Vordergrundfenster; Steam Input als Grenze in „So funktioniert's“ (Lösung erst mit #112/#72); Zeile bleibt (Test `The_controller_still_counts_as_connected_while_SISR_is_paused_for_a_game`, vorher rot) |
| T64-14, T64-16 (nur Logik) | 2026-10-20 | Claude | `dotnet test` (`InjectedInputGateTests`, `ControllerSupportServiceTests`) | ✅ ⧉ + ≡ zählt mit Nachlauf; Steam-Controller-Zeile bleibt bei Pause. Hooks, Steam und Vordergrund unter Windows nicht geprüft | T64-14, T64-16, T64-17 am PC des Owners |
| T64-14, T64-16 (Review) | 2026-10-20 | Claude | Review zu PR #120 (`reviews/pr-120-code-review.md`), `dotnet build`/`dotnet test` unter Linux | ✅ Build und Tests grün. Neu: „vorne“ verlangt zusätzlich, dass kein fremdes Fenster Arcadia ganz verdeckt (randloses Vollbild), ⧉ gehalten hält Arcadia dann bis zum Fokuswechsel oben; neben Spielen nur der Tastatur-Hook, ohne Controller keiner, die Hooks werden alle 30 s und bei jedem Wechsel neu gesetzt. Hooks, Vordergrund und CPU-Last unter Windows nicht geprüft | T64-14, T64-16 bis T64-18 am PC des Owners |
| T64-16 (Review, Nachtrag) | 2026-10-20 | Claude | Nach Juris Antwort (drei Monitore, Vollbildart egal), `dotnet build`/`dotnet test` unter Linux | ✅ Build und Tests grün. Neu: Zeiger beim Einschalten im Spiel in die Mitte des Spielfensters; Log jedes Vordergrundwechsels und des Ziels von ⧉ gehalten. Unter Windows nicht geprüft | T64-16 am PC des Owners, mit Log |
| T64-14, T64-16 (M3, M5) | 2026-10-20 | Claude | Nach Juris Freigabe von M3 und M5; Linux, Xvfb 1920×1080 `--demo`, Tastatur per xdotool; `dotnet test` | ✅ Eigene Box „Steams Desktop-Belegung ignorieren“ unter „Zurück zu Arcadia“, mit dem Steuerkreuz erreichbar (die Liste scrollt mit), Enter schaltet um. Pfade mit `/`, doppelten Trennern, `.`/`..` und `\\?\` passen (`GameFoldersTests`, vorher rot); `LaunchedGamesTests`. Hooks, Anti-Cheat-Spiel und Vordergrund unter Windows nicht geprüft | T64-14, T64-16 am PC des Owners |
| T64-17 (nur Text) | 2026-10-20 | Claude | Linux, Xvfb 1920×1080 `--demo`, Maus per xdotool | ✅ Der Hinweis zum Steam Controller auf „Steam Input“ steht unter den Nachteilen des Steam Controller Supports, nicht mehr in „So funktioniert's“ (Juri) | T64-17 am PC des Owners |
| T64-7, T64-8, T64-19, T64-17 (Texte) | 2026-10-20 | Claude | Linux, Xvfb 1920×1080 `--demo`, Tastatur per xdotool; `dotnet test` (`MouseModeInGamesTests`, `MouseModeTests`, `SettingsServiceTests`, `GameFoldersTests`, `LaunchedGamesTests`) | ✅ Neue Texte für „Von Hand“ und „Automatisch“; „Deaktivierten Mausmodus im Spiel erzwingen“ wechselt Nein/Ja/Individuell, „Standardeinstellung bei „Individuell““ wird erst bei „Individuell“ bedienbar; Vorschau und Detailseite zeigen „Individueller Mausmodus“, A wechselt Standard → Erlaubt → Deaktiviert und die Vorschau folgt. Nachteile des Steam Controller Supports gekürzt, Grenze „Spiel sieht die Eingaben“ entfernt. Sperre im Spiel unter Windows nicht geprüft (Logik per Test) | T64-19 am PC des Owners |
| T64-5, T64-19, T11-7 (Texte, Detailseite) | 2026-10-21 | Claude | Linux, Xvfb 1920×1080 `--demo`, Tastatur per xdotool; `dotnet test` | ✅ Neue Titel und Texte in der Box „Mausmodus“; Vorschau und Detailseite zeigen „Standard (deaktiviert)“, A wechselt zu „Erlaubt“; Detailseite in drei Reihen, ↓/↑ durch alle drei, mit Update-Hinweis und Hinweis der Schaltfläche in einer Zeile ragt nichts in die Kennzahlen. Steam-Controller-Zeile nicht gesehen (kein Steam Controller unter Linux) | Lauf am PC des Owners |

## #124 SISR übernimmt bei Steam Input für Xbox-Controller auch den Xbox Controller

- [ ] **T124-1** Steam Controller Support an, Steam Controller und Xbox Controller verbunden. In einem
      Steam-Spiel Steam Input für alle Controller einschalten und das Spiel aus Arcadia auf „Xbox 360
      Controller (SISR)“ starten: SISR ist schon vor dem Spielstart beendet, das Spiel nimmt sofort Eingaben
      vom Xbox Controller an. `arcadia.log` nennt Steams Einstellung und dass SISR pausiert, die Einstellungen
      zeigen „SISR pausiert für „…““. Auch beim ersten Spielstart nach dem Start von Arcadia. · E2E: nein
      (echtes Steam mit zwei Controllern)
- [ ] **T124-2** Wie T124-1, aber auf „Steam Input“ gestartet: Das Spiel nimmt Eingaben von beiden
      Controllern an. · E2E: nein (echtes Steam mit zwei Controllern)
- [ ] **T124-3** Wie T124-1, mit dem Steam Controller gespielt: Das Spiel bekommt ihn über Steam Input. Nach
      dem Beenden läuft SISR wieder, der Hinweis ist weg und der Steam Controller wird in Arcadia wieder als
      verbunden angezeigt. · E2E: nein (echtes Steam mit Steam Controller)
- [ ] **T124-4** Nur der Xbox Controller ist verbunden, und SISR übernimmt ihn (Steam Input für ihn an):
      Arcadia zeigt keinen Steam Controller als verbunden an (keine Modus-Auswahl in der Vorschau). · E2E: nein
- [ ] **T124-5** Ein Steam-Spiel ohne Steam Input für Xbox-Controller verhält sich wie bisher (G-24, T92-2). · E2E: nein
- [ ] **T124-7** Wie T124-1, aber das Spiel aus Steam selbst starten, während Arcadia läuft: SISR pausiert
      innerhalb weniger Sekunden, danach nimmt das Spiel Eingaben vom Xbox Controller an. · E2E: nein
- [ ] **T124-8** Im selben Spiel Steam Input nur für einen Teil der Controller-Typen einschalten (z. B. ohne
      Xbox) und auf „Xbox 360 Controller (SISR)“ starten: `arcadia.log` nennt, was Steam dafür in
      `localconfig.vdf` schreibt; SISR pausiert nur, wenn der Xbox Controller sonst keine Eingaben hätte. · E2E: nein
- [ ] **T124-6** Am PC des Owners: Was SISRs `/api/v1/devices` für den Steam Controller (Puck 28DE:1304) und
      ein Xbox-Pad als `real_device` meldet, passt zur Erkennung in `docs/controller-support.md`
      (`arcadia.log` beim Start von SISR und bei T124-1). · E2E: nein
- [ ] **T124-9** Statussymbole im Header und in den Einstellungen → Steam Controller Support: SISR für ein
      Spiel pausiert (T124-1) zeigt das blaue Info-Symbol (Kreis mit i), und der Status bleibt „bereit“;
      Steam beendet oder SISR deinstalliert, während der Support an ist, zeigt das gelbe Dreieck mit !;
      SISR stürzt wiederholt ab zeigt das rote X. Bei mehreren Meldungen zeigt der Header das Symbol der
      schwersten. · E2E: nein (echtes SISR und Steam)

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T124-1 bis T124-4 | 2026-10-20 | Claude | Branch `fix/steam-input-fuer-xbox-laesst-sisr-den-xbox-controller-uebernehmen-124`, Linux, nur Unit-Tests mit nachgebildetem SISR (`ControllerSupportServiceTests`, `SisrDevicesTests`) | ⚠️ Logik grün, App und echtes SISR nicht gestartet | Lauf am PC des Owners |
| T124-1 | 2026-10-21 | Juri | PC des Owners, PR #125 (erster Stand), Castlevania: Belmont's Curse, 8BitDo Ultimate 2 und Steam Controller | ❌ Erster Start: SISR übernahm den Pad nicht, keine Pause, keine Eingaben; mit beiden Controllern pausierte SISR erst 85 s nach dem Start, das Spiel reagierte nicht | Pause nach Steams Einstellung schon vor dem Spielstart (`SteamInputSetting`) |
| T124-1, T124-7 | 2026-10-21 | Claude | Branch wie oben, Linux, Unit-Tests; Steams Einstellung aus Juris `localconfig.vdf` gelesen (`UseSteamControllerConfig 2` für 4231820) | ⚠️ Logik grün, App nicht gestartet | Lauf am PC des Owners |
| T124-1 | 2026-10-21 | Juri | PC des Owners, PR #125 (Pause nach Steams Einstellung) | ✅ „Sieht grundsätzlich erstmal gut aus“; Hinweis zeigte noch das Warnsymbol | Drei Stufen Info/Warnung/Fehler (T124-9) |
| T124-9 | 2026-10-21 | Claude | Branch wie oben, Linux, Unit-Tests (Stufen der Meldungen); App gebaut, nicht gestartet | ⚠️ Stufen grün, Symbole nicht angesehen | Lauf am PC des Owners |

## #103 Laufende Spiele ohne Exceptions erkennen

- [ ] **T103-1** Arcadia im Debugger (Rider/Visual Studio) starten, ein Steam-Spiel starten (SISR pausiert) und
      eine Minute laufen lassen: Die Debug-Konsole zeigt keine Serien von `Win32Exception` („Zugriff verweigert“)
      oder `InvalidOperationException` mehr; beim Start keine Binding-Fehler „Value is null“, auch nicht beim
      Öffnen von Detailseite, Kontextmenü und Suche mit Tastatur. · E2E: nein (Debugger, echtes Spiel)
- [ ] **T103-2** Eine Kopie eines kleinen Programms (z. B. `notepad.exe`) in den Ordner eines Steam-Spiels legen.
      Das Spiel aus Arcadia starten (SISR pausiert), die Kopie per Rechtsklick „Als Administrator ausführen“
      starten, dann das Spiel beenden: SISR bleibt pausiert, bis auch das Programm beendet ist. · E2E: nein (UAC)
- [ ] **T103-3** CPU von Arcadia neben einem laufenden Spiel mit pausiertem SISR, vorher (`main`) und nachher
      (dieser Build), je 60 s: `$p = Get-Process Arcadia; $t = $p.TotalProcessorTime; Start-Sleep 60; $p.Refresh();`
      `($p.TotalProcessorTime - $t).TotalSeconds / 60 / [Environment]::ProcessorCount * 100` (Prozent). Nachher
      nicht höher als vorher, Ziel < 1 %. · E2E: nein (echtes Spiel, Messung am PC des Owners)

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T103-1 (nur Binding-Fehler) | 2026-10-20 | Claude | Branch `fix/processwatch-103-xq8e1u`, Linux, Xvfb 1920×1080 `--demo`, Avalonia-Log lokal auf die Konsole umgeleitet (nicht eingecheckt), Tastatur und Maus per xdotool | ✅ vorher 22 Binding-Fehler beim Start, 4 beim Öffnen der Suche, 50 beim Ausklappen der Tastatur; nachher keiner. Bibliothek, Detailseite, Kontextmenü, Suche und Einstellungen sehen aus wie vorher | Exceptions, Adminrechte und CPU nur unter Windows prüfbar |
| T103-1 bis T103-3 (Logik) | 2026-10-20 | Claude | `dotnet test` (`ProcessWatchTests`), Linux | ⚠️ Ordner des eigenen Prozesses wird gefunden, leerer Ordner nicht; den Windows-Weg (`PathOf`) prüft nur die CI unter Windows | Windows, Adminrechte und CPU am PC des Owners |

## #127 UI Polishing

- [ ] **T127-1** Detailseite mit dem Controller: Zwischen den Button-Reihen und zwischen den Buttons berührt
      der Fokusrahmen keine Nachbarn; die Kennzahlen unten sind vollständig sichtbar, auch bei einem Spiel mit
      Update (Hinweiszeile unter den Buttons). · E2E: ja
- [ ] **T127-2** Detailseite: Fokus auf „Steam Controller“ bzw. „Individueller Mausmodus“ zeigt keinen
      Hinweis mehr unter den Buttons; das Kontextmenü zeigt „Wechselt zu „…““. Maus über einem deaktivierten
      Button (z. B. „Zu Favoriten“) zeigt den Grund als Tooltip. · E2E: ja
- [ ] **T127-3** Detailseite aus der Bibliothek: X (Tastatur F, Klick auf den Such-Chip) öffnet die Suche;
      B/Esc dort führt zurück auf dieselbe Detailseite. · E2E: ja
- [ ] **T127-4** Detailseite aus der Suche: X führt zurück in die Suche mit unverändertem Text; B/Esc führt
      ins Listing des Launchers des Spiels, auf dessen Seite und mit dem Spiel fokussiert; die Suche ist zu. · E2E: ja
- [ ] **T127-5** Wenige Launcher: Der Tab-Streifen steht mittig zwischen Logo und Status, nur so breit wie
      nötig, LB/RB direkt daneben, keine Trennstriche an seinen Enden. · E2E: ja
- [ ] **T127-6** Viele Launcher (mehr als in den Header passen): Der Streifen reicht mit je 40 px und einem
      Trennstrich bis an Logo und Status; der gewählte Tab steht in der Mitte, soweit die Enden es erlauben;
      wo weitere Tabs folgen, blendet der Streifen aus. Mausrad über den Tabs dreht ihn, ein angeschnittener
      Tab ist anklickbar. Mit einer Statusmeldung (z. B. „Controller wird eingerichtet …“) wird er schmaler,
      die Abstände bleiben gleich, ein Trennstrich steht zwischen Meldung und Such-Chip. · E2E: ja (bis auf
      die SISR-Meldung)
- [ ] **T127-7** Eingabeart: Nach einer Controller-Taste zeigen Footer, Header-Chips ([X 🔍], [≡ ⚙]),
      LB/RB, LT/RT, „Ansicht“ (Y), „Spielen“ (A), B in der Detailseite und die Hinweise der Dialoge
      Controllertasten (rund); nach einer Taste oder einem Klick die Tastatur (eckig) und, wo es passt, die
      Maus. Bloßes Bewegen der Maus schaltet nicht um. Alle Glyphen haben die dunklere Unterkante. · E2E: ja
- [ ] **T127-8** Tastatur: WASD und Pfeile bewegen, Leertaste wie A (gehalten: Details), Q/E Seite bzw.
      Spiel, Umschalt+Q/E Launcher, 1 … 0 Launcher 1–10 (auch auf der Detailseite), R Optionen, V Ansicht,
      F Suche, Enter Einstellungen (nochmals Enter schließt sie), Esc zurück. In der Suche tippen diese
      Buchstaben, Enter startet den Treffer. Gehaltenes Enter öffnet die Einstellungen nur einmal. · E2E: ja
- [ ] **T127-9** In den Einstellungen (Mausmodus) erscheinen LS und L3 als Symbol im Text, in „So
      funktioniert's“ LS, RS und D-Pad als Symbol. · E2E: ja
- [ ] **T127-10** Detailseite eines Spiels mit Unterlänge im Titel (g, j, p, q, y, z. B. „Burning Kingdom“):
      der Titel ist ganz zu sehen, auch zweizeilig. · E2E: ja

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T127-1, T127-3 bis T127-8 | 2026-10-21 | Claude | Branch `feat/ui-polishing-127`, Linux, Xvfb 1920×1080 `--demo`, Tastatur per xdotool (W, 4, Leertaste gehalten, F, Esc); wenige und viele Launcher mit einem vorübergehenden, nicht eingecheckten Eingriff in die Demo-Launcher; Controller-Ansicht nur im Startzustand | ✅ Screenshots im Projektordner `screenshots/ui-polishing-127/`: Streifen mittig bzw. bis an die Trennstriche mit Ausblendung, gewählter Tab zentriert; Tastenhinweise rund/eckig; Detailseite mit neuen Abständen; F öffnet die Suche über der Detailseite, Esc führt zurück. Statusmeldung, Tooltip, T127-2, T127-9 und echter Controller nicht angesehen | Rest am PC des Owners |
| T127-10 | 2026-10-21 | Claude | Branch `feat/ui-polishing-127`, Linux, Xvfb 1920×1080 `--demo`, „Burning Kingdom“ | ❌ vorher: das g abgeschnitten (Zeilenhöhe 70 bei 64 px Schrift), ✅ nachher ganz zu sehen | — |

## #129 Bildschirmtastatur als Nachbildung echter Tastaturlayouts

- [ ] **T129-1** Suche mit X öffnen, Layout DE: Die Zeichenreihen sind die der echten deutschen Tastatur
      (`^ 1 … 0 ß ´` / `q … ü +` / `a … ä #` / `< y … , . -`), ohne Tab, Feststelltaste, Eingabe und Umschalt.
      Jede Taste zeigt groß ihr Zeichen, klein darüber das Umschalt-Zeichen (bei Buchstaben keins) und klein
      rechts unten das AltGr-Zeichen (z. B. q mit @, e mit €, 7 mit {, + mit * und ~). Die Tastatur ist so
      breit wie vorher. · E2E: ja
- [ ] **T129-2** LB kurz tippen: Taste „⇧ Groß“ hervorgehoben, die Tasten zeigen nur noch das Umschalt-Zeichen
      (z. B. °, !, ?, Q, *, >), bleibt so, nochmal tippen schaltet aus. LB gehalten gilt nur, solange es gehalten
      wird. Getippt wird das gezeigte Zeichen. · E2E: ja
- [ ] **T129-3** RB kurz tippen: Taste „AltGr“ hervorgehoben, die Tasten zeigen nur ihr AltGr-Zeichen, alle anderen
      sind leer; bleibt so, nochmal tippen schaltet aus, gehalten gilt es nur, solange es gehalten wird. LB schaltet
      AltGr aus und umgekehrt. Getippt wird das gezeigte Zeichen (z. B. @, €, {, ~, |, µ). · E2E: ja
- [ ] **T129-4** Mit AltGr an überspringt das D-Pad leere Tasten: von € nach links @, nach rechts ~, nach oben ³,
      nach unten |; eine Reihe ganz ohne Zeichen wird übersprungen. Ohne AltGr führt hoch und wieder runter
      zurück zur selben Taste (z. B. e → 3 → e). · E2E: ja
- [ ] **T129-5** Die Tottasten ^ ´ ` (und bei FR/ES ¨) tippen ihr Zeichen sofort, ohne auf den nächsten Buchstaben
      zu warten. · E2E: ja
- [ ] **T129-6** R3 → nacheinander US, UK, FR, ES: Jede Tastatur zeigt die Tasten ihres echten Layouts mit
      Umschalt- und AltGr-Zeichen (US: `` ` `` … `=` / `q … ] \`, ohne <-Taste; UK: `#` und `\`, AltGr 4 = €;
      FR: AZERTY mit `& é " ' (` in der Zahlenreihe; ES: `º … ¡`, ñ). Bei US ist „AltGr“ ausgegraut und RB
      bzw. ein Klick darauf tun nichts; ist AltGr beim Wechsel zu US an, geht es aus. · E2E: ja
- [ ] **T129-7** Mit der Maus: ein Klick auf eine Taste tippt das gezeigte Zeichen, Klicks auf „⇧ Groß“ und
      „AltGr“ schalten die Ebene um. · E2E: ja

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T129-1, T129-2, T129-3, T129-6, T129-7 | 2026-10-21 | Claude | Branch `feat/bildschirmtastatur-als-nachbildung-echter-tastaturlayouts-129`, Linux, Xvfb 1920×1080 `--demo`, Fenster 1600×900, nur Maus per xdotool | ✅ DE mit Umschalt- und AltGr-Zeichen in der bisherigen Breite; Klick auf „AltGr“ zeigt nur ² ³ { [ ] } \ @ € ~ \| µ, auf „⇧ Groß“ nur ° ! " … Q W E …; Klick auf „a“ tippt „a“; US mit ausgegrautem „AltGr“, Klick darauf ändert nichts | Controller (LB/RB gehalten, D-Pad über leere Tasten) am PC des Owners |
| T129-4, T129-5, T129-6 | 2026-10-21 | Claude | wie oben, Unit-Tests (`KeyboardLayoutTests`, `KeyGridTests`) | ⚠️ Layouts aller fünf Sprachen, Tottasten und das Überspringen leerer Tasten grün; mit dem Controller nicht gefahren | Lauf am PC des Owners |

## #95 Laufende Spiele anzeigen statt „Spielen“ anzubieten

- [ ] **T95-1** Ein Steam-Spiel mit A starten: Kachel „Startet“, Vorschau und Detailseite „Wird gestartet …“, dann
      „Läuft“ (Kachel) bzw. „Läuft seit HH:MM“ (Vorschau, Detailseite, Suchtreffer); A, Footer und Kontextmenü heißen
      „Zurück ins Spiel“, kein Update-Symbol. Nach dem Beenden zeigt alles wieder „Spielen“. · E2E: nein (echter
      Launcher, echtes Spiel)
- [ ] **T95-2** Wie T95-1 mit einem Epic-Spiel. · E2E: nein (echter Launcher, echtes Spiel)
- [ ] **T95-3** Ein Spiel direkt in Steam starten, dann zu Arcadia wechseln: Es zeigt nach höchstens 2 s „Läuft“;
      nach dem Beenden wieder „Spielen“. Dasselbe mit Epic. · E2E: nein (echter Launcher, echtes Spiel)
- [ ] **T95-4** „Zurück ins Spiel“ nur mit dem Controller, je einmal über A in der Bibliothek, den Button der
      Detailseite und das Kontextmenü: Das Spielfenster kommt nach vorne, auch wenn es minimiert war. Ein Spiel ohne
      Fenster (noch im Ladebildschirm des Launchers) bringt einen Toast. · E2E: nein (echtes Spiel)
- [ ] **T95-5** Ein Spiel, das als Administrator läuft (z. B. per Rechtsklick „Als Administrator ausführen“ aus seinem
      Ordner gestartet): „Zurück ins Spiel“ holt es nach vorne oder meldet per Toast, dass es nicht ging; Ergebnis im
      Verlauf notieren. · E2E: nein (UAC)
- [ ] **T95-6** Während „Wird gestartet …“ nochmals A drücken: Toast „… wird bereits gestartet …“, das Spiel startet
      nicht ein zweites Mal. · E2E: nein (echter Launcher)
- [ ] **T95-7** Während ein Spiel läuft, ein anderes aus Arcadia starten: Es startet (sofern der Launcher es
      zulässt), beide zeigen „Läuft“. · E2E: nein (echte Spiele)
- [ ] **T95-8** Demo-Daten (`--demo`): In jedem Launcher läuft ein Spiel (Kachel „Läuft“, Vorschau „Läuft seit …“,
      25 min vor dem Start von Arcadia). Vorschau: „Zurück ins Spiel“ steht in voller Größe ganz im Button, mit
      Abstand zur Rundung, mit Controller wie mit Tastatur; „Optionen“ steht darunter. Detailseite: Chip „LÄUFT SEIT …“, Button „Zurück ins Spiel“, „Deinstallieren …“
      ausgegraut mit Tooltip „Erst wenn das Spiel beendet ist“; Kontextmenü ebenso. A darauf bringt den Toast „Arcadia
      findet kein Fenster …“. · E2E: ja
- [ ] **T95-9** Mit angeschlossenem Steam Controller (SISR an), während ein Spiel läuft: Der Steam-Controller-Modus in
      Vorschau, Detailseite und Kontextmenü ist ausgegraut, Tooltip bzw. Hinweis „Gilt erst beim nächsten Start – änderbar,
      wenn das Spiel beendet ist“; „Individueller Mausmodus“ bleibt änderbar. Nach dem Spielende ist der Modus wieder
      änderbar. · E2E: nein (Steam Controller)
- [ ] **T95-10** Nach dem Beenden eines Steam- und eines Epic-Spiels stimmen „Zuletzt gespielt“ und die Spielzeit in
      Vorschau und Detailseite (bei Steam spätestens 30 s nach dem Ende); mit der Sortierung „Zuletzt gespielt · neueste
      zuerst“ steht das Spiel vorne. · E2E: nein (echte Spiele)
- [ ] **T95-11** Programm mit nicht lesbarem Pfad, aus Arcadia gestartet: zeigt „Läuft“ und danach wieder „Spielen“.
      Erst prüfbar, sobald ein Launcher ohne eigenes Signal kommt (Steam und Epic melden laufende Spiele selbst, die
      Prozesssuche läuft für sie nicht); bis dahin decken `ProcessGamesTests` und `RunningGamesTrackerTests` es ab.
      · E2E: nein (noch kein Launcher ohne eigenes Signal)
- [ ] **T95-12** Steams Flag prüfen: Während ein Steam-Spiel läuft,
      `Get-ItemProperty HKCU:\Software\Valve\Steam\Apps\<appid> | Select Running` zeigt 1, nach dem Ende 0; auch nach
      einem Absturz (Spiel im Task-Manager beenden). „Läuft seit“ stimmt mit der Startzeit überein. Ergebnis in
      `docs/launchers/steam.md` festhalten. · E2E: nein (echter Launcher)
- [ ] **T95-13** CPU von Arcadia neben einem aus Arcadia gestarteten Spiel, 60 s, wie in T103-3: unter 1 %. ·
      E2E: nein (Messung am PC des Owners)
- [ ] **T95-14** Arcadia auf dem zweiten Monitor, nicht im Vordergrund: Ein Spiel direkt in Steam bzw. Epic starten
      zeigt nach höchstens 2 s „Läuft“, sein Ende nach höchstens 2 s wieder „Spielen“, ohne dass Arcadia den Fokus
      bekommt. · E2E: nein (echter Launcher, zwei Monitore)

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T95-8 | 2026-10-21 | Claude | Branch `feat/laufende-spiele-anzeigen-94-95`, Linux, Xvfb 1920×1080 `--demo`, Tastatur per xdotool (Pfeile, R, Leertaste gehalten, Leertaste, F, Tippen) | ✅ Screenshots im Projektordner `screenshots/laufende-spiele-94-95/`: Kachel- und Treffer-Badge „Läuft“, Vorschau „Läuft seit 18:19“, „Zurück ins Spiel“ in Button und Footer, Detailseite mit Chip und ausgegrautem „Deinstallieren …“, Kontextmenü, Toast ohne Fenster. „Wird gestartet …“ nicht zu sehen, weil Demo-Spiele nicht starten | Echte Launcher am PC des Owners |
| T95-8 (Vorschau-Button) | 2026-10-21 | Juri | Screenshot des Laufs oben | ❌ „Zurück ins Spiel“ in der Vorschau nicht ganz im Button: verkleinert, stößt aber rechts an die Rundung (der Button hatte keinen Innenabstand) | Innenabstand; „Optionen“ gab sein Wort ab, wenn der Text den Platz brauchte |
| T95-8 (Vorschau-Button) | 2026-10-21 | Juri | Screenshot der Lösung mit „Optionen“ als Symbol | ❌ Lösung gefällt nicht | Buttons untereinander statt nebeneinander |
| T95-8 (Vorschau-Button) | 2026-10-21 | Claude | Branch `feat/laufende-spiele-anzeigen-94-95`, Linux, Xvfb `--demo`, Tastatur per xdotool | ✅ Buttons untereinander in voller Breite: „Zurück ins Spiel“ und „Installieren“ mit Tastatur, „Spielen“ mit Controller in voller Größe ganz im Button, „⧉/R Optionen“ ausgeschrieben darunter; nichts überlappt die Angaben darüber | — |
| T95-1 bis T95-3, T95-11 (Logik) | 2026-10-21 | Claude | `dotnet test`, Linux: `EpicRunningGamesTests`, `SteamRunningGamesTests` (Registry nachgebildet), `ProcessGamesTests`, `RunningGamesTrackerTests` | ⚠️ Start-/Stop-Zeilen, halbe Zeilen, neues Log, Registry-Flag und `RunningAppID`, Prozesse aus dem Spielordner und mit nicht lesbarem Pfad, „Wird gestartet …“ mit 2-min-Grenze grün; echte Launcher nicht | Lauf am PC des Owners |
| T95-13 (Prozesssuche) | 2026-10-21 | Claude | Release-Build, Linux, nachgebildete Prozesstabelle mit 351 Prozessen, 5.000 Spiele | ⚠️ ohne die Windows-Aufrufe je Prozess: Ordner der 5.000 Spiele aufbauen 3,2 ms (nur bei einer neuen Bibliothek), erste Runde 0,5 ms, jede weitere Runde ohne neue Prozesse 40 µs; Windows fragt nur neue Prozesse ab. Läuft für Steam und Epic gar nicht | CPU neben dem Spiel am PC des Owners |
| T95-4, T95-13 | 2026-10-21 | Juri | PC des Owners, Branch `feat/laufende-spiele-anzeigen-94-95` (Stand 25197b1), Debug aus VS Code (Arcadia als Teil des .NET-Host-Prozesses) | ✅ „Zurück ins Spiel“ holt das Spiel nach vorne. CPU laut Task-Manager meist 0–0,5 %, neben dem Spiel Spitzen bis 0,7 %; RAM knapp 200 MB. ⚠️ Debug-Build im .NET-Host, nicht der Release-Build | CPU im installierten Release-Build nachmessen |
| T95-3, T95-14 | 2026-10-21 | Juri | PC des Owners, Stand 25197b1, Debug aus VS Code, Arcadia auf dem zweiten Monitor | ❌ Ein in Steam/Epic gestartetes Spiel erst gesehen, als Arcadia den Fokus bekam; ebenso sein Ende, „Zurück ins Spiel“ dazwischen meldete einen fehlenden Prozess | Im Hintergrund alle Launcher fragen, nicht nur für aus Arcadia gestartete Spiele |
| T95-14 (Logik) | 2026-10-21 | Claude | `dotnet test`, Linux: `RunningGamesTrackerTests` | ⚠️ Jede Runde fragt jeden Launcher und sucht Prozesse, auch ohne aus Arcadia gestartetes Spiel; echte Launcher und CPU nicht | Lauf und CPU-Messung (T95-13) am PC des Owners |
| T95-14, T95-13 | 2026-10-21 | Juri | PC des Owners, Stand 271511b, Debug aus VS Code, Arcadia auf dem zweiten Monitor | ✅ Spiel in Steam/Epic gestartet und beendet: „Läuft“ und wieder „Spielen“ ohne Fokus auf Arcadia. CPU laut Task-Manager unverändert, Sprünge unter 1 %. ⚠️ Debug-Build im .NET-Host | CPU im Release-Build bei der Abnahme |

## #94 Nach einem aus Arcadia gestarteten Spiel kommt Arcadia wieder in den Vordergrund

- [ ] **T94-1** Ein Steam-Spiel aus Arcadia starten, spielen, beenden: Etwa 1 s später ist Arcadia vorne und mit dem
      Controller bedienbar. · E2E: nein (echtes Spiel)
- [ ] **T94-2** Wie T94-1 mit einem Epic-Spiel, nach dem Epic das Bewertungsfenster öffnet: Arcadia liegt darüber und
      ist mit dem Controller bedienbar. · E2E: nein (echtes Spiel)
- [ ] **T94-3** Ein Spiel direkt im Launcher starten und beenden: Arcadia bleibt, wo es war. · E2E: nein (echtes Spiel)
- [ ] **T94-4** Ein Spiel aus Arcadia starten, mit Alt+Tab in den Browser wechseln, das Spiel enden lassen (z. B. im
      Task-Manager beenden): Arcadia bleibt hinten. · E2E: nein (echtes Spiel)
- [ ] **T94-5** Während ein aus Arcadia gestartetes Spiel läuft, Arcadia aktualisieren („Jetzt aktualisieren“): nach
      dem Spielende kommt das neue Arcadia nach vorne. · E2E: nein (Update, echtes Spiel)
- [ ] **T94-6** Mausmodus „Automatisch“, bzw. „Von Hand“ mit „Mausmodus beenden, wenn Arcadia im Vordergrund ist“: Ist
      der Mausmodus beim Spielende an, endet er, sobald Arcadia nach vorne kommt. · E2E: nein (echtes Spiel, Controller)
- [ ] **T94-7** Ein aus Arcadia gestartetes Programm mit nicht lesbarem Pfad beenden: Arcadia ist vorne. Erst prüfbar
      wie T95-11. · E2E: nein (noch kein Launcher ohne eigenes Signal)

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T94-1, T94-3, T94-5, T94-7 (Logik) | 2026-10-21 | Claude | `dotnet test`, Linux: `RunningGamesTrackerTests` | ⚠️ Nur das Ende eines aus Arcadia gestarteten Spiels meldet „aus Arcadia“, auch nach der Übergabe beim Update; Fenster und Vordergrund nur unter Windows prüfbar | Lauf am PC des Owners |
| T94-1 | 2026-10-21 | Juri | PC des Owners, Branch `feat/laufende-spiele-anzeigen-94-95` (Stand 25197b1), Debug aus VS Code (Arcadia als Teil des .NET-Host-Prozesses) | ✅ Nach dem Beenden des Spiels kam Arcadia wieder nach vorne. Danach kurz 1,2 % CPU, vermutlich das Neueinlesen der Bibliothek nach dem Spielende (Spielzeit, „Zuletzt gespielt“), das gleich und 30 s später läuft | — |

## #15 Einstellungsseite mit Kategorien im Header

- [ ] **T15-1** In der Bibliothek auf Seite 2 ein Spiel fokussieren, ≡ drücken: Die Einstellungen ersetzen die ganze
      Seite. B führt zurück auf denselben Tab, dieselbe Seite und dasselbe Spiel. Ebenso mit Enter und Esc, mit einem
      Klick auf den Chip und ✕, und von einer Detailseite aus (B führt auf die Detailseite zurück). · E2E: ja
- [ ] **T15-2** LB/RB, Q/E und ein Klick auf eine Kategorie wechseln die Kategorie; nach der letzten kommt die erste.
      Der gewählte Tab steht in der Mitte; folgen mehr Kategorien, als Platz haben, fadet die Leiste an diesem Ende aus.
      Den Fadeout erst prüfbar, wenn es genug Kategorien gibt (#136 und später). · E2E: ja
- [ ] **T15-3** Steam Controller Support an, Steam beenden: Neben „Controller“ steht das Warnsymbol. Ein Spiel mit
      „Steam Input“ starten (SISR pausiert, nur Info): kein Symbol. Ein Fehler (SISR stürzt wiederholt ab) zeigt das
      rote X. · E2E: nein (Steam, SISR)
- [ ] **T15-4** Nur mit dem Controller in „Controller“: ▲ ▼ fokussieren Zeile für Zeile und halten am Ende an, die Zeile
      bleibt im Bild samt Gruppenüberschrift. ◀ ▶ ändern Schalter und Auswahlen sofort, A schaltet um bzw. zählt weiter,
      bei Aktionen („Beheben“, „Aktualisieren“, „SISR installieren“) löst A sie aus. „Mausmodus: Aus“ graut die
      abhängigen Zeilen aus, ihre Werte bleiben. Nach einem Neustart sind alle Änderungen noch da. · E2E: ja
- [ ] **T15-5** Der Footer nennt nur die Tasten der fokussierten Zeile („◀▶ Ändern“, „A Umschalten“, „A Beheben“ …),
      dazu „LB RB Kategorie“ und „B Zurück“; nach einer Taste oder einem Klick die Tasten und die Maus. · E2E: ja
- [ ] **T15-6** „Mausmodus – so funktioniert's“ fokussieren: Das Infopanel zeigt die Tasten und die Grenzen, unten fadet
      es aus, der Footer nennt RS. RS, Umschalt+W/S, Bild auf/ab und das Mausrad scrollen; oben fadet es dann aus. Eine
      andere Zeile beginnt das Infopanel wieder oben. · E2E: ja
- [ ] **T15-7** „SISR installieren“ bei laufendem Steam: „Steam beenden?“ erscheint als Dialog über der Seite; B schließt
      nur den Dialog, die Seite bleibt. Während Steam oder SISR starten, nimmt die Kategorie keine Eingaben an, B und
      LB/RB gehen. · E2E: nein (SISR, Steam)
- [ ] **T15-8** „Über Arcadia“ im installierten älteren Release-Kandidaten: Version, „Nach Updates suchen“, „Jetzt
      aktualisieren“ und „Was ist neu“ (Versionen im Infopanel) sind Zeilen; A auf „Jetzt aktualisieren“ aktualisiert.
      · E2E: nein (Update)
- [ ] **T15-9** Mit einer Hand an der Tastatur (W/S, A/D, Leertaste, Esc, Q/E, Umschalt+W/S) und allein mit der Maus
      (Klick auf Zeilen, Pfeile neben den Werten, Kategorien, ✕, Mausrad im Infopanel) ist jede Einstellung erreichbar.
      · E2E: ja
- [ ] **T15-10** Die Symbole im Header der Bibliothek öffnen die passende Kategorie: Controller-Warnung und
      SISR-Update „Controller“, Arcadia-Update „Über Arcadia“. · E2E: ja

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T15-1, T15-2, T15-4 bis T15-6, T15-9 | 2026-10-22 | Claude | Branch `feat/einstellungsseite-mit-kategorien-im-header-15`, Linux, Avalonia headless gerendert 1920×1080, `--demo`-Bibliothek, Controller-Aktionen ins View-Model, Tastatur über `NoteKeyboard`, Klicks über Headless-Eingaben | ⚠️ Seite, Kategorien, Gruppen, Werte, Ausgrauen, Infopanel mit Scrollen und Fades, Footer für Controller und Tastatur, Klick auf Pfeil, Kategorie und ✕ wie erwartet; echte Tasten und echter Controller nicht, Fadeout der Leiste mangels Kategorien nicht | Lauf am PC des Owners |
| T15-3, T15-7, T15-8 | 2026-10-22 | Claude | wie oben | ⚠️ Unter Linux ist Steam Controller Support „nur unter Windows verfügbar“, kein Update: nur `HealthIssuesTests` (Symbol nur bei Warnung oder Fehler) und `ListFocusTests` | Lauf am PC des Owners |

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
- [ ] **G-7** LB/RB wechseln den Launcher-Tab; bei vielen Tabs dreht sich die Leiste mit und blendet nur
      dort aus, wo es weitergeht (#127). · E2E: ja
- [ ] **G-8** LT/RT blättern die Seiten; die Seitenleiste zeigt die aktuelle Seite. · E2E: ja
- [ ] **G-9** Der Controller funktioniert in jedem der vier XInput-Plätze und nach Aus- und
      Wiedereinstecken während Arcadia läuft. · E2E: nein (echte Hardware)
- [ ] **G-32** Der Fokusrahmen ist überall vollständig sichtbar und nicht abgeschnitten: Spielkachel,
      „Ansicht“ (Sortierung und Seitengröße), Einstellungen und ihre Knöpfe. · E2E: ja
- [ ] **G-33** Kontextmenü in der Bibliothek (Owner, 2026-10-21): Rechtsklick auf eine Kachel öffnet es neben der
      Kachel; ⧉, R und ein Klick auf „Optionen“ in der Vorschau öffnen es über dem Button „Optionen“ (linke und
      untere Kante auf denen des Buttons). · E2E: ja

### Tastatur und Maus

- [ ] **G-10** Tasten (#127): WASD/Pfeile, Leertaste, Esc/Rücktaste, Umschalt+Q/E und Strg+(Umschalt+)Tab
      für Tabs, Q/E und Bild ↑/↓ für Seiten, V für „Ansicht“, Enter/F2 für Einstellungen, R für Optionen,
      F11 Vollbild. · E2E: ja
- [ ] **G-11** Maus: Klick auf ein Spiel fokussiert, Doppelklick startet, die linke Taste 1 s gehalten öffnet
      die Details (mit Ring); LB/RB/LT/RT, Seitenzahlen,
      Tabs, „Ansicht“, Suche und Einstellungen sind klickbar. · E2E: ja

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

### Steam Controller (SISR)

Mit echtem Steam Controller und laufendem Steam. Alle E2E: nein (Steam, SISR, Treiber, UAC).

- [ ] ~~**G-21** Einschalten in den Einstellungen installiert SISR (und usbip-win2 über die
      Windows-Abfrage) mit Ladeanzeige; danach steuert der Steam Controller Arcadia als Gamepad.~~
      Ersetzt durch T85-1 bis T85-4: Installiert wird seit #85 über „SISR installieren“. · E2E: nein
- [ ] **G-22** Ein Nicht-Steam-Spiel aus Arcadia erkennt den Steam Controller als Xbox-Controller. · E2E: nein
- [ ] **G-23** Ausschalten beendet SISR; der Steam Controller ist wieder Maus und Tastatur, Arcadia
      bleibt im Vordergrund. · E2E: nein
- [ ] **G-24** Ein Spiel aus dem Steam-Tab pausiert SISR (keine doppelten Eingaben) und nach
      dem Beenden des Spiels läuft SISR wieder. · E2E: nein
- [ ] ~~**G-25** Pro Spiel lässt sich der Support nur mit dem Controller auf Standard → immer an →
      immer aus schalten, und Arcadia hält sich beim nächsten Start daran.~~
      Ersetzt durch T92-2 bis T92-4 (zwei Modi statt drei); mit dem Controller kommt es mit #10. · E2E: nein
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
- [ ] **G-31** Die Einstellungen (Menü-Taste ≡ / F2) sind komplett per Controller bedienbar, der
      fokussierte Eintrag scrollt ins Bild, B schließt. · E2E: ja

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| G-21 bis G-24 | 2026-10-09 | nicht festgehalten | PC des Owners (laut `docs/controller-support.md`) | ✅ | – |
| G-32 | 2026-10-17 | Juri | PC des Owners, „Ansicht“ | ❌ Fokusrahmen oben und unten abgeschnitten, auch an anderen Stellen | Fix ohne Issue (Fluent-Button schneidet auf seine Größe zu) |
| G-32 | 2026-10-17 | Claude | Branch `fix/focus-ring-clipped`, Linux, Xvfb 1920×1080 `--demo` | ✅ Kachel (auch oberste Reihe), Seitengröße in „Ansicht“ und erste Einstellungszeile mit vollem Rahmen | Kontrolle am PC des Owners |
| G-32 | 2026-10-17 | Juri | PC des Owners, Branch `fix/focus-ring-clipped` | ❌ Sortier-Schalter in „Ansicht“ noch oben und unten abgeschnitten; Kachelrahmen unten dunkler | `ItemsControl` schneidet ebenfalls zu; Schlagschatten lag über dem Rahmen |
| G-32 | 2026-10-17 | Claude | Branch `fix/focus-ring-clipped`, Linux, Xvfb 1920×1080 `--demo` | ✅ Sortier-Schalter mit vollem Rahmen, Kachelrahmen unten so hell wie oben | Kontrolle am PC des Owners |
| G-33 | 2026-10-21 | Claude | Branch `feat/laufende-spiele-anzeigen-94-95`, Linux, Xvfb `--demo`, Maus und Tastatur per xdotool | ✅ Rechtsklick: Menü rechts neben der Kachel; R und Klick auf „Optionen“: Menü über dem Button, unten und links bündig. ⧉ am Controller nicht gesehen (kein XInput unter Linux), läuft aber über denselben Weg wie R | Controller am PC des Owners |

## Explorativer Test

Auftrag: –. Zeitrahmen: –. Funde: –.

## Abnahmetests (E2E)

Wird beim Abschluss-Test aus allen Testfällen mit `E2E: ja` erstellt und in der Abnahme-Iteration
umgesetzt.
