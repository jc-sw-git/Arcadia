# #27 Spielstart robust machen

- [x] **T27-1** Ein Spiel, dessen Start scheitert (z. B. die Exe eines Epic-Spiels umbenannt oder
      gesperrt), zeigt einen Hinweis „… konnte nicht gestartet werden“; Arcadia läuft weiter und ein
      zweiter Start mit A versucht es erneut. · E2E: ja (Fake-Launcher, der beim Start wirft)
- [x] **T27-2** Startet der Epic Games Launcher nicht innerhalb einer Minute (z. B. ohne Netzwerk
      oder im Task-Manager sofort beendet), zeigt Arcadia „Epic Games Launcher reagiert nicht …“ und
      später „… ist nicht gestartet – bitte in Epic nachsehen“, statt still „bereit“ zu melden. · E2E: nein (echter Epic Launcher)
- [x] **T27-3** Nach einem unerwarteten Fehler steht er mit Stacktrace in `arcadia.log`, ein Hinweis
      nennt das Log, und Arcadia bleibt mit dem Controller bedienbar. · E2E: ja (Fake-Launcher, der beim Start wirft)

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T27-1, T27-3 | 2026-10-12 | Claude | Branch `claude/project-thread-y4c95v`, Linux, Unit-Tests mit Fake-Launcher (`GameLaunchTests`), nicht in der App | ✅ Fehler gemeldet und geloggt, zweiter Start läuft | Ansicht in Arcadia am PC des Owners |
| T27-2 | 2026-10-12 | Claude | dito, Unit-Test mit Fake-Prozessen und `FakeTimeProvider` | ✅ Stufen `StartingLauncher → LauncherNotReady → NotConfirmed`, URI zweimal geschickt | Echter Epic Launcher am PC des Owners |
| T27-1 bis T27-3 | 2026-10-24 | Juri | 0.1.0-rc.1 installiert, PC des Owners | ✅ | – |
