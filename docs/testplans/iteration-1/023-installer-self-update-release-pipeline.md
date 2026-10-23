# #23 Installer, Self-Update, Release-Pipeline

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
