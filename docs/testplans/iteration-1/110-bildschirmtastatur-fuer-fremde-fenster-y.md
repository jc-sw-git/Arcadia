# #110 Bildschirmtastatur für fremde Fenster (Y im Mausmodus)

- [x] **T110-1** Mausmodus an, Editor (Notepad) vorne, Textfeld aktiv: Y öffnet die Bildschirmtastatur unten in der
      Mitte des Monitors, auf dem der Editor liegt; der Editor behält den Fokus (Titelleiste aktiv, Cursor blinkt).
      Reihen wie die echte deutsche Tastatur: ⌫ „Löschen“ oben rechts, ↹ Tab, hohes ↵ Enter über zwei Reihen,
      Feststelltaste als leere dunkle Taste, ⇧ links und rechts, Leertaste, AltGr, keine Strg/Alt/Win-Tasten; rechts
      „Layout: DE“, „Nach oben“, „Ausblenden“ und die Pfeile. Alle Tasten mit dunklerer Unterkante. Die Namen (Tab, Umschalt, Enter, Leertaste, Layout, Ausblenden …)
      stehen mittig in der Taste, Symbol bzw. Icon links, Controller-Taste rechts am Rand. · E2E: nein
- [x] **T110-2** D-Pad bewegt den Fokus (über die Feststelltaste hinweg, vom hohen Enter nach oben/unten weiter),
      A tippt das gezeigte Zeichen in den Editor, X löscht (gehalten: wiederholt), Y tippt ein Leerzeichen,
      ≡ Enter, ↹ Tab, Pfeile bewegen den Cursor. Ä, €, @, { und ^ (Tottaste: sofort, ohne Folgebuchstaben) kommen
      richtig an. · E2E: nein
- [x] **T110-3** LB tippen schaltet Umschalt an und aus, gehalten nur solange; RB genauso AltGr, nur eins zugleich.
      Mit Umschalt an: Pfeile markieren Text, ↹ springt zurück (Umschalt+Tab). · E2E: nein
- [x] **T110-4** R3 öffnet das Layout-Menü oben rechts (D-Pad, A wählt, B schließt nur das Menü). Bei US: Tastatur
      in ANSI-Form (breites Enter in der mittleren Reihe, \ darüber, breites linkes ⇧), AltGr ausgegraut. Das
      gewählte Layout gilt danach auch in Arcadias Suche und umgekehrt. · E2E: ja
- [x] **T110-5** ⧉ kurz: die Tastatur springt an den oberen Rand, die Taste heißt „Nach unten“ (Pfeil nach unten);
      nochmal: zurück. Beim nächsten Öffnen (auch nach Neustart) liegt sie am zuletzt gewählten Rand – außer die
      Einfügemarke des Ziels läge darunter, dann am anderen. · E2E: nein
- [x] **T110-6** B blendet die Tastatur aus, der Mausmodus bleibt (A klickt wieder, B ist wieder Esc). ⧉ gehalten
      (1 s) holt Arcadia zurück und schließt die Tastatur; ⧉ + ≡ beendet den Mausmodus samt Tastatur; ein Spiel,
      das nach vorne kommt, schließt sie auch. Die Sticks, RT und LT bleiben bei offener Tastatur die Maus. · E2E: nein
- [x] **T110-7** Mit der Maus: ein Klick auf eine Taste tippt sie, der Editor behält den Fokus, der Controller-Fokus
      springt auf die geklickte Taste; die Legende oben zeigt nach dem Klick „Klick Taste tippen“, nach einer
      Controller-Taste wieder die Buttons. · E2E: nein
- [x] **T110-8** Mehrere Monitore bzw. 4K: Die Tastatur öffnet auf dem Monitor des Vordergrundfensters und ist so
      groß wie Arcadias Big-Picture-Ansicht dort (auf 4K doppelt so groß wie auf Full HD). · E2E: nein
- [x] **T110-9** „So funktioniert's“ listet „Y: Bildschirmtastatur (B blendet sie aus)“ und nennt bei den Grenzen,
      dass Windows die Tastatur über Spielen im exklusiven Vollbild nicht zeigt. „Zurück zu Arcadia“: Haltedauer
      1–9 s, kein „Sofort“. · E2E: ja
- [x] **T110-10** Suche mit X öffnen: dieselbe Tastatur wie im Mausmodus (Anordnung, Sondertasten, rechter Block),
      nur ↹ und „Nach oben“ fehlen, ihre Plätze bleiben leer. D-Pad und linker Stick bewegen den Fokus, ↵ (A oder
      Klick) startet den fokussierten Treffer wie ≡, „Ausblenden“ fährt die Tastatur ein wie B. Filter und Sortierung
      weiter mit L3 oder dem Knopf in der Chip-Reihe. · E2E: ja
- [x] **T110-11** In der Suche „hollow“ tippen, dann dreimal ← der Tastatur: die Einfügemarke steht nach „hol“, ein
      getipptes Zeichen und ⌫ wirken dort, ebenso eine echte Tastatur. ↑ setzt sie an den Anfang, ↓ ans Ende. · E2E: ja
- [x] **T110-12** Mausmodus an, Arcadias Suche vorne (per RT auf den Such-Chip geöffnet): Y öffnet keine zweite
      Tastatur, die Tastatur der Suche fährt aus und nimmt die Belegung aus T110-2/T110-3, die Legende zeigt LS, RT, LT,
      RS, L3. ≡ startet den Treffer. B fährt sie ein, danach sind A und B wieder Klick und Esc. Kommt ein anderes
      Fenster nach vorne, geben die Knöpfe die Tastatur der Suche frei. · E2E: nein
- [x] **T110-13** Tastatur im Mausmodus, AltGr an (RB): von ↹ nach unten landet der Fokus auf ⇧ (LB), nicht auf ↵;
      von ² nach links auf ↹; vom hohen ↵ nach links auf ~, egal ob er von „Löschen“ oder von „Umschalt“ kam. Ohne
      AltGr: von „a“ nach links auf ↹. In der Suche ebenso (dort ohne ↹: von ² nach links auf ⇧). · E2E: ja
- [x] **T110-14** Der Fokusrahmen ist an allen vier Rändern der Tastatur ganz zu sehen (^, ⌫, „Layout: DE“, →, ⇧
      links unten, Leertaste), im Mausmodus und in der Suche. · E2E: ja
- [x] **T110-15** Tastatur offen, ein anderes Fenster „immer im Vordergrund“ (z. B. der Task-Manager mit dieser
      Option) nach vorne holen: spätestens nach ¼ s liegt die Tastatur wieder darüber. Startmenü und Windows-Suche
      dürfen sie verdecken, solange sie offen sind (Grenze von Windows, siehe `docs/product/mouse-mode.md`). · E2E: nein

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T110-1, T110-3, T110-4, T110-5, T110-7 (nur Ansicht) | 2026-10-22 | Claude | Branch `feat/bildschirmtastatur-fuer-fremde-fenster-110`, Linux, Xvfb 1920×1080 `--demo`; Tastaturfenster lokal per Testcode geöffnet und Tasten ausgelöst (nicht eingecheckt) | ✅ DE unten mit hohem Enter, Umschalt- und AltGr-Ebene (leere Tasten übersprungen), US oben in ANSI-Form mit offenem Layout-Menü und ausgegrautem AltGr, Legende nach Mausklick. Fokus behalten, `SendInput`, Einfügemarke und Monitore nur unter Windows prüfbar | Lauf am PC des Owners |
| T110-2 bis T110-6 (Logik) | 2026-10-22 | Claude | `dotnet test` (`MouseModeKeyboardTests`, `FullKeyboardTests`, `KeyboardPlacementTests`, `ReturnWithViewTests`, `SettingsServiceTests`) | ✅ Belegung bei offener Tastatur, ⧉ kurz vs. gehalten, ≡ als Enter, Übergabe gehaltener Tasten, ISO/ANSI-Form, Fokus am hohen Enter, Platzierung und Rand, „Sofort“ → 1 s | Echte Eingaben unter Windows nicht geprüft |
| T110-1 bis T110-9 | 2026-10-22 | owner | Screenshots aus PR #134 | ⚠️ Sieht gut aus, Test unter Windows später. Neu: dieselbe Tastatur für alle Tastaturen in Arcadia, Module können Tasten ausgrauen oder ausblenden | Suche nutzt die gemeinsame Tastatur (T110-10 bis T110-12 neu) |
| T110-10 bis T110-12 (nur Ansicht), T110-4 | 2026-10-22 | Claude | Branch nach der gemeinsamen Tastatur, Linux, Xvfb 1920×1080 `--demo`; Suche und Tasten lokal per Testcode ausgelöst (nicht eingecheckt); `dotnet test` (`TextWithCaretTests`) | ✅ Suche mit der gemeinsamen Tastatur (ohne ↹ und „Nach oben“, Plätze leer), Layout-Menü passt in die Tastatur, Einfügemarke nach dreimal ← in „hol\|low“, Mausmodus-Legende in der Suche; Fenster im Mausmodus unverändert (DE mit Umschalt, US oben mit Layout-Menü). Controller, Mausmodus-Weitergabe und Start per ↵ unter Windows nicht geprüft | Lauf am PC des Owners |
| T110-2, T110-10 | 2026-10-22 | owner | PC des Owners, PR #134 | ❌ Startmenü liegt über der Tastatur; Fokusrahmen an den Rändern abgeschnitten; mit AltGr führt ↓ von ↹ auf ↵, ← von ² tut nichts, ← vom hohen ↵ hängt davon ab, woher der Fokus kam | Navigation nach Abstand über alle Reihen, das hohe ↵ ist in beiden Reihen eine Taste (Test `On_the_german_altgr_level_…`); `UserControl` schnitt den Rahmen ab; Tastatur geht bei jedem Vordergrundwechsel wieder nach oben, über das Startmenü nur mit UIAccess (T110-13 bis T110-15 neu) |
| T110-13, T110-14 | 2026-10-22 | Claude | Branch nach dem Fix, Linux, Xvfb 1920×1080 `--demo`; Fenster und Suche lokal per Testcode geöffnet, Tasten ausgelöst (nicht eingecheckt); `dotnet test` (`KeyGridTests`, `FullKeyboardTests`) | ✅ Vor dem Fix Rahmen an ^ und → außen abgeschnitten, danach ganz, im Fenster und in der Suche; AltGr an: Fokus springt von „a“ auf @, ← auf ↹, ↓ auf ⇧. T110-15 nur im Code, unter Windows nicht geprüft | Lauf am PC des Owners |
| T110-1, T110-10 | 2026-10-22 | owner | PC des Owners, PR #134 | ⚠️ Tastennamen sollen mittig stehen, Icons und Controller-Tasten am Rand | Namen mittig (`CentredDockPanel`), Pfeile mittig als ihr einziges Zeichen |
| T110-1, T110-10 (nur Ansicht) | 2026-10-22 | Claude | Branch nach dem Fix, Linux, Xvfb 1920×1080 `--demo`; Fenster und Suche lokal per Testcode geöffnet (nicht eingecheckt) | ✅ DE und US im Fenster, DE in der Suche: Namen mittig, beim schmalen AltGr und hohen Enter so weit mittig, wie Symbol und Controller-Taste Platz lassen; Pfeile mittig | Lauf am PC des Owners |
| T110-1 bis T110-15 | 2026-10-24 | owner | 0.1.0-rc.1 installiert, PC des Owners | ✅ | – |
