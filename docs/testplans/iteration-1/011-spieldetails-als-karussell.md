# #11 Spieldetails als Karussell

- [x] **T11-1** A 1 s halten auf einer Kachel und ⧉ → „Details anzeigen“ öffnen die Detailseite des
      Spiels: Pfadleiste „B Bibliothek / <Launcher> / <Titel>“, Position („3 von 31“) und Sortierung
      oben, das Spiel in der Mitte, links und rechts angeschnitten das vorherige und das nächste,
      abgedunkelt und zum Rand hin weich ausgeblendet, ohne sichtbare Kante. Footer: D-Pad Aktion wählen, A Auswählen, Y Ansicht, LB/RB Launcher,
      LT/RT Spiel, B Zurück. · E2E: ja
- [x] **T11-2** LT/RT schieben die Seiten animiert nach rechts bzw. links und zeigen das vorherige bzw.
      nächste Spiel in der Sortierung der Bibliothek, über Seitengrenzen hinweg; vor dem ersten steht
      das letzte, nach dem letzten das erste. Schnelles mehrfaches Drücken springt ohne Lücken weiter.
      Ein Klick auf einen Nachbarn oder auf LT/RT am Rand tut dasselbe, ebenso Bild ↑/↓, Umschalt+←/→
      und das Mausrad (eine Raste, ein Spiel; nach unten das nächste). · E2E: ja
- [x] **T11-3** Ein Tab mit zwei Spielen zeigt das andere links und rechts; mit einem Spiel gibt es
      keine Nachbarn, keine Abdunklung und kein LT/RT. · E2E: ja
- [x] **T11-4** LB/RB (und ein Klick auf einen Tab) wechseln den Launcher, die Detailseite bleibt offen
      und zeigt das erste Spiel des Tabs. · E2E: ja
- [x] **T11-5** Y öffnet „Ansicht“ mit dem Hinweis, dass die Detailseite immer ein Spiel zeigt; eine
      andere Sortierung lässt das aktuelle Spiel in der Mitte, Nachbarn und Position folgen der neuen
      Sortierung. · E2E: ja
- [x] **T11-6** B (und ein Klick auf die Pfadleiste) führt zurück in die Bibliothek, auf die Seite des
      zuletzt gezeigten Spiels mit Fokus darauf. · E2E: ja
- [x] **T11-7** Schaltflächen wie im Kontextmenü, ohne „Details anzeigen“, in drei Reihen (Owner, 2026-10-21):
      oben „A Spielen“ (fokussiert), „Zu Favoriten“ (deaktiviert), „In <Launcher> öffnen“, „Artwork ändern“
      (deaktiviert); darunter die Einstellungen des Spiels, mit verbundenem Steam Controller
      „STEAM CONTROLLER <Modus>“ (A wechselt) und bei „Individuell“ „INDIVIDUELLER MAUSMODUS“ (ohne beide fehlt die
      Reihe, ↑/↓ überspringen sie); zuletzt allein „Deinstallieren …“ in Rot mit derselben Rückfrage wie im Menü.
      Nichts ragt aus der Karte, auch mit beiden Einstellungen und „Standard (deaktiviert)“. Das D-Pad
      überspringt deaktivierte Schaltflächen; unter den Reihen steht der Hinweis der fokussierten
      Schaltfläche (z. B. „Nur in Epic Games möglich – öffnet Epic Games“). ⧉ öffnet hier kein Menü. · E2E: ja
- [x] **T11-8** Mit Update: Chip „UPDATE VERFÜGBAR“, „Spielen“ bleibt, darunter „<Launcher> installiert
      das Update eventuell, bevor das Spiel startet.“ Nicht installiert: „Installieren“, abgedunkeltes
      Cover, „Nicht installiert“ unter „Installiert am“, nur die erste Reihe. Während einer Installation
      zeigt die erste Schaltfläche den Fortschritt und ist deaktiviert. · E2E: ja
- [x] **T11-9** Kennzahlen: Zuletzt gespielt (relativ und mit Datum und Uhrzeit), Spielzeit („Laut
      <Launcher>“), Installiert am (mit Pfad), Größe (mit Laufwerk). Größe und Speicherort stehen nur
      dort, nicht unter dem Titel (Owner, 2026-10-17). · E2E: ja
- [x] **T11-10** LT/RT bis zur sichtbaren Bewegung bleibt unter 50 ms (Release-Build, PC des Owners). · E2E: nein (Messung auf echter Hardware)

Die Version und Downloadgröße des Updates aus dem Entwurf zeigt Arcadia nicht: kein Launcher meldet
sie bisher.

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T11-1, T11-2, T11-4 bis T11-9 | 2026-10-17 | Claude | Branch `feat/context-menu-and-detail-page-10-11`, Linux, Xvfb 1920×1080 `--demo`, Tastatur | ✅ Seite wie im Entwurf; LT/RT mit Animation (Zwischenbild geprüft), 25× RT und B landen auf Seite 3 mit Fokus auf dem Spiel; Q/E bleiben in der Detailansicht; Sortierung hält das Spiel in der Mitte; Enter gehalten öffnet die Seite; nicht installiertes Spiel und Spiel mit Update | Controller am PC des Owners |
| T11-3 | 2026-10-17 | Claude | Unit-Tests (`CarouselTests`) | ✅ Nachbarn rotieren, zwei Spiele stehen beidseitig, ein Spiel hat keine Nachbarn | Sichtprüfung am PC des Owners (die Demo hat keinen Tab mit 1–2 Spielen) |
| T11-10 | 2026-10-17 | Claude | Release-Build, Linux-Container ohne GPU, Xvfb | ✅ Taste bis fertiges Layout 14–39 ms; Öffnen der Seite 199 ms (erstes Mal) | Messung am PC des Owners |
| T11-8, T11-9 | 2026-10-17 | Claude | wie oben, nach dem Code Review | ✅ Unter dem Titel keine Zeile mehr; installiertes Spiel mit Update und nicht installiertes Spiel geprüft | – |
| T11-2 | 2026-10-18 | Juri | PC des Owners, PR #101, Tastatur | ❌ Spiel lässt sich per Tastatur nicht wechseln (Bild ↑/↓ gab es, aber ohne Hinweis); Wunsch: Umschalt+Pfeile und Mausrad | Umschalt+←/→ und Mausrad ergänzt |
| T11-2, T11-7 | 2026-10-18 | Claude | Xvfb 1745×1000 `--demo`, Umschalt+→, Mausrad runter und hoch, Umschalt+← | ✅ 1 → 2 → 3 → 2 → 1 von 31; untere Reihe nur noch „Artwork ändern“ und „Deinstallieren …“ | Mausrad am PC des Owners |
| T11-1 bis T11-10 | 2026-10-24 | Juri | 0.1.0-rc.1 installiert, PC des Owners | ✅ | – |
