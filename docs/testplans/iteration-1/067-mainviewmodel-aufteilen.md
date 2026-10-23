# #67 MainViewModel aufteilen

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
