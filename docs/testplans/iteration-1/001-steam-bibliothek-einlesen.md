# #1 Steam-Bibliothek einlesen

- [x] **T1-1** Der Steam-Tab zeigt alle installierten Spiele aus allen Steam-Bibliotheken (auch
      auf anderen Laufwerken), aber keine Werkzeuge, Redistributables, SDKs oder Soundtracks;
      Demos bleiben drin. · E2E: ja (Manifeste als Testdateien)
- [x] **T1-2** Stichprobe gegen Steam: Größe, zuletzt gespielt und Spielzeit stimmen; das
      Installationsdatum ist das Erstelldatum des Spielordners. · E2E: ja (Testdateien)
- [x] **T1-3** Das Update-Symbol erscheint nur bei Spielen, für die Steam ein Update anzeigt; A
      startet das Spiel, Steam installiert vorher das Update. · E2E: nein (echtes Steam)
- [x] **T1-4** A, Enter und Doppelklick starten ein Steam-Spiel; Steam darf dabei im Hintergrund
      bleiben oder geschlossen sein. · E2E: nein (echtes Steam)
- [x] **T1-5** Ohne Steam (nicht installiert) gibt es keinen Steam-Tab und keinen Fehler. · E2E: ja
- [x] **T1-6** Mit `--demo` zeigt der Steam-Tab die echten Spiele statt Demo-Daten. · E2E: ja
- [ ] ~~**T1-7** Der Steam-Tab zeigt auch alle Spiele im Besitz, die nicht installiert sind.~~
      Entfällt in #1: Steams lokaler Cache enthält zurückerstattete Spiele; kommt mit #83. · E2E: ja
- [ ] ~~**T1-8** A auf einem nicht installierten Steam-Spiel öffnet Steams Installationsdialog.~~
      Entfällt in #1, kommt mit #83. · E2E: nein (echtes Steam)
- [ ] **T1-9** Eine Steam-Bibliothek, deren Laufwerk fehlt, erzeugt keinen Fehler; ihre Spiele
      fehlen im Tab und sind wieder da, sobald das Laufwerk zurück ist. · E2E: ja (Testordner)

Die SISR-Pause bei Steam-Spielen prüft G-24.

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T1-1, T1-2 | 2026-10-12 | Claude | PR-Branch, Linux, Scanner-Code gegen Kopien der echten Dateien des Owners (3 Bibliotheken, 77 Manifeste, `localconfig.vdf`), nicht in der App | ✅ 74 Spiele, ausgefiltert genau Steamworks Redistributables, Source SDK Base 2013 und ein Soundtrack; Update bei den 4 Spielen mit `StateFlags` 6/70 | Ansicht in Arcadia, Start und Update am PC des Owners |
| T1-7 | 2026-10-12 | Claude | PR-Branch, Linux, Leser gegen Kopien von `packageinfo.vdf`/`appinfo.vdf` des Owners, nicht in der App | ✅ 74 installierte + 394 nicht installierte Spiele; Abgleich mit der Zahl in Steam steht aus | Owner vergleicht mit seiner Steam-Bibliothek |
| T1-7 | 2026-10-12 | Juri | PR #80, PC des Owners | ❌ Zurückerstattete Spiele (007 First Light, Alchemy Factory) erscheinen: Steams Paket-Cache behält sie | Nicht installierte Steam-Spiele aus #80 entfernt; Steam-Anmeldung #83, diese Iteration |
| T1-1 bis T1-6 | 2026-10-24 | Juri | 0.1.0-rc.1 installiert, PC des Owners | ✅ | – |
| T1-9 | 2026-10-24 | Juri | 0.1.0-rc.1 installiert, PC des Owners | ⏭️ Nicht getestet: ein fehlendes Laufwerk lässt sich am PC des Owners nicht ohne weiteres nachstellen | Wird als Abnahmetest (E2E: ja, Testordner) in der Abnahme-Iteration geprüft |
