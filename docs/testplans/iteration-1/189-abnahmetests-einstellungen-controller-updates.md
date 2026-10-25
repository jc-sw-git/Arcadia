# #189 Abnahmetests View-Model: Einstellungen, Mausmodus, Controller-Support und Updates

Keine neuen manuellen Testfälle: Die Abnahmetests automatisieren bestehende Fälle mit `E2E: ja` in
`tests/Arcadia.App.Tests`. Berührt T15-5, T15-10, T15-11, T23-2, T23-4, T64-7, T64-8, T64-10, T64-12,
T64-15, T85-1, T85-2, T85-9, T85-11, T85-14, T85-16, T92-1, T92-2, T110-9 und T127-2 (Abdeckung in der
[Übersicht](README.md#abnahmetests-e2e)). Steam, SISR und GitHub sind Fakes (`FakeSteam`, `FakeSisr`,
`FakeNetwork`, `FakeReleases`); das Tasten-Routing für ⧉ und ≡ ist dafür aus `App.axaml.cs` in
`GamepadRouting` gezogen, das App und Tests gemeinsam nutzen.

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| 25 neue Abnahmetests (20 Testfälle) | 2026-10-24 | Claude | PR-Branch, Linux-Container (.NET 10), `dotnet test` | ✅ grün | CI auf Windows, macOS, Linux |
| Alle 25 Tests, je einmal rot | 2026-10-25 | Claude | PR-Branch, Linux-Container, je eine gezielte Änderung im Produktcode | ✅ alle rot, danach wieder grün | Änderungen pro Test im PR |
| Nach Codex-Review: B während Steam- und SISR-Start (T85-14), Tasten bei Spiel im Vordergrund und im Mausmodus (T64-10) | 2026-10-25 | Claude | PR-Branch, Linux-Container | ✅ grün; je einmal rot (B während der Sperre verschluckt; Sperren in `GamepadRouting` entfernt) | – |
