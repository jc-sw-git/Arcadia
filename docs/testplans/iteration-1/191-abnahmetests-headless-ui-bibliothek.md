# #191 Abnahmetests Headless-UI: Bibliothek, Detailseite, Header, Maus und Tastatur

Keine neuen manuellen Testfälle: Die Tests automatisieren bestehende Fälle mit `E2E: ja` mit dem echten Fenster
(Avalonia Headless mit Skia) in `tests/Arcadia.App.Tests/Window`. Berührt T10-1, T10-2, T10-4, T11-1, T11-2, T11-6,
T11-7, T41-1, T102-1 bis T102-4, T127-1, T127-5 bis T127-8, T127-10, T138-1, G-7, G-10, G-11, G-32, G-33 und T8-1
(Liste und Abdeckung in der [Übersicht](README.md#abnahmetests-e2e)).

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| 26 Headless-Tests für 25 Testfälle | 2026-10-25 | Claude | PR-Branch, Linux-Container (.NET 10, Skia), `dotnet test` | ✅ grün | CI auf Windows, macOS, Linux |
| Alle Headless-Tests je einmal rot | 2026-10-25 | Claude | PR-Branch, Linux-Container, je eine gezielte Änderung im Produktcode | ✅ alle rot, danach wieder grün | Änderungen pro Test im PR |
