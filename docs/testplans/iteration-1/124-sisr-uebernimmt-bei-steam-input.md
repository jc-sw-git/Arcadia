# #124 SISR übernimmt bei Steam Input für Xbox-Controller auch den Xbox Controller

- [x] **T124-1** Steam Controller Support an, Steam Controller und Xbox Controller verbunden. In einem
      Steam-Spiel Steam Input für alle Controller einschalten und das Spiel aus Arcadia auf „Xbox 360
      Controller (SISR)“ starten: SISR ist schon vor dem Spielstart beendet, das Spiel nimmt sofort Eingaben
      vom Xbox Controller an. `arcadia.log` nennt Steams Einstellung und dass SISR pausiert, die Einstellungen
      zeigen „SISR pausiert für „…““. Auch beim ersten Spielstart nach dem Start von Arcadia. · E2E: nein
      (echtes Steam mit zwei Controllern)
- [x] **T124-2** Wie T124-1, aber auf „Steam Input“ gestartet: Das Spiel nimmt Eingaben von beiden
      Controllern an. · E2E: nein (echtes Steam mit zwei Controllern)
- [x] **T124-3** Wie T124-1, mit dem Steam Controller gespielt: Das Spiel bekommt ihn über Steam Input. Nach
      dem Beenden läuft SISR wieder, der Hinweis ist weg und der Steam Controller wird in Arcadia wieder als
      verbunden angezeigt. · E2E: nein (echtes Steam mit Steam Controller)
- [x] **T124-4** Nur der Xbox Controller ist verbunden, und SISR übernimmt ihn (Steam Input für ihn an):
      Arcadia zeigt keinen Steam Controller als verbunden an (keine Modus-Auswahl in der Vorschau). · E2E: nein
- [x] **T124-5** Ein Steam-Spiel ohne Steam Input für Xbox-Controller verhält sich wie bisher (G-24, T92-2). · E2E: nein
- [x] **T124-7** Wie T124-1, aber das Spiel aus Steam selbst starten, während Arcadia läuft: SISR pausiert
      innerhalb weniger Sekunden, danach nimmt das Spiel Eingaben vom Xbox Controller an. · E2E: nein
- [x] **T124-8** Im selben Spiel Steam Input nur für einen Teil der Controller-Typen einschalten (z. B. ohne
      Xbox) und auf „Xbox 360 Controller (SISR)“ starten: `arcadia.log` nennt, was Steam dafür in
      `localconfig.vdf` schreibt; SISR pausiert nur, wenn der Xbox Controller sonst keine Eingaben hätte. · E2E: nein
- [x] **T124-6** Am PC des Owners: Was SISRs `/api/v1/devices` für den Steam Controller (Puck 28DE:1304) und
      ein Xbox-Pad als `real_device` meldet, passt zur Erkennung in `docs/controller-support/`
      (`arcadia.log` beim Start von SISR und bei T124-1). · E2E: nein
- [x] **T124-9** Statussymbole im Header und in den Einstellungen → Steam Controller Support: SISR für ein
      Spiel pausiert (T124-1) zeigt das blaue Info-Symbol (Kreis mit i), und der Status bleibt „bereit“;
      Steam beendet oder SISR deinstalliert, während der Support an ist, zeigt das gelbe Dreieck mit !;
      SISR stürzt wiederholt ab zeigt das rote X. Bei mehreren Meldungen zeigt der Header das Symbol der
      schwersten. · E2E: nein (echtes SISR und Steam)

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T124-1 bis T124-4 | 2026-10-20 | Claude | Branch `fix/steam-input-fuer-xbox-laesst-sisr-den-xbox-controller-uebernehmen-124`, Linux, nur Unit-Tests mit nachgebildetem SISR (`ControllerSupportServiceTests`, `SisrDevicesTests`) | ⚠️ Logik grün, App und echtes SISR nicht gestartet | Lauf am PC des Owners |
| T124-1 | 2026-10-21 | owner | PC des Owners, PR #125 (erster Stand), Castlevania: Belmont's Curse, 8BitDo Ultimate 2 und Steam Controller | ❌ Erster Start: SISR übernahm den Pad nicht, keine Pause, keine Eingaben; mit beiden Controllern pausierte SISR erst 85 s nach dem Start, das Spiel reagierte nicht | Pause nach Steams Einstellung schon vor dem Spielstart (`SteamInputSetting`) |
| T124-1, T124-7 | 2026-10-21 | Claude | Branch wie oben, Linux, Unit-Tests; Steams Einstellung aus owner's `localconfig.vdf` gelesen (`UseSteamControllerConfig 2` für 4231820) | ⚠️ Logik grün, App nicht gestartet | Lauf am PC des Owners |
| T124-1 | 2026-10-21 | owner | PC des Owners, PR #125 (Pause nach Steams Einstellung) | ✅ „Sieht grundsätzlich erstmal gut aus“; Hinweis zeigte noch das Warnsymbol | Drei Stufen Info/Warnung/Fehler (T124-9) |
| T124-9 | 2026-10-21 | Claude | Branch wie oben, Linux, Unit-Tests (Stufen der Meldungen); App gebaut, nicht gestartet | ⚠️ Stufen grün, Symbole nicht angesehen | Lauf am PC des Owners |
| T124-1 bis T124-9 | 2026-10-24 | owner | 0.1.0-rc.1 installiert, PC des Owners | ✅ | – |
