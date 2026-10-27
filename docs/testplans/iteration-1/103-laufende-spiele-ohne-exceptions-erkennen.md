# #103 Laufende Spiele ohne Exceptions erkennen

- [x] **T103-1** Arcadia im Debugger (Rider/Visual Studio) starten, ein Steam-Spiel starten (SISR pausiert) und
      eine Minute laufen lassen: Die Debug-Konsole zeigt keine Serien von `Win32Exception` („Zugriff verweigert“)
      oder `InvalidOperationException` mehr; beim Start keine Binding-Fehler „Value is null“, auch nicht beim
      Öffnen von Detailseite, Kontextmenü und Suche mit Tastatur. · E2E: nein (Debugger, echtes Spiel)
- [x] **T103-2** Eine Kopie eines kleinen Programms (z. B. `notepad.exe`) in den Ordner eines Steam-Spiels legen.
      Das Spiel aus Arcadia starten (SISR pausiert), die Kopie per Rechtsklick „Als Administrator ausführen“
      starten, dann das Spiel beenden: SISR bleibt pausiert, bis auch das Programm beendet ist. · E2E: nein (UAC)
- [x] **T103-3** CPU von Arcadia neben einem laufenden Spiel mit pausiertem SISR, vorher (`main`) und nachher
      (dieser Build), je 60 s: `$p = Get-Process Arcadia; $t = $p.TotalProcessorTime; Start-Sleep 60; $p.Refresh();`
      `($p.TotalProcessorTime - $t).TotalSeconds / 60 / [Environment]::ProcessorCount * 100` (Prozent). Nachher
      nicht höher als vorher, Ziel < 1 %. · E2E: nein (echtes Spiel, Messung am PC des Owners)

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T103-1 (nur Binding-Fehler) | 2026-10-20 | Claude | Branch `fix/processwatch-103-xq8e1u`, Linux, Xvfb 1920×1080 `--demo`, Avalonia-Log lokal auf die Konsole umgeleitet (nicht eingecheckt), Tastatur und Maus per xdotool | ✅ vorher 22 Binding-Fehler beim Start, 4 beim Öffnen der Suche, 50 beim Ausklappen der Tastatur; nachher keiner. Bibliothek, Detailseite, Kontextmenü, Suche und Einstellungen sehen aus wie vorher | Exceptions, Adminrechte und CPU nur unter Windows prüfbar |
| T103-1 bis T103-3 (Logik) | 2026-10-20 | Claude | `dotnet test` (`ProcessWatchTests`), Linux | ⚠️ Ordner des eigenen Prozesses wird gefunden, leerer Ordner nicht; den Windows-Weg (`PathOf`) prüft nur die CI unter Windows | Windows, Adminrechte und CPU am PC des Owners |
| T103-1 bis T103-3 | 2026-10-24 | owner | 0.1.0-rc.1 installiert, PC des Owners | ✅ | – |
