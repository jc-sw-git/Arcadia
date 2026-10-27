# #92 „Steam Controller Support“ verständlich benennen und beschreiben

- [x] **T92-1** Die Einstellung heißt „Steam Controller Kompatibilität“ (seit #15, owner 2026-10-22); Beschreibung,
      die beiden Modi und der SISR-Hinweis stehen wie in #92. Kein Text sagt mehr „Steam Controller
      Support“ (Einstellungen, Statusleiste, Vorschau, Deinstallationsfrage). · E2E: ja
- [x] **T92-2** Die Vorschau zeigt „STEAM CONTROLLER“ mit „Steam Input“ für Steam-Spiele und
      „Xbox Controller (SISR)“ für alle anderen, als Wert ohne Dropdown-Pfeil. · E2E: ja
- [x] **T92-3** Ein Klick auf den Wert wechselt zwischen den beiden Modi; zurück auf den Startwert
      wird nichts gespeichert (`ControllerSupportOverrides` in `settings.json` leer). · E2E: ja
- [x] **T92-4** Der Eintrag in der Vorschau fehlt bei ausgeschalteter Option und, bei eingeschalteter,
      ohne eingeschalteten Steam Controller, auch wenn nur der Funk-Dongle steckt; er erscheint
      innerhalb von etwa 2 Sekunden (plus der Zeit, die SISR für den virtuellen Controller braucht) nach
      dem Einschalten des Controllers und verschwindet ebenso nach dem Ausschalten. · E2E: nein (echter Steam Controller)
- [x] **T92-5** Die Einstellungen sind so lang, dass sie scrollen: die Fußzeile mit D-Pad/A/B bleibt
      unten sichtbar, auch ganz oben und ganz unten gescrollt. · E2E: ja

Mit dem Controller erreichbar wird die Einstellung pro Spiel erst mit #10.

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T92-4 | 2026-10-16 | owner | Branch `feat/steam-controller-setup-and-naming-85-92` (Stand d2375c7), Windows, Funk-Dongle mit ausgeschaltetem Steam Controller | ❌ Modi in der Vorschau sichtbar, obwohl der Controller aus ist (der Dongle reichte) | Erkennung über SISR statt über die HID-Liste |
| T92-4, T92-5 | 2026-10-16 | Claude | Branch `feat/steam-controller-setup-and-naming-85-92`, Linux, Unit-Test (`ControllerSupportServiceTests`) und Xvfb-Screenshot 1920×1080 `--demo` | ✅ Vorschau folgt dem Running/NoController-Zustand (nur Unit-Test); Fußzeile bleibt unter dem scrollenden Inhalt sichtbar | Echter Controller am PC des Owners |
| T92-3 | 2026-10-16 | Claude | Branch `feat/steam-controller-setup-and-naming-85-92`, Linux, Unit-Test (`ControllerSupportTests`), nicht in der App | ✅ Zurück auf den Startwert entfernt die Abweichung | Ansicht am PC des Owners |
| T92-1 bis T92-5 | 2026-10-24 | owner | 0.1.0-rc.1 installiert, PC des Owners | ✅ | – |
