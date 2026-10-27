# #64 Controller als Maus nutzen (Mausmodus per Back + Start)

- [x] **T64-1** Back + Start gehalten, Arcadia vorne: ab 0,2 s füllt sich der Rand des Primärmonitors in der
      Akzentfarbe von oben Mitte über beide Seiten bis unten Mitte, nach 3 s leuchtet er kurz auf, oben in der
      Mitte steht 2 s „Mausmodus an“, der Controller vibriert kurz; danach bleibt nichts stehen. Nochmal 3 s
      halten: „Mausmodus aus“, mit demselben Rand, ebenso bei jedem weiteren Umschalten. Früher loslassen: der Rand
      läuft schnell zurück, nichts schaltet. · E2E: nein
- [x] **T64-2** Wie T64-1, aber mit dem Explorer oder dem Epic Games Launcher vorne (Arcadia im Hintergrund):
      Umschalten in beide Richtungen geht, das Overlay nimmt weder Fokus noch Klicks (das Fenster vorne bleibt
      aktiv, ein Klick neben Rand und Text trifft das Fenster darunter); kein Eintrag in Taskleiste und Alt+Tab. · E2E: nein
- [x] **T64-3** Im Mausmodus in fremden Fenstern: linker Stick bewegt den Zeiger (Totzone, langsam anfahrend),
      RT oder A klickt links, gehalten zieht es (Datei im Explorer verschieben), LT oder X klickt rechts, der
      rechte Stick scrollt senkrecht und waagrecht, Steuerkreuz sind Pfeiltasten (gehalten wiederholt), B ist
      Esc, RB Tab, LB Umschalt+Tab, Menu Enter. Arcadia reagiert dabei auf nichts davon. · E2E: nein
- [x] **T64-4** L3 antippen: Zeiger ein Drittel so schnell, nochmal antippen: wieder normal. Kurz antippen, während
      der Stick bewegt wird, schaltet genauso um. L3 halten (ab 0,5 s) und den Stick bewegen: langsam, solange
      gehalten, danach normal. · E2E: nein
- [x] **T64-5** View gehalten holt Arcadia aus dem Mausmodus nach vorne (auch aus dem minimierten Zustand, siehe T64-13). Mit „Mausmodus beenden,
      wenn Arcadia im Vordergrund ist“ (Standard) endet der Mausmodus dabei mit „Mausmodus aus“; in Arcadia von Hand
      eingeschaltet bleibt er an und bedient Arcadia wie eine Maus. Option aus: er bleibt auch beim Wechsel an.
      Die Option gilt nur bei „Von Hand“ und ist sonst abgeblendet. · E2E: nein
- [x] **T64-6** „Mausmodus“ auf „Automatisch“: Wechsel per Alt+Tab zum Explorer → „Mausmodus an“ ohne Rand und
      ohne Vibrieren; Arcadia wieder vorne → „Mausmodus aus“. Ein Spiel aus der Bibliothek starten (auch direkt im
      Launcher gestartet) → bleibt aus; erscheint erst der Epic-Launcher und dann das Spiel, geht er mit dem Spiel
      wieder aus. Im Explorer ⧉ + ≡ gehalten → „Mausmodus aus“, er bleibt aus, bis Arcadia wieder vorne war. In einem
      Spiel mit ⧉ + ≡ eingeschaltet bleibt er an; der Text sagt dann „das Spiel sieht die Eingaben weiterhin“. · E2E: nein
- [x] **T64-7** Einstellungen → Controller → Box „Mausmodus“: „Mausmodus“ wechselt mit links/rechts oder A zwischen
      „Aus“, „Von Hand“ (Standard) und „Automatisch“, der Text darunter erklärt die Wahl. Haltedauer 1 und 9 s und Zeigergeschwindigkeit 1 und 9 wirken sofort;
      mit dem Controller links/rechts ändern, A zählt hoch und fängt nach 9 wieder bei 1 an. Alles bleibt
      nach einem Neustart erhalten, der Mausmodus selbst nicht (Arcadia startet immer im normalen Modus). · E2E: ja
- [x] **T64-8** „So funktioniert's“ öffnet sich mit A, zeigt die Belegung und die Grenzen (UAC, Administrator-
      Fenster, exklusives Vollbild), B schließt es, der Fokus steht wieder auf
      „So funktioniert's“. · E2E: ja
- [x] **T64-9** Zwei Controller: jeder schaltet mit Back + Start um (vibriert nur der, der umschaltet), beide
      bewegen den Zeiger. · E2E: nein
- [x] **T64-10** Normaler Modus in Arcadia: View (Kontextmenü) und Menu (Einstellungen) wirken beim Loslassen
      oder nach 0,2 s allein gehalten; ≡ 1 s gehalten auf einem Suchtreffer öffnet weiter die Details. Beide
      zusammen gedrückt öffnen nichts. Alle anderen Tasten verhalten sich wie bisher. · E2E: ja
- [x] **T64-11** Maus bewegen oder eine Taste drücken: „Ansicht“, „Filter und Sortierung“, Einstellungen, die
      SISR-Dialoge und „So funktioniert's“ zeigen oben rechts ein ✕, ein Klick darauf schließt nur diesen Dialog.
      Nach einer Controller-Taste ist das ✕ wieder weg. „So funktioniert's“ hat keine Schaltfläche „Schließen“
      mehr; A oder B schließt es. · E2E: ja
- [x] **T64-12** „Mausmodus“ auf „Aus“: ⧉ + ≡ und der Fokuswechsel tun nichts, alle Zeilen der Box sind abgeblendet
      und behalten ihre Werte; war der Mausmodus an, endet er sofort mit „Mausmodus aus“. Wieder „Von Hand“ oder
      „Automatisch“: alles wirkt wie vorher eingestellt. · E2E: ja
- [x] **T64-13** „Mit ⧉ zurück zu Arcadia“ (Standard: an, 1 Sekunde), Arcadia im Hintergrund (Explorer, Launcher,
      ein Spiel; mit und ohne Mausmodus): View gehalten zeigt ab 0,2 s oben „Zurück zu Arcadia“, dessen Rahmen sich
      über die Haltedauer füllt, dann kommt Arcadia nach vorne, ohne dass sich das Menü öffnet. Früher loslassen:
      der Rahmen läuft zurück. Die Haltedauer geht von 1 bis 9 s, „Sofort“ gibt es nicht mehr (#110); ein
      gespeichertes „Sofort“ wird 1 s. Back + Start löst es nie aus. Aus:
      View tut im Hintergrund nichts. In Arcadia öffnet View weiter das Menü. · E2E: nein
- [x] **T64-14** Steam läuft (Steam Controller oder Xbox-Pad mit Steam Input), „Steams Desktop-Belegung ignorieren“
      (eigene Box unter „Zurück zu Arcadia“, wirkt auch bei Mausmodus „Aus“) an: Back + Start im Explorer öffnet kein Alt+Tab-Fenster mehr, im Mausmodus kommen nur Arcadias Zeiger und
      Tasten an, in Arcadia bewegt das Steuerkreuz den Fokus nur einmal. Echte Maus und Tastatur gehen dabei normal.
      In einem Spiel aus der Bibliothek ohne Mausmodus wirkt Steams Belegung für das Spiel wie gewohnt, nur ⧉ + ≡
      gehalten macht dort kein Alt+Tab mehr (Steam-Spiel mit Xbox-Controller, z. B. Castlevania). Option aus:
      Steams Desktop-Belegung wirkt wieder. `arcadia.log` nennt beim Ausschalten, wie viele Eingaben verworfen
      wurden, und dass der Filter neben einem Spiel nur bei ⧉ + ≡ wirkt; ohne angeschlossenen Controller ist er aus.
      Mit einer Maus mit 1.000 Hz im Spiel bleibt Arcadias CPU-Last gleich, ob die Option an oder aus ist. · E2E: nein
- [x] **T64-16** Ein Spiel läuft, mehrmals zwischen Spiel und Arcadia wechseln (Alt+Tab, ⧉ halten, Klick) und im
      Spiel von Hand zwischen Mausmodus und normalem Modus umschalten: Arcadia reagiert nie auf Eingaben, die
      dem Spiel gelten, und ⧉ gehalten holt Arcadia jedes Mal nach vorne. Ebenso mit einem Spiel im randlosen
      Vollbild: Arcadia ist danach wirklich zu sehen, nicht nur im Vordergrund, und wird nie unsichtbar gesteuert.
      ⧉ und ≡ mit mehr als 0,2 s Abstand gedrückt öffnen in Arcadia erst Menü bzw. Einstellungen (bewusst, damit ≡
      gehalten auf einem Suchtreffer die Details öffnet). Mit mehreren Monitoren, Arcadia und Spiel auf verschiedenen:
      Mausmodus im Spiel einschalten setzt den Zeiger in die Mitte des Spielfensters. Ein aus Arcadia gestartetes
      Spiel mit Anti-Cheat (z. B. Easy Anti-Cheat) zählt als Spiel: das Log nennt es beim Vordergrundwechsel „Spiel“. `arcadia.log` nennt jeden
      Wechsel des Vordergrunds („Vordergrund: …“) und wohin ⧉ gehalten geführt hat; bei einem Fehler das Log
      mitschicken. · E2E: nein
- [x] **T64-17** Steam Controller Support an, Steam Controller verbunden, ein Steam-Spiel auf „Steam Input“ starten:
      SISR pausiert, die Zeile „Steam Controller“ in der Vorschau und auf der Detailseite bleibt sichtbar und
      umschaltbar. Die Nachteile des Steam Controller Supports nennen, dass Mausmodus und „Zurück zu Arcadia“ mit dem
      Steam Controller in Spielen auf „Steam Input“ nicht wirken. Gegenprobe mit dem Xbox-Controller, für das Spiel in
      Steam auf „Steam Input“ gestellt: Mausmodus und ⧉ gehalten wirken weiter (abgeleitet: Steam kann den echten
      Pad vor XInput nicht verstecken).
      Ebenso nach einem Update-Neustart von Arcadia, während das Spiel läuft (Ergebnis im Verlauf notieren). · E2E: nein
- [x] **T64-18** Mausmodus an, Windows-Bildschirmtastatur (osk.exe) offen: mit A auf Tasten klicken. Noch nicht
      gesehen, Ergebnis im Verlauf notieren: der Filter verwirft vermutlich auch ihre Tasten, weil sie sie wie Steam
      per `SendInput` schickt. · E2E: nein
- [x] **T64-19** „Von Hand“, „Beenden des Mausmodus in Spielen erzwingen“: „Nein“: ⧉ + ≡ schaltet
      den Mausmodus auch in einem Spiel aus der Bibliothek ein. „Ja“: in so einem Spiel zeigt ⧉ + ≡ gehalten „Mausmodus
      ist in diesem Spiel deaktiviert“ (ohne Vibrieren), und ein eingeschalteter Mausmodus endet, sobald das Spiel nach
      vorne kommt („Mausmodus aus – in diesem Spiel deaktiviert“); im Explorer geht er weiter. „Individuell“: die Zeile
      „Standardeinstellung bei „Individuell““ wird bedienbar, Vorschau und Detailseite zeigen „Individueller
      Mausmodus“ („Standard (erlaubt)“ bzw. „Standard (deaktiviert)“ nach der Standardeinstellung → Erlaubt →
      Deaktiviert, mit A oder Klick), und jedes Spiel folgt seiner Wahl oder bei „Standard“ der Standardeinstellung. Bei „Automatisch“ oder „Aus“ sind beide Zeilen abgeblendet und die
      Schaltfläche fehlt. Alles bleibt nach einem Neustart erhalten. · E2E: nein
- [x] **T64-15** Die Boxen „Mausmodus“ und „Zurück zu Arcadia“ und „So funktioniert's“ nennen Tasten über ihr
      Symbol (⧉, ≡), nirgends „Back“, „Start“, „View“ oder „Menu“. Die Symbole stehen im Text in einer runden
      Umrandung wie in der Legende; in der Tabelle von „So funktioniert's“ auch A, B, X, LB, RB, LT, RT und L3. · E2E: ja

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T64-7, T64-8 | 2026-10-20 | Claude | Branch `feat/controller-als-maus-nutzen-64`, Linux, Xvfb 1920×1080 `--demo`, Tastatur per xdotool (Pfeiltasten, Enter, Esc) | ✅ Box unter dem Steam Controller, Haltedauer abgeblendet bei Hauptschalter aus, Stepper mit links/rechts und Enter (9 → 1), „So funktioniert's“ öffnet und schließt, Fokus zurück auf der Zeile; Speichern nur per `SettingsServiceTests` geprüft | Controller am PC des Owners |
| T64-1 (nur Overlay) | 2026-10-20 | Claude | wie oben, Overlay lokal per Testcode ausgelöst (nicht eingecheckt) | ⚠️ Rand füllt sich über die Haltedauer von oben Mitte bis unten Mitte, läuft beim Abbruch zurück, Text „Mausmodus an“ 2 s; Hintergrund unter Xvfb schwarz (keine Transparenz) | Windows: Transparenz, Klicks durchs Overlay, Vibrieren, `SendInput`, Vordergrund-Erkennung am PC des Owners |
| T64-1 bis T64-10 | 2026-10-20 | owner | PC des Owners, PR #120 (Rough-Test) | ❌ „Schließen“ in „So funktioniert's“ unpassend, besser ✕ oben rechts in allen Dialogen; Windows reagiert vereinzelt selbst auf Controller-Eingaben | ✕ in allen Dialogen (T64-11 neu) |
| T64-11 | 2026-10-20 | Claude | Branch nach dem ✕, Linux, Xvfb 1920×1080 `--demo`, Maus und Tastatur per xdotool | ✅ ✕ in Einstellungen, „Ansicht“ und „So funktioniert's“ nach Taste oder Mausbewegung; Klick schließt Einstellungen bzw. nur die Hilfe; Enter schließt die Hilfe. Ausblenden nach Controller-Taste nur im Code geprüft, SISR-Dialoge nicht geöffnet | Controller am PC des Owners |
| T64-1 | 2026-10-20 | owner | PC des Owners, PR #120, Steam läuft | ❌ Back + Start gehalten öffnet Steams Alt+Tab-Fenster (Steams Desktop-Belegung) | Hauptschalter, „Zurück zu Arcadia“ mit Haltedauer, Steams Desktop-Belegung ignorieren (T64-12 bis T64-14 neu) |
| T64-12, T64-13 (nur Einstellungen und Fenster) | 2026-10-20 | Claude | Branch nach den neuen Einstellungen, Linux, Xvfb 1920×1080 `--demo`, Tastatur per xdotool; Fenster lokal per Testcode ausgelöst und mit 5 Bildern/s aufgenommen (nicht eingecheckt) | ✅ Hauptschalter aus blendet alle Zeilen der Box ab; Haltedauer von „Zurück zu Arcadia“ mit links bis „Sofort“; „Zurück zu Arcadia“ oben in der Mitte, der Rahmen füllt sich in 3 s von oben Mitte über die runden Ecken. View, Hooks und Steam unter Windows nicht geprüft, Logik per `ReturnWithViewTests`, `InjectedInputGateTests`, `MouseModeTests` | T64-13, T64-14 am PC des Owners |
| T64-1 bis T64-10 (Logik) | 2026-10-20 | Claude | `dotnet test` (`MouseModeTests`, `SoloButtonsTests`, `MouseModeAutomationTests`, `StickCurveTests`, `GameFoldersTests`, `SettingsServiceTests`) | ✅ Umschalten, Abbruch, Belegung, Wiederholung, L3, mehrere Controller, Automatik, Spielordner, Einstellungen | Echte Eingaben unter Windows nicht geprüft |
| T64-4 | 2026-10-20 | owner | PC des Owners, PR #120 | ❌ L3 kurz geklickt, während der Stick bewegt wird, wirkt wie gehalten: der Zeiger wird beim Loslassen wieder schnell | Bewegen macht einen Druck nicht mehr zum Halten, nur noch die Dauer (0,5 s); Test `L3_clicked_briefly_while_pointing_toggles_the_slow_pointer` (vorher rot) |
| T64-15 | 2026-10-20 | Claude | Branch nach der Symbol-Umbenennung, Linux, Xvfb 1920×1080 `--demo`, Tastatur per xdotool | ✅ Beide Boxen und „So funktioniert's“ zeigen ⧉ und ≡, kein Tastenname mehr; die Symbole sind im Fließtext klein, aber lesbar | Schrift unter Windows nicht geprüft |
| T64-5, T64-6, T64-12 | 2026-10-20 | owner | PC des Owners, PR #120 | ⚠️ ⧉ + ≡ von Hand und „Automatisch einschalten“ nebeneinander wirken schlecht zusammen | „Mausmodus“ ist jetzt eine Wahl aus „Aus“, „Von Hand“ und „Automatisch“; ⧉ + ≡ übersteuert die Automatik, bis Arcadia wieder vorne war (Test `Switched_off_by_hand_it_stays_off_until_arcadia_has_been_in_front_again`) |
| T64-7, T64-12 (nur Einstellungen) | 2026-10-20 | Claude | Branch nach der Dreier-Wahl, Linux, Xvfb 1920×1080 `--demo`, Tastatur per xdotool | ✅ „Mausmodus“ wechselt mit links/rechts zwischen „Aus“, „Von Hand“ und „Automatisch“, der Text darunter wechselt mit; „Aus“ blendet die Zeilen darunter ab, „Automatisch“ nur „Beenden, wenn Arcadia nach vorne kommt“. Fokuswechsel und ⧉ + ≡ unter Windows nicht geprüft, Logik per `MouseModeAutomationTests` | T64-6 am PC des Owners |
| T64-1, T64-15 | 2026-10-20 | owner | PC des Owners, PR #120 | ❌ Rand und „Mausmodus an/aus“ nur beim ersten Umschalten richtig, danach kein Rand beim Halten, der alte Text flackert kurz auf; Symbole im Text ohne Umrandung schlecht als Taste erkennbar | Das Blinken hielt die Deckkraft 0 fest (`FillMode="Forward"`), das Overlay versteckt sich jetzt erst nach einem leeren Bild; Symbole als Badges im Text |
| T64-1 (nur Overlay), T64-15 | 2026-10-20 | Claude | Branch nach dem Fix, Linux, Xvfb 1920×1080 `--demo`; Overlay lokal per Testcode dreimal nacheinander ausgelöst und mit 5 Bildern/s aufgenommen (nicht eingecheckt) | ✅ Vor dem Fix blieb der Rand ab dem zweiten Halten unsichtbar (Deckkraft 0 im Log), danach füllt er sich jedes Mal; Badges in beiden Boxen und in „So funktioniert's“ lesbar. Das Aufflackern des alten Texts ist unter Xvfb nicht sichtbar, nur unter Windows prüfbar | T64-1 am PC des Owners |
| T64-14, T64-16, T64-17 | 2026-10-20 | owner | PC des Owners, PR #120, Steam-Spiel (Castlevania), Xbox-Controller und Steam Controller | ❌ Im Steam-Spiel macht Steam aus ⧉ + ≡ Alt+Tab; Arcadia reagiert manchmal auf Eingaben im Spiel und ⧉ holt es nicht zurück; Steam Controller auf „Steam Input“: ⧉ + ≡ und ⧉ halten wirken nicht; die Steam-Controller-Zeile verschwindet, solange SISR pausiert | Filter wirkt neben Spielen bei ⧉ + ≡; „vorne“ über das Vordergrundfenster; Steam Input als Grenze in „So funktioniert's“ (Lösung erst mit #112/#72); Zeile bleibt (Test `The_controller_still_counts_as_connected_while_SISR_is_paused_for_a_game`, vorher rot) |
| T64-14, T64-16 (nur Logik) | 2026-10-20 | Claude | `dotnet test` (`InjectedInputGateTests`, `ControllerSupportServiceTests`) | ✅ ⧉ + ≡ zählt mit Nachlauf; Steam-Controller-Zeile bleibt bei Pause. Hooks, Steam und Vordergrund unter Windows nicht geprüft | T64-14, T64-16, T64-17 am PC des Owners |
| T64-14, T64-16 (Review) | 2026-10-20 | Claude | Review zu PR #120 (`reviews/pr-120-code-review.md`), `dotnet build`/`dotnet test` unter Linux | ✅ Build und Tests grün. Neu: „vorne“ verlangt zusätzlich, dass kein fremdes Fenster Arcadia ganz verdeckt (randloses Vollbild), ⧉ gehalten hält Arcadia dann bis zum Fokuswechsel oben; neben Spielen nur der Tastatur-Hook, ohne Controller keiner, die Hooks werden alle 30 s und bei jedem Wechsel neu gesetzt. Hooks, Vordergrund und CPU-Last unter Windows nicht geprüft | T64-14, T64-16 bis T64-18 am PC des Owners |
| T64-16 (Review, Nachtrag) | 2026-10-20 | Claude | Nach owner's Antwort (drei Monitore, Vollbildart egal), `dotnet build`/`dotnet test` unter Linux | ✅ Build und Tests grün. Neu: Zeiger beim Einschalten im Spiel in die Mitte des Spielfensters; Log jedes Vordergrundwechsels und des Ziels von ⧉ gehalten. Unter Windows nicht geprüft | T64-16 am PC des Owners, mit Log |
| T64-14, T64-16 (M3, M5) | 2026-10-20 | Claude | Nach owner's Freigabe von M3 und M5; Linux, Xvfb 1920×1080 `--demo`, Tastatur per xdotool; `dotnet test` | ✅ Eigene Box „Steams Desktop-Belegung ignorieren“ unter „Zurück zu Arcadia“, mit dem Steuerkreuz erreichbar (die Liste scrollt mit), Enter schaltet um. Pfade mit `/`, doppelten Trennern, `.`/`..` und `\\?\` passen (`GameFoldersTests`, vorher rot); `LaunchedGamesTests`. Hooks, Anti-Cheat-Spiel und Vordergrund unter Windows nicht geprüft | T64-14, T64-16 am PC des Owners |
| T64-17 (nur Text) | 2026-10-20 | Claude | Linux, Xvfb 1920×1080 `--demo`, Maus per xdotool | ✅ Der Hinweis zum Steam Controller auf „Steam Input“ steht unter den Nachteilen des Steam Controller Supports, nicht mehr in „So funktioniert's“ (owner) | T64-17 am PC des Owners |
| T64-7, T64-8, T64-19, T64-17 (Texte) | 2026-10-20 | Claude | Linux, Xvfb 1920×1080 `--demo`, Tastatur per xdotool; `dotnet test` (`MouseModeInGamesTests`, `MouseModeTests`, `SettingsServiceTests`, `GameFoldersTests`, `LaunchedGamesTests`) | ✅ Neue Texte für „Von Hand“ und „Automatisch“; „Deaktivierten Mausmodus im Spiel erzwingen“ wechselt Nein/Ja/Individuell, „Standardeinstellung bei „Individuell““ wird erst bei „Individuell“ bedienbar; Vorschau und Detailseite zeigen „Individueller Mausmodus“, A wechselt Standard → Erlaubt → Deaktiviert und die Vorschau folgt. Nachteile des Steam Controller Supports gekürzt, Grenze „Spiel sieht die Eingaben“ entfernt. Sperre im Spiel unter Windows nicht geprüft (Logik per Test) | T64-19 am PC des Owners |
| T64-5, T64-19, T11-7 (Texte, Detailseite) | 2026-10-21 | Claude | Linux, Xvfb 1920×1080 `--demo`, Tastatur per xdotool; `dotnet test` | ✅ Neue Titel und Texte in der Box „Mausmodus“; Vorschau und Detailseite zeigen „Standard (deaktiviert)“, A wechselt zu „Erlaubt“; Detailseite in drei Reihen, ↓/↑ durch alle drei, mit Update-Hinweis und Hinweis der Schaltfläche in einer Zeile ragt nichts in die Kennzahlen. Steam-Controller-Zeile nicht gesehen (kein Steam Controller unter Linux) | Lauf am PC des Owners |
| T64-1 bis T64-19 | 2026-10-24 | owner | 0.1.0-rc.1 installiert, PC des Owners | ✅ | – |
