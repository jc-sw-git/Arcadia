# #85 Steam Controller Support erst einschaltbar, wenn alles bereit ist

Mit echtem Steam Controller. Für die Fälle ohne SISR vorher in Arcadia deinstallieren oder
`%LOCALAPPDATA%\SISR` umbenennen.

- [x] **T85-1** Ohne SISR (oder ohne usbip-Treiber, ohne `.cef-enable-remote-debugging` im
      Steam-Ordner oder ohne „SISR Marker“ in der Steam-Bibliothek) zeigt die Einstellung „SISR installieren“ statt des Schalters. · E2E: ja (Fake-Plattform)
- [x] **T85-2** Steam läuft: A auf „SISR installieren“ öffnet über den Einstellungen das Modal „Steam beenden?“;
      nur dessen Schaltflächen haben den Fokus. „Abbrechen“ oder B: das Modal schließt, nichts wird
      installiert, Steam läuft weiter; erneutes A fragt wieder. · E2E: ja (Fake-Plattform)
- [x] **T85-3** Steam läuft, „Steam beenden und installieren“: Steam schließt, SISR und Treiber werden
      installiert (Windows-Abfrage), Steam startet wieder im Hintergrund, die Option ist danach an und
      der Steam Controller steuert Arcadia als Xbox 360 Controller. Nur mit dem Controller. · E2E: nein (echtes Steam, UAC)
- [x] **T85-4** Steam läuft nicht: „SISR installieren“ installiert ohne Rückfrage; Steam bleibt aus,
      die Meldung „Steam läuft nicht“ mit „Steam starten“ erscheint. · E2E: nein (UAC)
- [x] **T85-5** Ein Steam-Spiel läuft: „SISR installieren“ installiert nichts und öffnet das Modal
      „Steam-Spiel läuft“ mit dem Namen des Spiels. Spiel beenden, während das Modal offen ist: es wechselt
      von selbst zu „Steam beenden?“. Schließen (B) und die Einstellungen neu öffnen: kein alter Hinweis
      bleibt stehen. · E2E: nein (echtes Steam-Spiel)
- [x] **T85-6** Ohne Netzwerk „SISR installieren“: nach spätestens etwa 20 Sekunden eine Meldung, die
      Option bleibt aus, Steam läuft wieder, Arcadia bleibt bedienbar. · E2E: nein (SISR-Installation)
- [x] **T85-7** Windows-Abfrage des Treibers ablehnen: Meldung „… abgelehnt“, die Option bleibt aus,
      Steam läuft wieder. · E2E: nein (UAC)
- [x] **T85-8** Bei eingeschalteter Option den usbip-Treiber deinstallieren: Die Option bleibt an und
      zeigt das Problem mit „Installieren“. · E2E: nein (Treiber)
- [x] **T85-9** Unter Linux (bzw. ohne Steam unter Windows) ist die Option ausgegraut und sagt
      „Momentan nur unter Windows verfügbar.“ bzw. „Benötigt Steam.“; A tut nichts. · E2E: ja
- [x] **T85-10** Option aus, Steam beendet: Einschalten startet zuerst Steam, mit Spinner und
      „Steam wird gestartet …“; erst wenn Steam läuft, startet SISR. Kein Vollbildfenster von SISR
      („Steam läuft nicht“) erscheint. Bis dahin reagieren die Einstellungen nur auf B. · E2E: nein (echtes Steam)
- [x] **T85-11** Startet Steam beim Einschalten nicht innerhalb von 60 Sekunden (z. B. Steam-Ordner
      umbenannt): Modal „Steam startet nicht“; „Abbrechen“ lässt die Option aus, „Erneut versuchen“
      versucht es noch einmal. Ebenso, wenn Steam läuft, aber bei „Nutzerdaten werden geladen“ hängen
      bleibt: SISR startet dann nicht. · E2E: ja (Fake-Plattform)
- [x] **T85-12** Option an, dann Steam beenden: SISR endet innerhalb von etwa 2 Sekunden mit, Steam
      startet nicht wieder; die Einstellungen zeigen „Steam läuft nicht“ mit „Steam starten“. Danach
      ausschalten: Steam bleibt aus. Steam selbst starten: SISR startet von allein wieder. · E2E: nein (echtes Steam)
- [x] **T85-13** Bei keinem Ein- oder Ausschalten (auch mit beendetem Steam) und bei der Einrichtung
      erscheint ein Fenster von SISR (Ersteinrichtung, „Steam läuft nicht“, Marker hinzufügen). Nach der
      Einrichtung gibt es genau einen „SISR Marker“ in der Steam-Bibliothek. · E2E: nein (echtes Steam)
- [x] **T85-14** Solange Steam, SISR oder VIIPER starten oder beenden (auch von selbst, z. B. nach dem
      Beenden von Steam), zeigt die Einstellung einen Spinner und reagiert nur auf B; der Schalter lässt sich
      erst danach umlegen. Hängt Steam beim Anmelden, bleibt der Schalter bedienbar. · E2E: ja (Fake-Plattform)
- [x] **T85-15** Mit einem Steam-Konto: „SISR installieren“ legt den „SISR Marker“ ohne Frage an, die Option
      geht an und SISR startet; kein wiederholtes „Anlegen“. Ohne je angemeldetes Konto: die Meldung „kein
      Steam-Konto gefunden“ kommt sofort, Steam wird nicht beendet, nichts wird installiert, die Option bleibt aus.
      Mehrere Konten: T85-19. · E2E: ja (Fake-Plattform)
- [x] **T85-16** Hängt Steam länger als 30 Sekunden bei „Nutzerdaten werden geladen“: die Einstellungen zeigen
      „Steam meldet sich noch an“ mit „Steam neu starten“; ein laufendes SISR wird dabei nicht beendet. · E2E: ja (Fake-Plattform)
- [x] **T85-17** Option an, SISR läuft, Standby und wieder aufwachen bzw. kurz das Netzwerk trennen, während ein
      Nicht-Steam-Spiel läuft: der Controller bleibt als Xbox 360 Controller erhalten. · E2E: nein (Standby, echtes Steam)
- [x] **T85-18** Steam im Offline-Modus starten, Option an: SISR startet, sobald Steam offline bereit ist (falls
      nicht: Befund notieren, Steam schreibt dann vermutlich kein `Success` ins Login-Log). · E2E: nein (echtes Steam)
- [ ] **T85-19** PC mit mehreren Steam-Konten, „SISR installieren“: vor „Steam beenden?“ fragt „Steam-Eintrag für
      SISR anlegen?“ mit allen Konten (zuletzt angemeldetes zuerst und vorausgewählt, Konten mit Eintrag ausgegraut
      und „bereits eingerichtet“), per D-Pad und A wählbar; „Weiter“ nur mit mindestens einem Haken (eingerichtete
      Konten zählen mit: wählt man dann nichts Neues, wird kein Eintrag geschrieben). Danach steht „SISR Marker“
      genau in den gewählten Konten in Steam. Ist das angemeldete Konto nicht dabei, startet SISR nicht und die
      Einstellungen zeigen „Steam-Eintrag für SISR fehlt“ mit „Anlegen“. „Abbrechen“ oder B bricht die
      Installation ab, nichts wird installiert. Nur Konten aus Steams Anmeldeliste erscheinen. Mit nur einem Konto
      kommt die Frage nicht. · E2E: ja
- [x] **T85-20** Option einschalten, während Steam beim Anmelden hängt: nach einer Minute „Steam meldet sich nicht
      an“ mit „Steam neu starten“ (statt „Steam startet nicht“); der Neustart schaltet die Option danach ein. Die
      Einstellungen während der Minute schließen und wieder öffnen: die Meldung erscheint, sobald der Start
      scheitert, nicht erst beim nächsten Öffnen. · E2E: nein (echtes Steam, hängende Anmeldung)

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T85-10 bis T85-12 | 2026-10-16 | Juri | Branch `feat/steam-controller-setup-and-naming-85-92` (Stand d2375c7), Windows, echtes Steam | ❌ Einschalten ohne Steam zeigte SISRs Vollbildfenster „Steam läuft nicht“; nach Steam beenden und Ausschalten startete Steam | SISR nur mit laufendem Steam, Steam beim Einschalten starten |
| T85-12, T85-13 | 2026-10-16 | Juri | Branch `feat/steam-controller-setup-and-naming-85-92` (Stand 32c4ef1), Windows, echtes Steam | ❌ Nach dem Beenden startete Steam wieder; beim Einschalten ohne Steam fragte SISR jedes Mal, einen Marker hinzuzufügen | SISR ohne Steam hart beenden, auf Anmeldung warten, Marker und Ersteinrichtung selbst |
| T85-12 | 2026-10-16 | Juri | Branch `feat/steam-controller-setup-and-naming-85-92` (Stand 314f649), Windows, echtes Steam | ❌ Manchmal startete Steam nach dem Beenden trotzdem wieder; Log: SISR wurde regulär beendet, während steam.exe noch herunterfuhr | Ohne bereites Steam immer hart beenden |
| T85-12, T85-13 | 2026-10-16 | Claude | Branch `feat/steam-controller-setup-and-naming-85-92`, Linux, Unit-Tests mit Fake-Plattform und Fake-SISR | ✅ Ohne Steam wird SISR beendet statt beendet-mit-Aufräumen, Neustart mit Steam zählt nicht als Absturz, ohne Marker kein SISR-Start, die Einrichtung legt den Marker bei geschlossenem Steam an | Echter Ablauf am PC des Owners |
| T85-10 bis T85-12 | 2026-10-16 | Claude | Branch `feat/steam-controller-setup-and-naming-85-92`, Linux, Unit-Tests mit Fake-Plattform (`ControllerSupportServiceTests`); Modal „Steam startet nicht“ per Xvfb-Screenshot mit lokalem Eingriff | ✅ Steam vor SISR, Timeout schaltet aus, ohne Steam kein SISR-Start und kein Absturzzähler, Ausschalten ohne Steam sendet nichts an Steam | Echter Ablauf am PC des Owners |
| T85-11 | 2026-10-16 | Juri | Branch `feat/steam-controller-setup-and-naming-85-92` (Stand e32a90b), Windows, echtes Steam | ❌ Steam hing bei „Nutzerdaten werden geladen“ (auch ohne Arcadia; laut Steam-Logs antwortete der Steam Controller nicht, erst ein Windows-Neustart half), trotzdem startete SISR und fand keinen Controller | Bereit erst nach abgeschlossener Anmeldung (`steamui_login.txt`) |
| T85-2, T85-5 | 2026-10-16 | Claude | Branch `feat/steam-controller-setup-and-naming-85-92`, Linux, Xvfb 1920×1080, `--demo`, Modal per lokalem Eingriff erzwungen (unter Linux ist die Option ausgegraut) | ✅ Beide Modals erscheinen über den abgedunkelten Einstellungen, Fokus nur auf ihren Schaltflächen; per Unit-Test geprüft sind nur die frische Abfrage des Spiels und `ClearError`, der Wechsel des Modals beim Spielende nicht | Echter Ablauf mit Steam-Spiel am PC des Owners |
| T85-1, T85-3 bis T85-7 | 2026-10-16 | Claude | Branch `feat/steam-controller-setup-and-naming-85-92`, Linux, Unit-Tests mit Fake-Plattform und Fake-SISR (`ControllerSupportServiceTests`), nicht in der App | ✅ Reihenfolge Steam beenden → CEF → SISR → Treiber → Steam starten → an; Steam-Spiel, Timeout, abgelehnter Treiber und nicht beendbares Steam melden sich und lassen die Option aus | Ansicht und echter Ablauf am PC des Owners |
| T85-14 bis T85-18 | 2026-10-16 | Claude | Branch `feat/steam-controller-setup-and-naming-85-92` (zweites Review), Linux, Unit-Tests mit Fake-Plattform und Fake-SISR (`ControllerSupportServiceTests`, `SteamLoginLogTests`, `SteamInstallationTests`, `UsbipPortsTests`), nicht in der App | ✅ Hängende Anmeldung stoppt SISR nicht und warnt nach 30 s mit „Steam neu starten“; Neustart über Arcadia; Marker fürs zuletzt angemeldete Konto; VIIPER-Aufräumen nach jedem Stopp; gleichzeitige Einstellungsänderungen gehen nicht verloren | Echter Ablauf und `usbip port`-Format am PC des Owners |
| T85-19 | 2026-10-16 | Claude | Branch `feat/steam-controller-setup-and-naming-85-92`, Linux, Xvfb 1920×1080 `--demo`, Abfrage mit drei erfundenen Konten per lokalem Eingriff erzwungen; Unit-Tests (`ControllerSupportServiceTests`, `SteamInstallationTests`) | ✅ Abfrage, Vorauswahl, „bereits eingerichtet“, D-Pad und A wie beschrieben; Marker nur für gewählte Konten (nur Unit-Test, Schreiben in echte `shortcuts.vdf` mehrerer Konten ungetestet) | Echter Ablauf mit mehreren Konten am PC des Owners |
| T85-15, T85-19, T85-20 | 2026-10-16 | Claude | Branch `feat/steam-controller-setup-and-naming-85-92` (Nach-Review), Linux, Unit-Tests (`ControllerSupportServiceTests`, `SteamInstallationTests`), nicht in der App | ✅ Kein Konto: Abbruch vor Steam-Beenden und Treiber; Marker nur für ein anderes Konto: SISR startet nicht, „Anlegen“ erscheint; alle Konten eingerichtet: nichts geschrieben; Kontenliste nur aus `loginusers.vdf`. T85-20 nur gebaut, nicht ausgeführt | T85-20 und echte Konten am PC des Owners |
| T85-14 bis T85-18 (VIIPER-Aufräumen) | 2026-10-17 | Juri / Claude | Windows, `usbip port` am PC des Owners bei laufendem SISR (Screenshot); Ausgabe als Unit-Test (`UsbipPortsTests`) übernommen | ✅ Format wie angenommen, der Parser findet Port 01 von `usbip://localhost:3241` | Aufräumen nach einem echten SISR-Abbruch am PC beobachten |
| T85-1 bis T85-18, T85-20 | 2026-10-24 | Juri | 0.1.0-rc.1 installiert, PC des Owners | ✅ | – |
| T85-19 | 2026-10-24 | Juri | 0.1.0-rc.1 installiert, PC des Owners | ⏭️ Laut Juri am PC des Owners nicht ohne weiteres testbar | Abnahmetest in der Abnahme-Iteration |
