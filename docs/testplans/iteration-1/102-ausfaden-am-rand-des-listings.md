# #102 Ausfaden am Rand des Listings

- [x] **T102-1** Ein Tab, dessen Seite nicht ins Raster passt (z. B. 31 Spiele mit 50 pro Seite): ganz oben
      faden nur unten die angeschnittenen Kacheln aus, in der Mitte oben und unten, ganz unten nur oben;
      die erste bzw. letzte Reihe ist dann vollständig sichtbar. Der Effekt blendet weich ein und aus. · E2E: ja
- [x] **T102-2** Passt die Seite ganz ins Raster (z. B. 10 pro Seite), gibt es kein Ausfaden. · E2E: ja
- [x] **T102-3** Die fokussierte Kachel liegt mit D-Pad nie im ausgeblendeten Bereich, ihr Rahmen bleibt
      voll sichtbar. Mausrad-Scrollen schaltet das Ausfaden genauso. · E2E: ja
- [x] **T102-4** Kein Streifen unverblasster Cover am äußersten Rand des Ausfadens, auch nicht bei einer
      Fenstergröße, bei der die Oberfläche skaliert wird (z. B. 1745×1000 oder 125 % Windows-Skalierung). · E2E: ja

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T102-1, T102-3 | 2026-10-17 | Claude | Branch `feat/context-menu-and-detail-page-10-11`, Linux, Xvfb 1920×1080 `--demo`, GOG 31 Spiele, 50 pro Seite, Pfeiltasten | ✅ Oben nur unten, Mitte beidseitig, unten nur oben; Fokus jeweils frei vom Ausfaden | Mausrad und Controller am PC des Owners |
| T102-4 | 2026-10-18 | Juri | PC des Owners, PR #101 | ❌ Am unteren Rand 1–2 px Cover ohne Ausfaden | Bei skaliertem Fenster nachgestellt (Xvfb 1745×1000): 1 px Spalt zwischen Ausschnitt des Rasters und Verlauf |
| T102-4 | 2026-10-18 | Claude | Xvfb 1745×1000 `--demo`, oben und unten vergrößert | ✅ Kein Streifen mehr, der Verlauf reicht 2 px über das Raster hinaus | Kontrolle am PC des Owners |
| T102-1 bis T102-4 | 2026-10-24 | Juri | 0.1.0-rc.1 installiert, PC des Owners | ✅ | – |
