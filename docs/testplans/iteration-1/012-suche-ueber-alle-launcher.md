# #12 Suche über alle Launcher

- [x] **T12-1** X öffnet die Suche mit Bildschirmtastatur: Kopf „ARCADIA Suche“ mit „Schließen“, Suchfeld
      mit Platzhalter „Spiele suchen …“ (ohne Kürzel-Hinweis), Zeile „Suche in allen Launchern“, in einer Zeile
      die Chips „Alle Launcher“ und je Launcher mit Kürzel sowie rechts „L3 Sortiert nach Relevanz“, mittig der
      leere Zustand „Tippe einen Suchbegriff ein“ mit Grafik in einem runden Schein ohne Kante, die Tastatur (Layout DE, Fokus auf „q“) und unten
      die Legende mit den Controller-Tasten. F oder die Lupe öffnen sie ohne Tastatur, die Legende zeigt dann Maus und Tastatur. · E2E: ja
- [x] **T12-2** Nur mit dem Controller: D-Pad und linker Stick bewegen den Fokus über die Tasten (hoch/runter
      zur nächstgelegenen Taste, an den Rändern bleibt er stehen), A tippt das Zeichen; Y tippt ein
      Leerzeichen. X löscht ein Zeichen, gehalten (oder A auf „Löschen“ gehalten) löscht es nach kurzer Pause
      eins nach dem anderen. · E2E: ja
- [x] **T12-3** Treffer kommen aus allen eingeschalteten Launchern, sortiert nach Relevanz: Titelanfang vor
      Wortanfang vor Wortmitte, Akzente und Groß-/Kleinschreibung egal. Kopf „N Treffer in M Launchern“
      bzw. der leere Zustand „Keine Treffer“ mit Grafik; Cover mit Launcher-Logo oben links und Launchername
      darunter. ™, ® und © zählen nicht: „™“ allein findet nichts (kein Batman, kein Hitman), „F1 24“ findet
      „F1® 24“. · E2E: ja
- [x] **T12-4** Präfix (nur mit echter Tastatur): `/gog holl` sucht nur in GOG, `/gog-xbox holl` in beiden,
      die Chips folgen; Zeile „Per Kürzel gefiltert: …“. `/xyz holl` zeigt in Orange „· Unbekanntes
      Kürzel: /xyz“. · E2E: ja
- [x] **T12-5** Chips (Maus) und L3-Filter schalten Launcher aus oder ein (der letzte bleibt an), „Alle
      Launcher“ schaltet alle ein. Ohne Präfix bleibt der Text unverändert und die Zeile zeigt „Suche in: …“;
      ein getipptes Präfix wird neu geschrieben, „Alle Launcher“ entfernt es. · E2E: ja
- [x] **T12-6** R3 (oder Klick auf die Globus-Taste) öffnet das Menü „TASTATURLAYOUT“ mit DE, US, UK, FR und
      ES über den Treffern (nichts überlappt es), das aktive mit Haken; D-Pad und A wählen, die Tasten wechseln
      sofort. B oder R3 schließt erst das Menü. Das Layout gilt nach einem Neustart weiter. · E2E: ja
- [x] **T12-7** Auf dem fokussierten Treffer: ≡ startet das Spiel (beim Loslassen, nicht installiert:
      Installation), ≡ 1 s halten füllt einen Ring ums Cover und öffnet die Detailseite, ⧉ (Rechtsklick,
      Umschalt+F10) öffnet das Kontextmenü neben dem Cover. Mit der Maus wählt ein Klick den Treffer, ein
      Doppelklick startet ihn, die linke Taste 1 s gehalten füllt den Ring und öffnet die Detailseite. Die Legende
      nennt das. · E2E: ja
- [x] **T12-8** Detailseite aus der Suche: Pfadleiste „B Suche / <Titel>“, Position „2 von 3“, keine Tabs, kein
      Y und keine LB/RB im Footer. LT/RT rotieren durch die Treffer in ihrer Reihenfolge; LB/RB und Y tun
      nichts. B führt zurück zur Suche mit unverändertem Text, der Fokus steht auf dem zuletzt gezeigten
      Spiel. · E2E: ja
- [x] **T12-9** B bei ausgefahrener Tastatur fährt sie ein, ein zweites B führt in die Bibliothek zurück
      (zweimal schnell B: direkt zurück), Tab, Seite und Fokus wie vorher; B löscht keine Zeichen. · E2E: ja
- [x] **T12-10** Echte Tastatur: Tippen schreibt ins Suchfeld (auch Umlaute, Großbuchstaben und Leerzeichen),
      Rücktaste löscht (gehalten wiederholt), die Pfeiltasten bewegen den Fokus durch die Treffer, Enter startet,
      Enter halten öffnet die Details, Esc schließt. In die Bildschirmtastatur kommt man mit ihr nicht. · E2E: ja
- [x] **T12-11** Wird die Bibliothek bei offener Suche neu eingelesen (z. B. Spiel im Launcher installiert),
      bleiben Text und Fokus, die Treffer folgen. · E2E: nein
- [x] **T12-12** Bildschirmtastatur ein- und ausfahren: Sie rollt sichtbar (kurz) von unten herein und wieder
      hinaus, die Lasche mittig an ihrer Oberkante fährt mit (X bzw. B, Tastatursymbol und Pfeil auf einer Linie). Ein Klick auf die Lasche fährt sie aus bzw. ein;
      jede Taste der echten Tastatur fährt sie ein; B fährt sie ein, X wieder aus.
      Eingefahren stehen die Treffer im Raster, D-Pad bewegt den Fokus durch sie, A startet, A gehalten öffnet
      die Details; die Legende passt sich an (ausgefahren bzw. eingefahren, Controller bzw. Maus und Tastatur). · E2E: ja
- [x] **T12-13** Groß/Klein: LB kurz tippen schaltet Großbuchstaben ein (Taste „⇧ Groß“ hervorgehoben, Tasten
      zeigen Großbuchstaben) und bleibt so, nochmal tippen schaltet aus. LB gehalten gilt nur, solange es
      gehalten wird: Buchstaben dabei groß, nach dem Loslassen wieder klein. · E2E: ja
- [ ] ~~**T12-14** RB kurz tippen wechselt auf die Sonderzeichen-Seite (u. a. ! ? & + ( ) [ ] @ % : ; ' " € ½, ohne
      ™, ® und …; Taste zeigt „ABC“) und bleibt dort, nochmal tippen zurück. RB gehalten zeigt sie nur, solange
      es gehalten wird. Die Seite ist für alle Layouts gleich.~~
      Ersetzt durch T129-3 (RB schaltet auf AltGr, die Sonderzeichen-Seite entfällt). · E2E: ja
- [x] **T12-15** L3 (oder Klick auf „Sortiert nach …“, oder die Taste „Filter“) öffnet „Filter und Sortierung“:
      Launcher als Mehrfachauswahl, Sortierung nach Relevanz (Standard), Name, Installationsdatum und
      Zuletzt gespielt, je auf- und absteigend. Jede Wahl wirkt sofort, der Kopf zeigt „Sortiert nach …“;
      B oder L3 schließt. Eine neue Suche beginnt wieder mit Relevanz. · E2E: ja
- [x] **T12-16** Mehr Treffer als sichtbar, Tastatur ausgefahren: links und rechts stehen die Nachbarn angeschnitten
      und laufen weich aus (ohne harte Kante und ohne Streifen am Rand, auch bei krummen Fenstergrößen), daneben Schaltflächen „LT“/„RT“ (mit der Maus nur Pfeile), über den Treffern „RS Treffer
      wählen“, der Kopf zeigt „8–14 von 105“. Der rechte Stick bewegt den Fokus einzeln (die Reihe gleitet mit),
      LT/RT gleiten eine ganze Reihe weiter und vom Ende wieder zum Anfang (und umgekehrt). Eingefahren scrollt
      das Raster senkrecht mit verblassten Reihen oben und unten, LT/RT und Bild ↑/↓ blättern um zwei Reihen und
      bleiben am Anfang und Ende stehen (kein Sprung ans andere Ende, wie Mausrad und Bibliothek); Mausrad und
      Klick auf einen angeschnittenen Treffer scrollen ebenso. Eine Scrollleiste zeigt, wo man steht: unter der
      Reihe waagrecht, neben dem Raster senkrecht; Ziehen oder ein Klick auf die Leiste scrollt. · E2E: ja
- [x] **T12-17** Viele Launcher (mit der Maus sichtbar): Passen die Chips nicht neben „Sortiert nach …“, fallen
      bei allen zugleich zuerst die Kürzel weg, dann werden die Namen gekürzt („Ubis…“), zuletzt bleibt nur das
      Logo; wird es eng vor der Sortierung, erscheint dort ein Trennstrich. · E2E: nein
- [x] **T12-18** Wechsel zwischen Controller und Maus: Bei ausgefahrener Tastatur und Controller-Legende wirkt
      schon der erste Klick auf eine Taste oder die Lasche; die Legende wechselt dabei auf Maus und Tastatur,
      die Controller-Zeichen auf den Tasten und neben „Schließen“ verschwinden, die Tastatur bleibt stehen.
      Ein Druck auf dem Controller holt die Controller-Legende zurück. · E2E: ja
- [x] **T12-19** Fenster mit anderem Seitenverhältnis als 16:9 (breit im Fenster- oder Vollbild-Fenstermodus, und
      hoch): Legende und Bildschirmtastatur der Suche reichen bis an die Fensterränder, ebenso die Linie über dem
      Footer der Bibliothek und die Abdunklung hinter Dialogen; neben der Suche schimmert nichts von der Bibliothek
      durch. · E2E: nein

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T12-1, T12-3, T12-4, T12-6, T12-10 | 2026-10-18 | Claude | Branch `feat/search-across-launchers-12`, Linux, Xvfb 1920×1080 `--demo`, Fenster 1600×900, Tastatur per xdotool, Klick auf Globus | ✅ „holl“ 3 Treffer in 3 Launchern, „e“ 112 mit „+105 weitere“, `/gog-xyz e` nur GOG plus Warnung, UK gewählt und nach Neustart noch aktiv, „ezzzz“ „Keine Treffer“ | Controller (R3, LT, ≡) am PC des Owners |
| T12-7, T12-8 | 2026-10-18 | Claude | wie oben, Enter halten, Bild ↓, Esc, Umschalt+F10 | ✅ Ring füllt sich, Detailseite „Suche / Hollow Kingdom II“ „2 von 3“ ohne Tabs und Y, Bild ↓ zum nächsten Treffer, Esc zurück mit Fokus darauf, Kontextmenü neben dem Cover | Controller am PC des Owners |
| T12-2, T12-3 | 2026-10-18 | Juri | PC des Owners, PR #104 (Rough-Test) | ❌ Filter per „/“ zu prominent, keine Sortierung, Platzhalter irreführend, kein Groß/Klein, keine Sonderzeichen, echte Tastatur kommt in die Bildschirmtastatur, Löschen ohne Wiederholung, „Suchen“-Taste sinnlos | Umbau nach Rückfragen im Thread (T12-12 bis T12-16 neu) |
| T12-1, T12-2, T12-5 bis T12-10, T12-12 bis T12-16 | 2026-10-18 | Claude | Branch `feat/search-across-launchers-12` nach dem ersten Umbau, Linux, Xvfb 1920×1080 `--demo`; Gamepad nur nachgestellt (lokale Strg+Alt-Tasten, die `HandleGamepad` aufrufen, nicht eingecheckt), echte Tastatur und Maus per xdotool | ✅ X mit Tastatur und Legende, Lupe ohne; X 1,3 s gehalten löscht „6epp“ ganz; LT tippen: Großbuchstaben bleiben; RT Sonderzeichen; L3: GOG aus, „Name Z–A“, 82 Treffer; RB 8–14, LB zweimal 99–105; ≡ 1,4 s: Detailseite „1 von 82“, B zurück; Pfeil ↓: zwei Reihen ohne Tastatur, RB danach blendet sie nur ein; Enter startet (Demo-Hinweis), Enter 1,4 s: Detailseite, Umschalt+F10 Menü; Klick auf den Xbox-Chip: „holl“ 2 Treffer, Text unverändert; LT gehalten + A: „Q“, danach „q“; R3-Menü, B, B | Echter Controller (Stick, LT gehalten, L3, ≡) am PC des Owners |
| T12-6, T12-9, T12-12, T12-14, T12-16 | 2026-10-18 | Juri | PC des Owners, PR #104 (zweiter Rough-Test) | ❌ Layout-Menü von Treffern überdeckt, Blättern in zwei Reihen seltsam, Sortierung nicht in der Launcher-Zeile, leere Zustände „schweben“, unnötige Sonderzeichen, RT nur Umschalter, ohne Tastatur nur per Maus bedienbar, „™“ findet Batman/Hitman | Zweiter Umbau (T12-17 neu) |
| T12-1, T12-3, T12-6, T12-9, T12-12, T12-14, T12-16, T12-17 | 2026-10-18 | Claude | Branch nach dem zweiten Umbau, Linux, Xvfb 1920×1080 `--demo`; Gamepad nachgestellt wie oben (nicht eingecheckt), bis zu 12 zusätzliche Demo-Launcher lokal nachgestellt, Tastatur und Maus per xdotool, Animation per ffmpeg mit 30 Bildern/s aufgenommen | ✅ Layout-Menü über den Treffern; Tastatur rollt in etwa 5 Bildern herein; RB gleitet zu 9–15; B fährt ein, Raster mit verblassten Reihen, RB 15–28; B, B zurück in die Bibliothek; Klick auf die Lasche fährt aus, ohne Legende; „xyzq“ zeigt „Keine Treffer“; Chips mit 6 bis 18 Launchern wie beschrieben; `dotnet test` deckt ™/®/© ab | Echter Controller am PC des Owners |
| T11-1, T12-1, T12-3, T12-12, T12-16 | 2026-10-19 | Juri | PC des Owners, PR #104 (dritter Rough-Test) | ❌ Ausblendung der Trefferzeile und der Nachbarn auf der Detailseite beginnt mit harter Kante, X/B und Tastatursymbol in der Lasche nicht auf einer Linie, Schein der leeren Zustände oben hart abgeschnitten | Verläufe, Lasche und Schein überarbeitet |
| T11-1, T12-1, T12-12, T12-16 | 2026-10-19 | Claude | Branch nach dem dritten Umbau, Linux, Xvfb 1920×1080 `--demo`, Fenster 1600×900; Gamepad nachgestellt wie oben (nicht eingecheckt) | ✅ Trefferzeile und Detailseite blenden ohne Kante aus, Nachbarn der Detailseite gleichmäßig abgedunkelt (auch nach RT); Schein hinter der Grafik ohne Kante (mit angehobenem Kontrast geprüft); X/B, Tastatursymbol und Pfeil auf einer Linie | Sichtprüfung am PC des Owners |
| T12-3, T12-16 (Leistung) | 2026-10-19 | Claude | Branch nach dem Code-Review (P1/P2), Linux-Container, Xvfb mit Software-Rendering, Release-Build, Demo lokal auf 5.004 bzw. 1.001 Spiele hochgesetzt, Gamepad nachgestellt (nicht eingecheckt); Zeit von der Eingabe bis zum fertigen Layout bzw. bis zum übernächsten Frame. `GameSearch` allein mit 5.000 Titeln (Ø 32 Zeichen) per Benchmark | ✅ Suche je Taste vorher 25–32 ms (Spitzen 66 ms), jetzt 0,3–0,8 ms, Vorbereitung beim Öffnen und Rescan einmal 10–25 ms. In der App bei 5.000 Spielen (Median / 90 %): RS in der Reihe vorher 50–125 ms, jetzt 6 / 13 ms; D-Pad im Raster vorher 120–200 ms, jetzt 22 / 43 ms (ein Ausreißer 84 ms); Tippen vorher 60–140 ms, jetzt 26 / 30 ms; X gehalten löscht alle 60 ms ohne verworfene Schritte; Detailseite aus der Suche drehen 14 / 21 ms (wie #101). Offen: der erste Buchstabe nach dem Öffnen braucht einmalig 0,35 s (vorher 0,43 s), vor allem fürs erste Bauen der Kacheln | Gegenmessung am PC des Owners mit echter Bibliothek |
| T12-7, T12-12, T12-13, T12-16 | 2026-10-19 | Juri | PC des Owners, PR #104 (vierter Rough-Test) | ❌ Bei Controller-Legende wirkt der erste Klick auf eine Taste oder die Lasche nicht; die Legende verschwindet bei Maus und die Tastatur rutscht; Treffer blättern mit LB/RB, Detailseite und Bibliothek mit LT/RT; je nach Fenstergröße 1 px Streifen am Rand der seitlichen Ausblendung | Legende immer da, Treffer mit LT/RT, Groß/Sonderzeichen mit LB/RB, Maustaste halten für Details, Verläufe über den Rand (T12-18 neu) |
| T12-1, T12-7, T12-12, T12-13, T12-14, T12-16, T12-18, G-11 | 2026-10-19 | Claude | Branch nach dem vierten Umbau, Linux, Xvfb 1920×1080 `--demo`, Fenster 1600×900 und 7 krumme Größen (1111×625 bis 1711×963) mit Skalierung 1, 1,25 und 1,5; Gamepad nachgestellt wie oben (nicht eingecheckt), Maus und Tastatur per xdotool | ✅ Mit Controller-Legende wirkt der erste Klick („ABC“, dann „e“), Legende wechselt auf Maus/Tastatur, Tastatur bleibt stehen, Zeichen auf den Tasten weg; RB schaltet die Sonderzeichen; Klick auf die Lasche fährt ein, Legende für das Raster; Bild ↓ blättert 15–28; Maustaste 1,4 s gehalten auf einem Treffer und einem Cover der Bibliothek öffnet die Details (Ring sichtbar), ein Klick wählt nur aus. Den Streifen am Rand konnte ich hier nicht nachstellen (Randspalte bei allen Größen gleich der Spalte daneben, vorher wie nachher) | Sichtprüfung am PC des Owners, Streifen bei seiner Fenstergröße |
| T12-16, T12-19 | 2026-10-19 | Juri | PC des Owners, PR #104 (fünfter Rough-Test), Vollbild-Fenstermodus | ❌ Bild ↑/↓ springt im Raster ans andere Ende, das Mausrad nicht; keine Scrollleiste; Footer der Suche links und rechts abgeschnitten (nicht mit F11) | Raster ohne Umlauf, Scrollleiste, Bänder bis an den Fensterrand (T12-19 neu) |
| T12-16, T12-19 | 2026-10-19 | Claude | Branch nach dem fünften Umbau, Linux, Xvfb 1920×1080 `--demo`, Fenster 1900×800 (breit) und 960×1000 (hoch); Gamepad nachgestellt wie oben (nicht eingecheckt); `HitWindowTests` für Raster-Enden und Scrollleiste | ✅ Legende, Tastatur, Footer-Linie und Abdunklung der Einstellungen reichen im breiten Fenster bis an den Rand, im hohen bis nach unten; neben der Suche keine Linie der Bibliothek; Scrollleiste unter der Reihe und neben dem Raster sichtbar (dünn, wie in der Bibliothek). Ziehen der Leiste nur mechanisch geprüft | Sichtprüfung am PC des Owners im Vollbild-Fenstermodus |
| T12-1 bis T12-13, T12-15 bis T12-19 | 2026-10-24 | Juri | 0.1.0-rc.1 installiert, PC des Owners | ✅ | – |
