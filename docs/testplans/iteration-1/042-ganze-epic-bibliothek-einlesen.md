# #42 Ganze Epic-Bibliothek einlesen

- [x] **T42-1** Der Epic-Tab zeigt alle Spiele der Epic-Bibliothek. Erlaubte Abweichung: Unreal
      Engine und Unreal Editor for Fortnite fehlen. · E2E: ja (`catcache.bin` als Testdatei)
- [x] **T42-2** Keine DLCs, Season Pässe, Betas, Soundtracks oder Vorbesteller-Boni im Tab. · E2E: ja
- [x] **T42-3** Ohne `catcache.bin` (Epic nie gestartet) zeigt der Tab nur die installierten Spiele
      und keinen Fehler. · E2E: ja
- [x] **T42-4** Titel ohne störende `?` von Markenzeichen; Umlaute bleiben bis #43 bekannt kaputt. · E2E: ja

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T42-1 | 2026-10-10 | nicht festgehalten | Commit `4bad13a`, PC des Owners | ✅ 331 statt 333, Abweichung erklärt | – |
| T42-1 bis T42-4 | 2026-10-24 | Juri | 0.1.0-rc.1 installiert, PC des Owners | ✅ | – |
