# #188 Abnahmetests View-Model: Suche und Bildschirmtastatur

Keine neuen manuellen Testfälle: Die Abnahmetests automatisieren bestehende Fälle mit `E2E: ja` in
`tests/Arcadia.App.Tests`. Berührt T12-2, T12-3, T12-7, T12-8, T12-9, T12-13, T12-15, T110-11, T110-13,
T127-3, T127-4 und T129-1 bis T129-6 (Liste in der [Übersicht](README.md#abnahmetests-e2e)).

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| 19 neue Abnahmetests (Suche, Treffer, Filter, Bildschirmtastatur) | 2026-10-24 | Claude | PR-Branch, Linux-Container (.NET 10), `dotnet test` | ✅ grün nach Fix | T129-6 fand einen Absturz: Layoutwechsel zu US mit AltGr an (KeyNotFound), im selben PR behoben |
| Alle 19, je einmal rot | 2026-10-24 | Claude | PR-Branch, Linux-Container, je eine gezielte Änderung im Produktcode | ✅ alle rot, danach wieder grün | Änderungen pro Test im PR |
| Nachbesserung nach Codex-Review: Start beim Loslassen über `IsStarting` statt über den erst später befüllten Recorder; Tottasten auch in ES | 2026-10-25 | Claude | PR-Branch, Linux-Container | ✅ grün; Start beim Drücken nur für installierte Treffer und Tottasten nur in ES je einmal rot | – |
