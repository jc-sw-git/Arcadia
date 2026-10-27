# #100 Beenden von Arcadia bricht einen laufenden SISR-Start ab

Mit echtem Steam Controller und Steam.

- [x] **T100-1** Steam Controller Support einschalten und Arcadia sofort schließen (Fenster ✕ oder Alt+F4, solange
      SISR noch startet, in den ersten Sekunden). Nach höchstens 8 Sekunden ist Arcadia weg und es läuft kein
      `SISR.exe` und kein `viiper.exe` mehr (Task-Manager); in `arcadia.log` steht „SISR wird beendet“ und
      „SISR beendet (läuft noch: False)“. · E2E: nein (echtes SISR)
- [x] **T100-2** Steam beenden, Support einschalten (Arcadia startet Steam, „Steam wird gestartet …“) und Arcadia
      währenddessen schließen: Arcadia schließt ohne die Wartezeit von einer Minute, es startet kein SISR. · E2E: nein (echtes Steam)
- [ ] **T100-3** Neustart für ein Update bei laufendem SISR: SISR läuft weiter (unverändert, T23). · E2E: nein (Update)

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T100-1, T100-2 (Logik) | 2026-10-21 | Claude | Branch `td/sisr-shutdown-and-usbip-signature-100-29`, Linux, `dotnet test` (`ControllerSupportServiceTests`, Fake-SISR, `FakeTimeProvider`) | ✅ vorher rot (SISR, das `quit` ignoriert, hielt das Beenden auf; Warten auf Steam wurde nicht abgebrochen), nachher grün | Echter Ablauf am PC des Owners |
| T100-1, T100-2 | 2026-10-24 | owner | 0.1.0-rc.1 installiert, PC des Owners | ✅ | – |
| T100-3 | 2026-10-24 | owner | 0.1.0-rc.1 installiert, PC des Owners | ⏭️ Nicht getestet: kein neueres Release als 0.1.0-rc.1, auf das aktualisiert werden kann | Beim Update auf das nächste Release, mit T23-3 bis T23-6 |
