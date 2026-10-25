# #137 Launcher bearbeiten: Name und Such-Kürzel, eine Seite je Launcher

- [x] **T137-1** In Einstellungen → „Launcher“ öffnen A (Leertaste) oder ein Klick auf eine Zeile die Seite des
      Launchers: oben Logo, „Launcher / Name“ und Zustand, daneben die Nachbarn der Liste. Gruppen „Allgemein“ (Aktiv,
      Name im Tab, Such-Kürzel, Logo) und „Bibliothek“ (Ordner, falls nötig, Neu einlesen). B (Esc) führt zurück zur
      Liste, der Launcher der Seite hat den Fokus; ebenso ein Klick auf „Launcher“ über dem Namen. Esc/B dort und Q/E
      bzw. LT/RT an den Nachbarn stehen als Tasten-Chip (Controller rund, Tastatur eckig), nicht als bloßer Text. · E2E: ja
- [x] **T137-2** LT/RT (Q/E, ein Klick auf einen Nachbarn) blättern zum vorigen/nächsten Launcher in der
      Reihenfolge der Liste, am Ende wieder zum ersten. Mit nur einem Launcher gibt es keine Nachbarn. ⇧ Q / ⇧ E (LB/RB)
      wechseln in allen Einstellungen die Kategorie, Q/E die Tabs innerhalb einer, wie in der Bibliothek. Der Rahmen beim
      Überfahren eines Nachbarn ist ganz zu sehen, auch rechts. · E2E: ja
- [x] **T137-3** „Aktiv“ auf der Seite schaltet wie in der Liste; ein nicht gefundener lässt sich nicht einschalten. Der
      Kopf zeigt „Aktiv · Tab N“, „Nicht aktiv“ oder „Nicht installiert“. · E2E: ja
- [x] **T137-4** A (Leertaste, ein Klick) auf „Name im Tab“ öffnet die Bildschirmtastatur unter der Seite (ohne ↹ und die
      Randtaste), das Feld zeigt den Text mit Cursor; die Leertaste, die die Tastatur öffnet, tippt kein Leerzeichen.
      Mit dem Controller: Steuerkreuz und A tippen, X löscht, Y Leerzeichen, LB/RB ⇧/AltGr, R3 Layout, ≡ übernimmt, B
      verwirft. Mit der echten Tastatur: tippen, Pfeile/Pos1/Ende bewegen den Cursor, Enter übernimmt, Esc verwirft. Die
      Maus fokussiert solange keine andere Zeile. Der Footer nennt die Tasten der Tastatur. · E2E: ja
- [x] **T137-5** Ein übernommener Name steht sofort im Tab, in der Liste, in den Filter-Chips und Treffern der Suche, in
      Vorschau und Detailseite; nach einem Neustart noch. Leer übernommen gilt wieder der Standardname. · E2E: ja
- [x] **T137-6** „Such-Kürzel“: das Feld zeigt „/“ davor. Ein Kürzel, das ein anderer Launcher schon hat, mit „-“, mit
      Großbuchstaben/Sonderzeichen oder zu lang zeigt sofort rot den Grund; übernommen bleibt das alte und ein Toast
      sagt warum. Ein gültiges gilt sofort in der Suche (`/kürzel begriff`) und im Chip. Die Bildschirmtastatur des
      Kürzels bietet nur gültige Tasten: Sonderzeichen, Leertaste und ⇧ (bei DE) sind ausgegraut, ihr Zeichen bleibt
      sichtbar, bei DE auch AltGr (µ zählt nicht); Steuerkreuz und Stick überspringen sie, Y tippt kein Leerzeichen, LB schaltet nicht um; bei FR bleibt ⇧
      an (Ziffern), auf ⇧ sind dann nur die Ziffern wählbar. Der Name im Tab bietet alle Tasten. · E2E: ja
- [x] **T137-7** Das Infopanel erklärt die fokussierte Zeile; über „Aktiv“, „Name im Tab“ und „Logo“ steht die Vorschau
      „Launcher-Reihenfolge“ (Untertitel „Eine Vorschau der Reihenfolge, in der die Launcher aufgeführt werden“; der in
      der Bibliothek gewählte Tab ist darin nicht weiß, nur der Launcher der Seite umrandet), beim Kürzel ein Beispiel. „Neu einlesen“ ist für einen inaktiven oder nicht gefundenen Launcher
      ausgegraut (Grund unter dem Namen), sonst liest es nur diesen Launcher ein (`arcadia.log`: „Neu eingelesen für …“) und
      nennt danach Spiele und Zeit. „Neu einlesen“ auf einem leeren Tab ebenso. · E2E: ja
- [x] **T137-8** Alles aus T137-1 bis T137-7 allein mit dem Controller, mit einer Hand an der Tastatur und allein mit der
      Maus. · E2E: ja

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T137-1 bis T137-7 | 2026-10-23 | Claude | Branch `feat/launcher-bearbeiten-und-logos-137-138`, Debug, Linux in der Cloud mit Xvfb 1920×1080, `--demo`-Bibliothek, Tasten und Klicks über xdotool | ⚠️ Seite per Leertaste und Klick, Blättern mit ⇧ E, zurück mit Esc (Fokus auf dem Launcher), Name mit der Tastatur geändert (Tabs, Chips, Treffer folgen, keine Leertaste getippt), belegtes Kürzel „/ubi“ rot und abgelehnt, `/gog` sucht mit neuem Namen; kein echter Controller | Lauf am PC des Owners |
| T137-8 | 2026-10-23 | Claude | wie oben | ⚠️ Nur Tastatur und Maus; Controller nur über die Tasten, die seine Aktionen auslösen | Lauf am PC des Owners |
| T137-1 bis T137-8 | 2026-10-24 | Juri | 0.1.0-rc.1 installiert, PC des Owners | ✅ | – |
| T137-5 | 2026-10-25 | Claude | Abnahmetest `LauncherPageTests` (#190), Linux-Container | ❌ Leer übernommen wurde abgelehnt („Der Name darf nicht leer sein.“) statt wieder der Standardname | Behoben im PR zu #190 |
