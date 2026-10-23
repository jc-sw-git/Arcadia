# #68 Launcher als ein Bündel registrieren

Keine neuen Testfälle: Umbau ohne sichtbare Änderung; dass deaktivierte Launcher weder eingelesen
noch überwacht werden, prüfen Unit-Tests (`LauncherRegistryTests`), die Auswahl selbst kommt mit
#15. Berührt G-1, G-2, G-7, G-17 bis G-20, T41-2 bis T41-6 und T42-1.

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| G-1, G-2, G-7, G-31 | 2026-10-12 | Claude | PR-Branch, Linux mit virtuellem Display, mit und ohne `--demo` | ✅ Tab-Reihenfolge wie vorher, Hinweis ohne Launcher | Epic-Start und -Installation am PC des Owners |
