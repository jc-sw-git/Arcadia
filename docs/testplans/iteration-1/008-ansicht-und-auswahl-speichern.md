# #8 Ansicht und Auswahl speichern

- [x] **T8-1** Sortierung und Seitengröße in „Ansicht“ (Y) ändern, Arcadia beenden und neu starten:
      Sortierung, Seitengröße und der zuletzt gewählte Tab (LB/RB oder Klick) sind wie zuvor, der Fokus
      liegt auf dem ersten Spiel von Seite 1. · E2E: ja
- [x] **T8-2** Sortierung und Seitengröße gelten in allen Tabs gleich; „Ansicht“ sagt „gültig für alle
      Launcher“. Ein Tab mit zu wenigen Spielen zeigt die nächstkleinere erlaubte Größe, ein großer Tab
      danach wieder die gewählte. · E2E: ja
- [x] **T8-3** Fehlt der zuletzt gewählte Launcher beim Start (z. B. deinstalliert), startet Arcadia im
      ersten Tab ohne Fehler; kommt er wieder, startet Arcadia wieder in ihm. · E2E: ja

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T8-1 | 2026-10-17 | Claude | Branch `feat/save-view-and-tab-8`, Linux, Xvfb 1920×1080 `--demo`, Tastatur (E, V, Pfeile, Enter) | ✅ Nach Neustart EA app, „Zuletzt gespielt · neueste zuerst“, 10 pro Seite, Fokus auf dem ersten Spiel | – |
| T8-3 | 2026-10-17 | Claude | Branch `feat/save-view-and-tab-8`, Linux, Xvfb `--demo`, `LastTabLauncherId` von Hand auf einen unbekannten Launcher gesetzt | ✅ Start im ersten Tab (GOG Galaxy), der gespeicherte Wert bleibt erhalten | Echter fehlender Launcher am PC des Owners |
| T8-1, T8-3 | 2026-10-17 | Claude | Branch `feat/save-view-and-tab-8`, Unit-Tests (`SettingsServiceTests`) | ✅ Neustart, alte `settings.json` ohne die Felder, unbekannte Werte, gleichzeitiges Speichern aus zwei Threads (geht ohne Lock rot) | – |
| T8-1 bis T8-3 | 2026-10-24 | Juri | 0.1.0-rc.1 installiert, PC des Owners | ✅ | – |
