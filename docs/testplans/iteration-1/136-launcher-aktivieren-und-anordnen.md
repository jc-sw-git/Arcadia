# #136 Launcher aktivieren und anordnen

- [ ] **T136-1** Ohne gespeicherte Auswahl (frische `settings.json`): Alle gefundenen Launcher sind aktiv und haben einen
      Tab wie bisher. In Einstellungen → „Launcher“ (erste Kategorie) stehen sie unter „Aktiv · erscheinen als Tabs“ in
      Tab-Reihenfolge mit ihrer Taste 1 … 0; unter „Nicht aktiv“ zuerst die installierten, dann die nicht gefundenen,
      ausgegraut mit „Nicht installiert“. · E2E: ja
- [ ] **T136-2** ◀ ▶ (A/D, mit der Maus ein Klick irgendwo auf den Schalter „Aktiv“/„Aus“) auf einem aktiven Launcher: „Aus“, er steht oben unter „Nicht
      aktiv“, sein Tab verschwindet, seine Spiele fehlen in der Suche. Wieder an: Er steht am Ende der aktiven, bekommt den
      letzten Tab und wird eingelesen. Nach einem Neustart gilt die Auswahl samt Reihenfolge noch. · E2E: ja
- [ ] **T136-3** Ein nicht gefundener Launcher lässt sich nicht einschalten (◀ ▶ tun nichts, der Fokus hält trotzdem an
      ihm); ein aktiver, der nicht mehr gefunden wird (deinstalliert), lässt sich ausschalten. · E2E: nein (Deinstallation)
- [ ] **T136-4** Y (V) hebt die Zeile an (Rahmen, Footer „▲▼ Verschieben, Y Ablegen, B Abbrechen“), ▲ ▼ (W/S) schieben
      sie; Y legt sie ab und die Tabs folgen sofort. B (Esc) bricht ab und alles steht wie vorher. Unter „Nicht aktiv“
      abgelegt ist der Launcher aus. Mit der Maus: Zeile ziehen und loslassen, ebenso. · E2E: ja
- [ ] **T136-5** Rechts stehen zwei Boxen: „Tab-Leiste“ zeigt die Tabs in der aktuellen Reihenfolge (der fokussierte
      Launcher markiert, ein inaktiver nicht darin), darunter „Launcher-Details“: Logo, Name und Zustand, Warnungen, dann
      Spiele/installiert, zuletzt eingelesen, Fundort, Installationen, laufende Spiele. RS scrollt die Details, ein Fade
      zeigt mehr. · E2E: ja
- [ ] **T136-6** X (F) liest alle aktiven Launcher neu ein; ein Toast „Alle Launcher sind neu eingelesen.“ erscheint über
      der Seite. · E2E: ja
- [ ] **T136-7** Ein aktiver Launcher ohne Spiele behält seinen Tab: „Keine Spiele in …“ mit „Launcher-Einstellungen“ und
      „Neu einlesen“; einer, der nicht mehr gefunden wird: „… nicht gefunden“ mit „Erneut suchen“. ◀ ▶ und A, die Maus
      und Leertaste bedienen beide Knöpfe; „Launcher-Einstellungen“ öffnet die Kategorie mit diesem Launcher fokussiert.
      Alle Launcher aus: „Kein Launcher aktiv“ mit leeren Tabs als Bild, „Launcher-Einstellungen“ und „Erneut suchen“,
      ohne Hinweis auf `--demo`. · E2E: ja
- [ ] **T136-8** Alles aus T136-2 bis T136-7 allein mit dem Controller, mit einer Hand an der Tastatur und allein mit der
      Maus. · E2E: ja
- [ ] **T136-9** Release-Build am PC des Owners, Bibliothek mit rund 1.000 Spielen: `arcadia.log` nennt „Bibliothek:
      N Spiele aus M Launchern in X ms eingelesen, in Y ms angezeigt“; Start bis zur bedienbaren Bibliothek < 2 s.
      · E2E: nein (Messung)
- [ ] **T136-10** In „Controller“ steht bei eingeschalteter „Steam Controller Kompatibilität“ eine Lampe hinter dem Namen:
      grün, solange SISR läuft, grau, solange es startet, pausiert, keinen Controller findet oder ein Problem hat;
      ausgeschaltet keine Lampe.
      · E2E: nein (SISR)
- [ ] **T136-11** Die Pfeile in den Werte-Chips (◀ ▶) stehen mittig in ihrem Kreis. Ein angehobener Launcher zeigt seine
      Umrandung ganz, auch rechts. · E2E: nein (Optik)
- [ ] **T136-12** Eine Meldung in „Controller“ (z. B. „SISR pausiert wegen eines anderen Controllers“) hat unter dem
      Namen nur eine kurze Zeile („Es wurden weitere Controller erkannt, die Steam-Eingabe in Steam nutzen.“), der ganze
      Text steht im Infopanel. Ihre Aktion („Erneut versuchen“) ist eine Pille so breit wie die Werte-Chips, die sich
      blau füllt, solange die Zeile den Fokus hat. · E2E: nein (SISR)
- [ ] **T136-13** In jeder Kategorie steht jede Gruppe in einer eigenen Box. In „Controller“ stehen die abhängigen
      Einstellungen eingerückt unter ihrer Haupteinstellung: unter „Mausmodus“ alle übrigen Zeilen der Gruppe, unter
      „Beenden des Mausmodus in Spielen erzwingen“ noch einmal „Standardeinstellung bei „Individuell““, unter „Mit ⧉
      zurück zu Arcadia“ die „Haltedauer“. Fokus, Hover und Klick gehen wie vorher. · E2E: nein (Optik)

A auf einem Launcher öffnet seine Seite (#137).

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T136-1, T136-2, T136-4 bis T136-7 | 2026-10-22 | Claude | Branch `feat/launcher-aktivieren-und-anordnen-136`, Debug, Linux in der Cloud mit Xvfb 1920×1080, `--demo`-Bibliothek (Steam und Epic dort „nicht gefunden“), Tasten und Klicks über xdotool | ⚠️ Liste, Schalter, Anheben/Verschieben/Ablegen samt Abbruch, Ablegen unter „Nicht aktiv“ (in `settings.json` gespeichert, Tab weg), Ziehen mit der Maus, beide leeren Tabs und ihre Knöpfe, Infopanel, Toast über der Seite wie erwartet; kein echter Controller, keine echten Launcher | Lauf am PC des Owners |
| T136-3 | 2026-10-22 | Claude | wie oben | ⚠️ Nur mit Demo-Launchern ohne Fundort; echte Deinstallation nicht | Lauf am PC des Owners |
| T136-2, T136-5, T136-7, T136-11 (Juris Feedback) | 2026-10-22 | Claude | wie oben | ⚠️ Klick auf den Schalter schaltet um (erst auf die ganze Zeile, von Juri verworfen), zwei Boxen, „Kein Launcher aktiv“ samt Knöpfen (Leertaste öffnet die Einstellungen, Klick schaltet GOG ein, Tab erscheint), Pfeile mittig | Lauf am PC des Owners |
| T136-12 | 2026-10-22 | Claude | wie oben; Aktionszeilen in „Über Arcadia“ lokal eingeblendet | ⚠️ Pille unfokussiert, fokussiert und als Hauptaktion gesehen; die SISR-Meldung selbst nur per Test (`ShortText`) | Lauf am PC des Owners |
| T136-13 | 2026-10-22 | Claude | wie oben | ⚠️ Boxen in „Launcher“ und „Controller“, Einrückung in zwei Stufen gesehen | Lauf am PC des Owners |
| T136-10 | 2026-10-22 | Claude | wie oben | ⚠️ Unter Linux ist SISR nicht verfügbar, Lampe nur gebaut, nicht gesehen | Lauf am PC des Owners |
| T136-9 | 2026-10-22 | Claude | wie oben, Demo-Zahlen lokal auf 1.045 Spiele aus 4 Launchern erhöht | ⚠️ 426 ms eingelesen (außerhalb des UI-Threads), 37 ms angezeigt; kein Release-Build, nicht der PC des Owners | Messung am PC des Owners |
