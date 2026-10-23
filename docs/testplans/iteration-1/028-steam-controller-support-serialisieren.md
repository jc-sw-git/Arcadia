# #28 Steam Controller Support serialisieren

Mit echtem Steam Controller und laufendem Steam.

- [ ] **T28-1** Steam Controller Support in den Einstellungen schnell hintereinander mehrmals aus- und
      einschalten: Am Ende entspricht der Zustand dem Schalter (aus = kein SISR-Prozess, an = Controller
      als Gamepad), es bleibt kein zweiter SISR-Prozess. · E2E: nein (echter Steam Controller, SISR)
- [ ] **T28-2** Ein Steam-Spiel aus Arcadia starten (SISR pausiert), während es läuft den Support
      ausschalten, dann das Spiel beenden: SISR startet nicht wieder. Wieder einschalten, während ein
      solches Spiel läuft: Status „Pausiert“, SISR startet erst nach dem Spiel. · E2E: nein (echter Steam Controller, SISR)
- [ ] ~~**T28-3** Ohne Netzwerk Arcadia starten und den Support einschalten (SISR nicht installiert): nach
      spätestens etwa 20 Sekunden eine Meldung in den Einstellungen, Arcadia bleibt bedienbar.~~
      Ersetzt durch T85-6: Einschalten installiert seit #85 nichts mehr, das macht „SISR installieren“. · E2E: nein (SISR-Installation)

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T28-1 bis T28-3 | 2026-10-12 | Claude | Branch `claude/project-thread-y4c95v`, Linux, Unit-Tests mit Fake-SISR, Fake-API und `FakeTimeProvider` (`ControllerSupportServiceTests`), nicht in der App | ✅ überlappende Ticks starten SISR einmal; Ausschalten während Tick oder Pause lässt SISR aus; GitHub-Timeout und abgebrochener Download melden sich; nach 3 Abstürzen in 5 min kein Neustart | Echter Steam Controller am PC des Owners |
