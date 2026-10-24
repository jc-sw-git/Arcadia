# #169 Steam-Einstellungen in einer Gruppe „Steam“ bündeln

- [ ] **T169-1** Einstellungen → Controller: oben steht eine Box „STEAM“ mit „Steam Controller Kompatibilität“
      („Nur Steam Controller: …“), ggf. ihren Meldungen und „SISR aktualisieren“, darunter „Steams Desktop-Belegung
      ignorieren“ („Alle Controller: …“). Danach „MAUSMODUS“ und „ZURÜCK ZU ARCADIA“; eine Box „STEAM CONTROLLER“ gibt
      es nicht mehr. · E2E: ja
- [ ] **T169-2** „Steams Desktop-Belegung ignorieren“ ist nicht eingerückt und bleibt bedienbar (◀ ▶ schalten um),
      auch bei ausgeschalteter Steam Controller Kompatibilität und bei Mausmodus „Aus“. · E2E: ja
- [ ] **T169-3** Nur mit dem Controller: ▲ ▼ erreichen alle Zeilen in der neuen Reihenfolge, die Liste scrollt mit,
      B geht zurück. · E2E: ja

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T169-1, T169-3 | 2026-10-24 | Claude | Branch `feat/steam-einstellungen-in-einer-gruppe-169`, Linux, Xvfb 1920×1080, `--demo`, Tasten per xdotool; `dotnet test` | ✅ Box „STEAM“ oben mit beiden Zeilen, darunter „MAUSMODUS“; S, S führt von der Kompatibilität über die Desktop-Belegung zum Mausmodus. Unter Linux ist SISR nicht verfügbar, daher weder Meldungen noch Update noch der Untertitel „Nur Steam Controller“ zu sehen | T169-1 mit SISR und T169-2 am PC des Owners |
