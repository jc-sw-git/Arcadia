# #127 UI Polishing

- [x] **T127-1** Detailseite mit dem Controller: Zwischen den Button-Reihen und zwischen den Buttons berührt
      der Fokusrahmen keine Nachbarn; die Kennzahlen unten sind vollständig sichtbar, auch bei einem Spiel mit
      Update (Hinweiszeile unter den Buttons). · E2E: ja
- [x] **T127-2** Detailseite: Fokus auf „Steam Controller“ bzw. „Individueller Mausmodus“ zeigt keinen
      Hinweis mehr unter den Buttons; das Kontextmenü zeigt „Wechselt zu „…““. Maus über einem deaktivierten
      Button (z. B. „Zu Favoriten“) zeigt den Grund als Tooltip. · E2E: ja
- [x] **T127-3** Detailseite aus der Bibliothek: X (Tastatur F, Klick auf den Such-Chip) öffnet die Suche;
      B/Esc dort führt zurück auf dieselbe Detailseite. · E2E: ja
- [x] **T127-4** Detailseite aus der Suche: X führt zurück in die Suche mit unverändertem Text; B/Esc führt
      ins Listing des Launchers des Spiels, auf dessen Seite und mit dem Spiel fokussiert; die Suche ist zu. · E2E: ja
- [x] **T127-5** Wenige Launcher: Der Tab-Streifen steht mittig zwischen Logo und Status, nur so breit wie
      nötig, LB/RB direkt daneben, keine Trennstriche an seinen Enden. · E2E: ja
- [x] **T127-6** Viele Launcher (mehr als in den Header passen): Der Streifen reicht mit je 40 px und einem
      Trennstrich bis an Logo und Status; der gewählte Tab steht in der Mitte, soweit die Enden es erlauben;
      wo weitere Tabs folgen, blendet der Streifen aus. Mausrad über den Tabs dreht ihn, ein angeschnittener
      Tab ist anklickbar. Mit einer Statusmeldung (z. B. „Controller wird eingerichtet …“) wird er schmaler,
      die Abstände bleiben gleich, ein Trennstrich steht zwischen Meldung und Such-Chip. · E2E: ja (bis auf
      die SISR-Meldung)
- [x] **T127-7** Eingabeart: Nach einer Controller-Taste zeigen Footer, Header-Chips ([X 🔍], [≡ ⚙]),
      LB/RB, LT/RT, „Ansicht“ (Y), „Spielen“ (A), B in der Detailseite und die Hinweise der Dialoge
      Controllertasten (rund); nach einer Taste oder einem Klick die Tastatur (eckig) und, wo es passt, die
      Maus. Bloßes Bewegen der Maus schaltet nicht um. Alle Glyphen haben die dunklere Unterkante. · E2E: ja
- [x] **T127-8** Tastatur: WASD und Pfeile bewegen, Leertaste wie A (gehalten: Details), Q/E Seite bzw.
      Spiel, Umschalt+Q/E Launcher, 1 … 0 Launcher 1–10 (auch auf der Detailseite), R Optionen, V Ansicht,
      F Suche, Enter Einstellungen (nochmals Enter schließt sie), Esc zurück. In der Suche tippen diese
      Buchstaben, Enter startet den Treffer. Gehaltenes Enter öffnet die Einstellungen nur einmal. · E2E: ja
- [x] **T127-9** In den Einstellungen (Mausmodus) erscheinen LS und L3 als Symbol im Text, in „So
      funktioniert's“ LS, RS und D-Pad als Symbol. · E2E: ja
- [x] **T127-10** Detailseite eines Spiels mit Unterlänge im Titel (g, j, p, q, y, z. B. „Burning Kingdom“):
      der Titel ist ganz zu sehen, auch zweizeilig. · E2E: ja

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T127-1, T127-3 bis T127-8 | 2026-10-21 | Claude | Branch `feat/ui-polishing-127`, Linux, Xvfb 1920×1080 `--demo`, Tastatur per xdotool (W, 4, Leertaste gehalten, F, Esc); wenige und viele Launcher mit einem vorübergehenden, nicht eingecheckten Eingriff in die Demo-Launcher; Controller-Ansicht nur im Startzustand | ✅ Screenshots im Projektordner `screenshots/ui-polishing-127/`: Streifen mittig bzw. bis an die Trennstriche mit Ausblendung, gewählter Tab zentriert; Tastenhinweise rund/eckig; Detailseite mit neuen Abständen; F öffnet die Suche über der Detailseite, Esc führt zurück. Statusmeldung, Tooltip, T127-2, T127-9 und echter Controller nicht angesehen | Rest am PC des Owners |
| T127-10 | 2026-10-21 | Claude | Branch `feat/ui-polishing-127`, Linux, Xvfb 1920×1080 `--demo`, „Burning Kingdom“ | ❌ vorher: das g abgeschnitten (Zeilenhöhe 70 bei 64 px Schrift), ✅ nachher ganz zu sehen | — |
| T127-1 bis T127-10 | 2026-10-24 | Juri | 0.1.0-rc.1 installiert, PC des Owners | ✅ | – |
