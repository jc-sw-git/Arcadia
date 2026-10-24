# #187 Abnahmetests View-Model: Bibliothek, Detailseite, Spielstart und App-Start

Keine neuen manuellen Testfälle: Die Abnahmetests automatisieren bestehende Fälle mit `E2E: ja` in
`tests/Arcadia.App.Tests`. Berührt T8-2, T8-3, T10-3, T11-3, T11-4, T11-5, T11-8, T11-9, T27-1, T27-3, T41-7,
T41-9, T95-8, G-2, G-4, G-5, G-6, G-15, G-20 und G-30 (Abdeckung je Fall in der [Übersicht](README.md#abnahmetests-e2e)).

Produktcode, den die Tests dafür brauchten: `GamepadPolls` (aus `XInputGamepadSource` gelöst: Wiederholung beim
Halten, linker Stick als Steuerkreuz), die Uhr stellt sich über `TimeProvider` selbst (`StatusBarViewModel`, vorher ein
Timer in `App.axaml.cs`). Berührt damit auch G-6 und G-30 im Abschluss-Test.

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| 22 neue Abnahmetests, 6 Tests für `GamepadPolls` | 2026-10-24 | Claude | PR-Branch, Linux-Container (.NET 10), `dotnet test` | ✅ grün | CI auf Windows, macOS, Linux |
| Alle 22 Abnahmetests, je einmal rot | 2026-10-24 | Claude | PR-Branch, Linux-Container, je eine gezielte Änderung im Produktcode | ✅ alle 22 rot, danach wieder grün | Änderungen pro Test im PR |
