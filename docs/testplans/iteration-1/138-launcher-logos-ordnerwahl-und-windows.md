# #138 Launcher-Logos, Ordnerwahl und Windows-Dialoge mit dem Controller

- [x] **T138-1** Steam und Epic zeigen ihr Logo statt des Anfangsbuchstabens: im Tab (hell auf dunkel, dunkel auf dem
      gewählten Tab), in der Liste, in Vorschau und Detailseite, auf den Treffern der Suche (auf dunklem Grund) und in
      den Filter-Chips. Launcher ohne Logo behalten Buchstabe und Kachel. · E2E: ja
- [x] **T138-2** „Logo“ ist ein Knopf „Hochladen …“ (A, Leertaste, Klick), kein ◀ ▶-Schalter; er öffnet die Dateiwahl von
      Windows. Ein PNG, SVG, ICO oder WebP bis 2 MB ersetzt das Logo überall sofort, ins 24 × 24-Feld eingepasst
      (Seitenverhältnis bleibt); nach einem Neustart noch, auch wenn die Originaldatei gelöscht ist. Eine kaputte oder zu
      große Datei: Toast „Logo nicht übernommen: …“, das alte bleibt. Nur mit eigenem Logo steht eingerückt darunter
      „Standardlogo“ mit „Wiederherstellen“; das stellt das Standardlogo wieder her, die Zeile verschwindet und der Fokus
      steht wieder auf „Logo“. · E2E: nein (Windows-Dialog)
- [x] **T138-3** Findet Arcadia einen Launcher nicht, steht auf seiner Seite „Ordner“ mit Warnung. „Ordner wählen …“
      öffnet die Ordnerwahl; ein Ordner ohne den Launcher: Toast „Dort liegt kein Steam: Arcadia sucht …“, nichts
      ändert sich. Der richtige Ordner (für Steam mit `steam.exe` und `steamapps`, oder `steamapps` selbst): der Launcher
      wird gefunden, eingeschaltet und eingelesen; nach einem Neustart noch. Ein Launcher, den Arcadia selbst findet,
      zeigt die Zeile nicht. · E2E: nein (Windows-Dialog)
- [x] **T138-4** Einstellungen → „Controller“ → Mausmodus: „Bei System-Dialogfenstern einschalten“ steht unter
      „Mausmodus“, ausgegraut bei „Aus“, gilt für „Von Hand“ und „Automatisch“. An und mit dem Controller einen Dialog
      geöffnet (Logo, Ordner): der Mausmodus ist an, solange der Dialog offen ist, und danach wieder aus (war er vorher
      an, bleibt er an). Aus, oder mit Maus/Tastatur geöffnet: der Mausmodus bleibt, wie er ist. · E2E: nein
      (Windows-Dialog)
- [x] **T138-5** Logos in Tabs, Liste und Suche sehen mit eigenen Bildern (breit, hoch, transparent, SVG) sauber aus,
      nichts ragt aus seinem Feld; ein inaktiver Launcher zeigt sein Logo blasser. · E2E: nein (Optik)

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T138-1, T138-5 | 2026-10-23 | Claude | Branch `feat/launcher-bearbeiten-und-logos-137-138`, Debug, Linux in der Cloud mit Xvfb 1920×1080, `--demo`-Bibliothek | ⚠️ Steam- und Epic-Logo in der Liste, Logos in Tabs, Chips, Treffern und Detailseite gesehen; breites PNG eingepasst | Lauf am PC des Owners |
| T138-2 | 2026-10-23 | Claude | wie oben, GTK-Dateiwahl statt der von Windows | ⚠️ PNG und SVG übernommen, kaputte Datei abgelehnt (Toast), Logo nach Neustart noch da, ◀ stellt den Standard her und löscht die Kopie | Lauf am PC des Owners |
| T138-3 | 2026-10-23 | Claude | wie oben, gefälschte Steam-Ordner (`steam.exe` + `steamapps`) | ⚠️ Ordner ohne Steam abgelehnt, mit Steam angenommen: Steam an, gefunden, eingelesen (0 Spiele) | Lauf am PC des Owners |
| T138-2, T137-7 (Juris Feedback) | 2026-10-23 | Claude | wie oben | ⚠️ „Logo“ als Knopf „Hochladen …“, eingerückte Zeile „Standardlogo“ erscheint mit eigenem Logo und verschwindet nach „Wiederherstellen“ (Fokus zurück auf „Logo“); „Jetzt einlesen“ liest nur GOG (5 ms, Toast „31 Spiele in GOG Galaxy gefunden.“), dass die anderen nicht gelesen werden nur per Test | Lauf am PC des Owners |
| T137-1, T137-2 (Juris Feedback) | 2026-10-23 | Claude | wie oben | ⚠️ Klick auf „Launcher“ über dem Namen führt zur Liste (Fokus auf dem Launcher), E blättert zum nächsten Launcher, ⇧ E/⇧ Q wechseln die Kategorie, Rahmen des rechten Nachbarn beim Überfahren ganz zu sehen | Lauf am PC des Owners |
| T137-6 (Juris Feedback) | 2026-10-23 | Claude | wie oben | ⚠️ Tastatur des Kürzels (DE): Sonderzeichen, Leertaste und ⇧ ausgegraut, Buchstaben, Umlaute, ß und Ziffern wählbar; AltGr aus, seit µ nicht mehr zählt (Juri: ein Sonderzeichen); das Überspringen mit dem Steuerkreuz nicht gesehen (kein Controller), es nutzt die schon getestete Sperre ausgegrauter Tasten | Lauf am PC des Owners |
| T138-4 | 2026-10-23 | Claude | wie oben | ⚠️ Zeile, Ausgrauen bei „Aus“ gesehen; das Einschalten beim Dialog nur per Test (`MouseMode`), unter Linux kein Mausmodus | Lauf am PC des Owners |
| T138-1 bis T138-5 | 2026-10-24 | Juri | 0.1.0-rc.1 installiert, PC des Owners | ✅ | – |
