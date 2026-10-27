# #143 Steam Input für ganze Controllertypen legt den Steam Controller lahm

- [x] **T143-1** Steam Controller Support an, Steam Controller und Xbox Controller verbunden, kein Spiel läuft. In
      Steam unter Einstellungen → Controller → Erweiterte Controllereinstellungen Steam Input für Xbox-Controller
      einschalten: Die Einstellungen → Controller → Steam Controller Support warnen „SISR pausiert wegen eines
      anderen Controllers“ (T143-2) mit dem echten Namen des Controllers (z. B. „8BitDo Ultimate 2 …“, kein
      „#controller_…“, kein „XInput Controller #…“, keine USB-ID), „Steam-Eingabe“ statt „Steam Input“ und der
      Empfehlung, sie in Steam nur für den Steam Controller zu verwenden.
      `arcadia.log` nennt den Controller mit Steams Namen und USB-ID. · E2E: nein (echtes Steam mit zwei Controllern)
- [x] **T143-2** Wie T143-1: Innerhalb weniger Sekunden pausiert SISR, die Einstellungen zeigen diese
      Warnung mit „Erneut versuchen“, der Status „Pausiert – die Meldungen unter der Einstellung sagen, warum.“. In Arcadia die
      Maus bewegen und klicken, eine Taste drücken: Die Hinweise und das ✕ der Dialoge bleiben auf Maus/Tastatur, bis
      ein Controller-Knopf gedrückt wird; der Xbox Controller steuert Arcadia normal. · E2E: nein
- [x] **T143-3** Wie T143-2, dann in Steam Steam Input für Xbox-Controller wieder ausschalten: SISR läuft nach
      wenigen Sekunden von selbst wieder, der Steam Controller funktioniert, die Warnung ist weg. · E2E: nein
- [x] **T143-4** Wie T143-2, dann den Xbox Controller ausschalten und „Erneut versuchen“ wählen (mit dem Steam
      Controller über Steams Desktop-Belegung oder der Maus): SISR startet, der Steam Controller funktioniert. · E2E: nein
- [x] **T143-6** Wie T143-2, Steam Input für Xbox bleibt an, den Xbox Controller nicht anfassen: „Erneut versuchen“
      startet SISR sofort, und nach wenigen Sekunden (ohne Eingabe am Xbox Controller) pausiert SISR wieder mit der
      Warnung. Ebenso direkt nach dem Start von Arcadia. · E2E: nein
- [x] **T143-5** Steam Input für Xbox-Controller aus, Steam Controller und Xbox Controller verbunden: SISR läuft
      und pausiert nicht, auch nicht direkt nach dem Start von Arcadia. · E2E: nein

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T143-1 | 2026-10-22 | Claude | Branch `fix/steam-input-fuer-ganze-controllertypen-143`, Linux, ohne .NET SDK; Unit-Tests (`SisrDevicesTests`, `ControllerSupportServiceTests`) nur in CI | ⚠️ App nicht gestartet | Lauf am PC des Owners |
| T143-2 (vorher) | 2026-10-22 | owner | PC des Owners, `main` (Stand #133), Steam Input für Xbox an, 8BitDo Ultimate 2 und Steam Controller | ❌ Hinweise springen zurück auf Controller; `arcadia.log`: SISR bildet `#controller_xbox360`, `XInput Controller #2` und den 8BitDo nach, Phantom-Eingaben (23:59 je 469× linker und rechter Stick oben in einer Minute) | Pause für andere Controller außerhalb von Spielen |
| T143-2 bis T143-5 | 2026-10-22 | Claude | Branch wie oben, Linux, Unit-Tests mit nachgebildetem SISR nur in CI | ⚠️ App nicht gestartet | Lauf am PC des Owners |
| T143-4, T143-6 | 2026-10-22 | owner | PC des Owners, PR #144 (zweiter Stand) | ❌ „Erneut versuchen“ wirkt erst nach einem Moment und startet SISR, ohne dass ein noch an Steam Input hängender Controller erkannt wird; erkannt erst nach einer Eingabe an ihm, ebenso nach dem Start von Arcadia | Erkennung über `steam_virtual_device` (SISR legt sein VIIPER-Gerät erst bei der ersten Eingabe an), Retry sofort |
| T143-6 | 2026-10-22 | Claude | Branch wie oben, Linux, Unit-Tests mit nachgebildetem SISR nur in CI; SISR 0.6.2 Quelltext gelesen | ⚠️ App nicht gestartet | Lauf am PC des Owners |
| T143-1 bis T143-6 | 2026-10-24 | owner | 0.1.0-rc.1 installiert, PC des Owners | ✅ | – |
