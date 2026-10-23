# #113 Steam-Tab zeigt Spiele, die Steam nicht mehr listet

- [ ] **T113-1** Steam-Tab am PC des Owners: Half-Life 2: Episode One, Episode Two, Lost Coast und
      Plan B: Terraform fehlen, Half-Life 2 ist da. Die Anzahl installierter Spiele stimmt mit Steams
      Bibliothek (Filter „Installiert“) überein. · E2E: nein (echte Steam-Bibliothek mit verwaisten Manifesten)

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T113-1 | 2026-10-20 | Juri | PC des Owners, `main` | ❌ 80 statt 78 Spiele, die vier oben zu viel | #113 |
| T113-1 | 2026-10-20 | Claude | Juris Manifeste nur gelesen (83 in 3 Bibliotheken), Regel mechanisch nachgezählt, App nicht gestartet | ⚠️ Die vier haben als einzige `LastOwner 0` und fallen weg; 83 − 3 Werkzeuge − 4 = 76, Steam nennt 78 | Zählweise in Steam mit Juri klären, Lauf am PC des Owners |
