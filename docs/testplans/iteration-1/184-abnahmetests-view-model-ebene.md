# #184 Abnahmetests auf View-Model-Ebene für die Kernabläufe

Keine neuen manuellen Testfälle: Die Abnahmetests automatisieren bestehende Fälle mit `E2E: ja` in
`tests/Arcadia.App.Tests`. Berührt G-1, G-6, G-7, G-8, G-12, G-13, G-14, G-31, T8-1, T10-4, T11-1,
T11-2, T11-6, T12-3, T12-9, T15-1, T139-1 und T139-3 (Liste in der [Übersicht](README.md#abnahmetests-e2e)).

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| Erste 17 Abnahmetests | 2026-10-24 | Claude | PR-Branch, Linux-Container (.NET 10), `dotnet test` | ✅ grün | CI auf Windows, macOS, Linux |
| Alle 17 Abnahmetests, je einmal rot | 2026-10-24 | Claude | PR-Branch, Linux-Container, je eine gezielte Änderung im Produktcode | ✅ alle 17 rot, danach wieder grün | Änderungen pro Test im PR |
| Nachbesserung nach Codex-Review (Neustart, Halten mit Zwischenzuständen, Suche nach Relevanz, Einstellungen bedienen, alle Sortierungen, gesperrte Launcher): 25 Tests | 2026-10-24 | Claude | PR-Branch, Linux-Container | ✅ grün; die 12 neuen Prüfungen je einmal rot | Abdeckung je Testfall in der Übersicht |
| „Kein Start“ ohne Wartezeit: synchrones Signal `IsStarting` statt `SettleAsync` (zweites Codex-Review) | 2026-10-24 | Claude | PR-Branch, Linux-Container | ✅ grün; Start beim Drücken, nach dem Rahmen und nach den Details je einmal rot | – |
