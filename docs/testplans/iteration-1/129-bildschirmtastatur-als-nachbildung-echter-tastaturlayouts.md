# #129 Bildschirmtastatur als Nachbildung echter Tastaturlayouts

- [x] **T129-1** Suche mit X öffnen, Layout DE: Die Zeichenreihen sind die der echten deutschen Tastatur
      (`^ 1 … 0 ß ´` / `q … ü +` / `a … ä #` / `< y … , . -`), angeordnet wie auf der echten Tastatur (seit #110,
      siehe T110-10).
      Jede Taste zeigt groß ihr Zeichen, klein darüber das Umschalt-Zeichen (bei Buchstaben keins) und klein
      rechts unten das AltGr-Zeichen (z. B. q mit @, e mit €, 7 mit {, + mit * und ~). · E2E: ja
- [x] **T129-2** LB kurz tippen: Tasten „⇧ Umschalt“ hervorgehoben, die Tasten zeigen nur noch das Umschalt-Zeichen
      (z. B. °, !, ?, Q, *, >), bleibt so, nochmal tippen schaltet aus. LB gehalten gilt nur, solange es gehalten
      wird. Getippt wird das gezeigte Zeichen. · E2E: ja
- [x] **T129-3** RB kurz tippen: Taste „AltGr“ hervorgehoben, die Tasten zeigen nur ihr AltGr-Zeichen, alle anderen
      sind leer; bleibt so, nochmal tippen schaltet aus, gehalten gilt es nur, solange es gehalten wird. LB schaltet
      AltGr aus und umgekehrt. Getippt wird das gezeigte Zeichen (z. B. @, €, {, ~, |, µ). · E2E: ja
- [x] **T129-4** Mit AltGr an überspringt das D-Pad leere Tasten: von € nach links @, nach rechts ~, nach oben ³,
      nach unten |; eine Reihe ganz ohne Zeichen wird übersprungen. Ohne AltGr führt hoch und wieder runter
      zurück zur selben Taste (z. B. e → 3 → e). · E2E: ja
- [x] **T129-5** Die Tottasten ^ ´ ` (und bei FR/ES ¨) tippen ihr Zeichen sofort, ohne auf den nächsten Buchstaben
      zu warten. · E2E: ja
- [x] **T129-6** R3 → nacheinander US, UK, FR, ES: Jede Tastatur zeigt die Tasten ihres echten Layouts mit
      Umschalt- und AltGr-Zeichen (US: `` ` `` … `=` / `q … ] \`, ohne <-Taste; UK: `#` und `\`, AltGr 4 = €;
      FR: AZERTY mit `& é " ' (` in der Zahlenreihe; ES: `º … ¡`, ñ). Bei US ist „AltGr“ ausgegraut und RB
      bzw. ein Klick darauf tun nichts; ist AltGr beim Wechsel zu US an, geht es aus. · E2E: ja
- [x] **T129-7** Mit der Maus: ein Klick auf eine Taste tippt das gezeigte Zeichen, Klicks auf „⇧ Umschalt“ und
      „AltGr“ schalten die Ebene um. · E2E: ja

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T129-1, T129-2, T129-3, T129-6, T129-7 | 2026-10-21 | Claude | Branch `feat/bildschirmtastatur-als-nachbildung-echter-tastaturlayouts-129`, Linux, Xvfb 1920×1080 `--demo`, Fenster 1600×900, nur Maus per xdotool | ✅ DE mit Umschalt- und AltGr-Zeichen in der bisherigen Breite; Klick auf „AltGr“ zeigt nur ² ³ { [ ] } \ @ € ~ \| µ, auf „⇧ Groß“ nur ° ! " … Q W E …; Klick auf „a“ tippt „a“; US mit ausgegrautem „AltGr“, Klick darauf ändert nichts | Controller (LB/RB gehalten, D-Pad über leere Tasten) am PC des Owners |
| T129-4, T129-5, T129-6 | 2026-10-21 | Claude | wie oben, Unit-Tests (`KeyboardLayoutTests`, `KeyGridTests`) | ⚠️ Layouts aller fünf Sprachen, Tottasten und das Überspringen leerer Tasten grün; mit dem Controller nicht gefahren | Lauf am PC des Owners |
| T129-1 bis T129-7 | 2026-10-24 | Juri | 0.1.0-rc.1 installiert, PC des Owners | ✅ | – |
