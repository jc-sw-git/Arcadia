# #41 Nicht installierte Spiele anzeigen und installieren

- [x] **T41-1** Nicht installierte Spiele sind ausgegraut und tragen das Installieren-Symbol; die
      Vorschau zeigt „Nicht installiert“, die Hauptaktion heißt „Installieren“. · E2E: ja
- [x] **T41-2** A auf einem nicht installierten Epic-Spiel öffnet den Installationsdialog im Epic
      Launcher, auch wenn Epic vorher nicht lief. · E2E: nein (echter Epic Launcher)
- [x] **T41-3** Installation aus Arcadia: „Warte auf Bestätigung“, dann Platz in der Warteschlange,
      Fortschritt in %, am Ende ein Hinweis und das Spiel ist ohne Neustart installiert. · E2E: ja
      (Epic-Log und Warteschlange als Testdateien)
- [x] **T41-4** Pausieren, Fortsetzen und Abbrechen in Epic zeigt Arcadia an; solange eine
      Installation läuft, ist „Installieren“ gesperrt. · E2E: ja (Testdateien)
- [x] **T41-5** Eine direkt in Epic gestartete Installation erscheint in Arcadia mit Status. · E2E: ja (Testdateien)
- [x] **T41-6** Ein außerhalb von Arcadia deinstalliertes Spiel erscheint als nicht installiert,
      sobald das Arcadia-Fenster wieder aktiv wird oder das Spiel ausgewählt wird. · E2E: ja
- [x] **T41-7** Sortierung nach Installationsdatum: nicht installierte Spiele stehen immer am Ende. · E2E: ja
- [x] **T41-8** Wird eine pausierte Installation abgebrochen, verschwindet ihr Fortschritt. · E2E: ja (Testdateien)
- [x] **T41-9** Ein Spiel in der Warteschlange zeigt eine Sanduhr statt des Ladekreisels, auf dem
      Cover und in der Vorschau. · E2E: ja (Testdateien)
- [x] **T41-10** Läuft eine Installation und wird in Epic ein anderes Spiel fortgesetzt oder
      gestartet, zeigt Arcadia das erste als pausiert und nur das zweite als laufend. · E2E: ja (Testdateien)

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T41-1, T41-3 bis T41-6 | 2026-10-10 | nicht festgehalten | Commit `4bad13a`, PC des Owners, echte Epic-Installationen (laut #41 und `CLAUDE.md`) | ✅ | – |
| T41-8 | 2026-10-10 | nicht festgehalten | Commit `4bad13a`, PC des Owners | ❌ Fortschritt bleibt hängen | #58, Backlog |
| T41-9, T41-10 | 2026-10-12 | Juri | PR #79, PC des Owners, echte Epic-Installationen | ❌ Warteschlange hat denselben Kreisel; nach Fortsetzen eines anderen Spiels wirken beide aktiv (Epic bricht das laufende ohne Pause-Zeile ab) | In PR #79 behoben, erneut zu testen |
| T41-1 bis T41-10 | 2026-10-24 | Juri | 0.1.0-rc.1 installiert, PC des Owners | ✅ | – |
