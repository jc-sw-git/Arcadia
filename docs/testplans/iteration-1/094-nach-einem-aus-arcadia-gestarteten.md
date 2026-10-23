# #94 Nach einem aus Arcadia gestarteten Spiel kommt Arcadia wieder in den Vordergrund

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
