# #190 Abnahmetests View-Model: Launcher-Einstellungen und Ersteinrichtung

Keine neuen manuellen Testfälle: Die Abnahmetests automatisieren bestehende Fälle mit `E2E: ja` in
`tests/Arcadia.App.Tests`. Berührt T136-4 bis T136-7, T136-14 bis T136-16, T137-2, T137-3, T137-5 bis T137-7,
T139-1, T139-2, T139-4, T139-5 und T139-7 (Liste in der [Übersicht](README.md#abnahmetests-e2e)).

Fund: Ein leer übernommener Name im Tab wurde abgelehnt („Der Name darf nicht leer sein.“), statt wieder der
Standardname zu werden (T137-5, `docs/product/launchers.md`); im selben PR behoben.

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| 30 neue und 2 erweiterte Abnahmetests (Launcher-Einstellungen, Launcher-Seite, Ersteinrichtung) | 2026-10-25 | Claude | PR-Branch, Linux-Container (.NET 10), `dotnet test` | ❌ T137-5: leerer Name abgelehnt; sonst ✅ | Behoben, danach alle grün |
| Alle 32, je einmal rot | 2026-10-25 | Claude | PR-Branch, Linux-Container, je eine gezielte Änderung im Produktcode | ✅ alle rot, danach wieder grün | Änderungen pro Test im PR |
