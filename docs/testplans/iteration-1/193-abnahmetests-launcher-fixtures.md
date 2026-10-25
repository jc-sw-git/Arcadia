# #193 Abnahmetests Launcher-Fixtures: Steam und Epic mit nachgestellten Dateien

Keine neuen manuellen Testfälle: Die Abnahmetests automatisieren bestehende Fälle mit `E2E: ja` in
`tests/Arcadia.App.Tests`. Die echten Integrationen von Steam und Epic lesen dort nachgestellte Dateien (erfundene
IDs) aus einem Temp-Ordner. Berührt T1-1, T1-2, T1-5, T1-6, T1-9, T41-3, T41-4, T41-5, T41-6, T41-8, T41-10, T42-1
bis T42-4, T81-1 bis T81-6, T85-15 und T85-19 (Liste in der [Übersicht](README.md#abnahmetests-e2e)).

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| 32 Abnahmetests für 23 Testfälle | 2026-10-25 | Claude | PR-Branch, Linux-Container (.NET 10), `dotnet test` | ❌ T81-5: eine Deinstallation in Steam meldete „„Spiel“ wurde deinstalliert.“, weil der Titel erst nach dem Neueinlesen gesucht wurde | im PR behoben (Titel vor dem Neueinlesen) |
| Dieselben 32 Tests | 2026-10-25 | Claude | PR-Branch, Linux-Container | ✅ grün | CI auf Windows, macOS, Linux |
| Alle 32, je einmal rot | 2026-10-25 | Claude | PR-Branch, Linux-Container, je eine gezielte Änderung im Produktcode | ✅ alle rot, danach wieder grün | Änderungen pro Test im PR |
