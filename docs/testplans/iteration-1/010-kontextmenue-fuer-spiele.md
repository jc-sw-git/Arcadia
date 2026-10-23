# #10 Kontextmenü für Spiele

- [ ] **T10-1** ⧉ (View-Taste), Rechtsklick auf ein Cover, „⧉ Optionen“ in der Vorschau und die
      Kontextmenü-Taste der Tastatur öffnen das Menü des Spiels; Hoch/Runter überspringen deaktivierte
      Einträge, B und ⧉ schließen es, ein Klick daneben auch. Das Menü steht rechts neben dem Cover,
      in der rechten Spalte links davon, immer ganz im Fenster. ≡ (Menü-Taste) öffnet die Einstellungen. · E2E: ja
- [ ] **T10-2** Installiertes Spiel: Spielen, Details anzeigen, Zu Favoriten hinzufügen und Artwork
      ändern (beide deaktiviert, „Folgt in einer späteren Version“), In <Launcher> öffnen,
      Trennstrich, Deinstallieren … in Rot. Kein „Controller-Layout anzeigen“ (Owner, 2026-10-18: Steam öffnet es
      nicht zuverlässig, siehe `docs/launchers/steam.md`). Mit Update steht unter
      „Spielen“ der Hinweis, dass der Launcher ggf. vorher aktualisiert; es gibt keinen Update-Eintrag. · E2E: ja
- [ ] **T10-3** Nicht installiertes Spiel: Installieren, Details anzeigen, Zu Favoriten hinzufügen,
      In <Launcher> öffnen. Während einer Installation steht statt „Installieren“ der Status
      (z. B. „Wird installiert · 42 %“), deaktiviert. · E2E: ja
- [ ] **T10-4** A kurz drücken startet das Spiel beim Loslassen. A halten: nach etwa 0,2 s füllt sich
      ein blauer Rahmen um die Vorschau links, nach 1 s öffnet sich die Detailseite, ohne loszulassen.
      Loslassen dazwischen bricht ab, nichts passiert. Enter verhält sich gleich. · E2E: ja
- [ ] **T10-5** Steam-Spiel: „In Steam öffnen“ zeigt die Seite des Spiels in der Steam-Bibliothek. · E2E: nein (echter Steam-Client)
- [ ] **T10-6** Steam-Spiel deinstallieren: Arcadia fragt nach (Fokus auf „Abbrechen“, A dort
      bricht ab); „Deinstallieren“ öffnet Steams eigene Deinstallationsfrage, danach verschwindet das
      Spiel aus der Bibliothek. · E2E: nein (echter Steam-Client)
- [ ] **T10-7** Epic-Spiel: „In Epic Games öffnen“ öffnet die Epic-Bibliothek; „Deinstallieren …“
      sagt „Nur in Epic Games möglich – öffnet Epic Games“ und öffnet die Bibliothek ohne Rückfrage. · E2E: nein (echter Epic-Client)
- [ ] **T10-8** Mit eingeschalteter Option und verbundenem Steam Controller zeigt das Menü
      „Steam Controller: <Modus>“; A wechselt den Modus, das Menü bleibt offen und zeigt den neuen
      Modus, die Vorschau ebenso. Ohne Steam Controller fehlt der Eintrag. · E2E: nein (echter Steam Controller)
- [ ] **T10-9** Liest Arcadia die Bibliothek neu ein, während A gedrückt ist (z. B. wenn es nach dem
      Ende eines Spiels nach vorn kommt), zählt der Druck weiter: Tippen startet das Spiel, Halten
      öffnet die Detailseite, der Rahmen läuft durch. · E2E: nein (der Zeitpunkt des Einlesens lässt sich nicht steuern)

Die Steam-Links `steam://nav/games/details` und `steam://uninstall` sowie
der Epic-Link `com.epicgames.launcher://store/library` sind noch nicht am echten Client geprüft (T10-5
bis T10-7).

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T10-1, T10-2, T10-4 | 2026-10-17 | Claude | Branch `feat/context-menu-and-detail-page-10-11`, Linux, Xvfb 1920×1080 `--demo`, Tastatur (Pfeile, Kontextmenü-Taste, Enter gehalten) | ✅ Menü eines GOG-Spiels mit deaktivierten Einträgen und Begründung; Enter 1,2 s gehalten öffnet (noch) den Platzhalter der Detailseite, der Rahmen füllt sich sichtbar | Controller am PC des Owners |
| T10-1 | 2026-10-17 | Claude | wie oben, Menü an der Kachel | ✅ Menü rechts neben dem Cover in der Mitte, links neben dem Cover in der rechten Spalte | – |
| T10-4 | 2026-10-17 | Juri | PC des Owners, PR #101 | ❌ Der Rahmen bricht manchmal auf den letzten 5–10 % ab | Haltezeit lief ab dem Drücken, die Animation erst ab ihrem (verspäteten) Start |
| T10-4 | 2026-10-17 | Claude | PR #101, Unit-Test (geht vorher rot) und Xvfb `--demo`, Enter 0,93 s gehalten | ✅ Rahmen geschlossen, bevor die Detailseite öffnet | Kontrolle am PC des Owners |
| T10-6 | 2026-10-17 | Claude | wie oben | ✅ Rückfrage mit Fokus auf „Abbrechen“ (Demo-Spiel, ohne echten Launcher) | Steam am PC des Owners |
| T10-4 | 2026-10-17 | Claude | Unit-Tests (`HoldGestureTests`) | ✅ Tippen, Abbrechen, Halten, wiederholte KeyDown-Ereignisse, verspätete Timer (geht ohne Zählung der Drücke rot) | – |
| T10-9 | 2026-10-17 | Claude | Xvfb `--demo`, Debug-Build mit vorübergehendem Einlesen 0,1 s nach jedem Druck auf Enter (wieder entfernt) | ✅ Enter 0,12 s: Spiel startet (Toast); Enter 1,5 s gehalten: Rahmen läuft durch, Detailseite öffnet | – |
| T10-2 | 2026-10-18 | Claude | Xvfb 1745×1000 `--demo`, Kontextmenü-Taste | ✅ Menü ohne „Controller-Layout anzeigen“ | – |
