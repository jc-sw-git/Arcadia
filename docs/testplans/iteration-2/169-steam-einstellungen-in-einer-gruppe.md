# #169 Steam-Einstellungen in einer Gruppe „Steam“ bündeln

- [ ] **T169-1** Einstellungen → Controller: oben steht eine Box „STEAM“ mit „Steam Controller Kompatibilität“,
      ggf. ihren Meldungen und „SISR aktualisieren“, darunter „Steams Desktop-Belegung
      ignorieren“ („Alle Controller: …“). Danach „MAUSMODUS“ und „ZURÜCK ZU ARCADIA“; eine Box „STEAM CONTROLLER“ gibt
      es nicht mehr. · E2E: ja
- [ ] **T169-2** „Steams Desktop-Belegung ignorieren“ ist nicht eingerückt und bleibt bedienbar (◀ ▶ schalten um),
      auch bei ausgeschalteter Steam Controller Kompatibilität und bei Mausmodus „Aus“. · E2E: ja
- [ ] **T169-3** Nur mit dem Controller: ▲ ▼ erreichen alle Zeilen in der neuen Reihenfolge, die Liste scrollt mit,
      B geht zurück. · E2E: ja
- [ ] **T169-4** Einstellungen → Launcher → Steam (aktiv oder nicht): unten eine Box „CONTROLLER“ mit
      „Controller-Einstellungen für Steam“ und „Öffnen“; ihr Infotext zeigt A und B als Glyphen; auf den Seiten der anderen Launcher fehlt sie. · E2E: ja
- [ ] **T169-5** A (Leertaste, Klick) auf „Öffnen“ zeigt die Kategorie „Controller“ mit dem Fokus auf „Steam Controller
      Kompatibilität“; B (Esc) führt zurück auf Steams Seite, Fokus auf der Verweis-Zeile, ein weiteres B zur Launcher-Liste.
      Wechselt man nach dem Sprung mit LB/RB die Kategorie, gilt B wie sonst (in „Controller“ schließt es die Einstellungen). · E2E: ja

- [ ] **T169-6** „Steam Controller Kompatibilität“ aus oder nicht installiert: Untertitel „Erlaubt es, den Steam
      Controller auch in Spielen außerhalb von Steam zu verwenden“. An: stattdessen der Status in einer Zeile (z. B.
      „Aktiv – der Steam Controller läuft als Xbox Controller“, „Pausiert, solange ein Spiel mit „Steam Input“ läuft“). · E2E: nein (SISR unter Windows)
- [ ] **T169-7** Die Lampe neben dem Namen: grün, solange SISR läuft und keine Meldung da ist; grau, wenn an, aber
      pausiert (Steam-Spiel mit „Steam Input“), SISR startet oder kein Steam Controller gefunden; gelb, sobald eine
      Meldung mit Warndreieck oder rotem X darunter steht (z. B. Steam beenden). Aus: keine Lampe. · E2E: nein (SISR und Steam unter Windows)
- [ ] **T169-8** Meldungen und „SISR aktualisieren“ stehen direkt unter der Kompatibilität, eine Stufe eingerückt,
      vor „Steams Desktop-Belegung ignorieren“ (nicht eingerückt). · E2E: nein (SISR unter Windows)

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T169-1, T169-3 | 2026-10-24 | Claude | Branch `feat/steam-einstellungen-in-einer-gruppe-169`, Linux, Xvfb 1920×1080, `--demo`, Tasten per xdotool; `dotnet test` | ✅ Box „STEAM“ oben mit beiden Zeilen, darunter „MAUSMODUS“; S, S führt von der Kompatibilität über die Desktop-Belegung zum Mausmodus. Unter Linux ist SISR nicht verfügbar, daher weder Meldungen noch Update noch der Untertitel „Nur Steam Controller“ zu sehen | T169-1 mit SISR und T169-2 am PC des Owners |
| T169-4, T169-5 | 2026-10-24 | Claude | dito, `--demo` (Steam nicht gefunden, nicht aktiv) | ✅ Box „CONTROLLER“ auf Steams Seite, Öffnen springt auf „Steam Controller Kompatibilität“, Esc zurück auf die Verweis-Zeile; nach ⇧E, ⇧Q schließt Esc die Einstellungen. Klick und die anderen Launcher-Seiten nicht angesehen | Am PC des Owners |
| T169-4 | 2026-10-24 | Juri | PR #170 | ❌ Infotext des Verweises schreibt A und B aus statt Glyphen | Behoben in PR #170 ([A], [B]; dazu „Y“-Zeile der Mausmodus-Hilfe), neuer Test `ButtonGlyphTextTests` |
| T169-4 | 2026-10-24 | Claude | dito, Linux, Xvfb, `--demo` | ✅ Infotext zeigt [A] und [B] als Glyphen, Mausmodus-Hilfe „Bildschirmtastatur ([B] blendet sie aus)“ ebenso | Am PC des Owners |
| T169-6 bis T169-8 | 2026-10-24 | Claude | Branch `feat/steam-einstellungen-in-einer-gruppe-169`, Linux, nur Build und Tests | ➖ nicht ausführbar: unter Linux gibt es SISR nicht, die Zeile zeigt nur „Momentan nur unter Windows verfügbar.“ | Am PC des Owners |
