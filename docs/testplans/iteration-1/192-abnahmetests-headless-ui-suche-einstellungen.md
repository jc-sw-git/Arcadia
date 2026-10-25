# #192 Abnahmetests Headless-UI: Suche, Tastatur, Einstellungen und Launcher-Seiten

Keine neuen manuellen Testfälle: Die Tests automatisieren bestehende Fälle mit `E2E: ja` mit dem echten Fenster
(Avalonia Headless mit Skia) in `tests/Arcadia.App.Tests/Window`. Berührt T12-1, T12-4 bis T12-6, T12-10, T12-12,
T12-16, T12-18, T15-1, T15-2, T15-4, T15-6, T15-9, G-31, T64-11, T92-3, T92-5, T110-4, T110-10, T110-14, T127-9,
T129-7, T136-2, T136-8, T137-1, T137-4, T137-8, T139-3 und T139-6 (Liste und Abdeckung in der
[Übersicht](README.md#abnahmetests-e2e)).

Abweichungen der Testfälle vom heutigen Verhalten (die Tests prüfen das heutige): Die Bildschirmtastatur der Suche
startet auf „a“ (T12-1 nennt „q“); die Kategorien wechseln seit #137 mit ⇧ Q/⇧ E (T15-2, T15-9 nennen Q/E);
„So funktioniert's“ ist seit #15 eine Box im Infotext, kein Dialog (T64-11); der Zustand eines Launchers steht im
Infopanel, nicht im Kopf seiner Seite (T137-1).

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| 36 Headless-Tests für 29 Testfälle | 2026-10-25 | Claude | PR-Branch, Linux-Container (.NET 10, Skia), `dotnet test` | ✅ grün | CI auf Windows, macOS, Linux |
| Alle Headless-Tests je einmal rot | 2026-10-25 | Claude | PR-Branch, Linux-Container, je eine gezielte Änderung im Produktcode | ✅ alle rot, danach wieder grün | Änderungen pro Test im PR |
