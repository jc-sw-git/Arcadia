# Roadmap

Offene Aufgaben werden als **GitHub-Issues** geführt (Label `roadmap`, als Nächstes: `prio:next`).
Roadmap: https://github.com/jc-sw-git/Arcadia/issues/26 – darunter je Bereich ein Epic (`type:epic`)
als Sub-Issue, die Aufgaben hängen als Sub-Issues am jeweiligen Epic. Features sind `type:story`
und beginnen mit einer Story-Zeile, Fehler `bug`, Technik `type:tech-debt`.

Designs: `docs/designs/` (PNG + Quellen), Canvas: https://claude.ai/artifact/8A9qxc67RcsuFmavaqCAuN

## Erledigt (Grundgerüst)

- Solution-Struktur: Core, Launchers, Input, ControllerSupport, App, Tests
- Bibliothek im Big-Picture-Layout (1920×1080, skaliert auf das Fenster)
- Launcher-Tabs, horizontal scrollbar mit Rand-Pfeilen, LB/RB
- Paginiertes Raster (10/25/50/100) mit der Regel „größer nur, wenn die kleinere Größe nicht schon alles zeigt“
- Sortierung nach Name, Installationsdatum, Zuletzt gespielt — jeweils auf-/absteigend
- „Ansicht“-Modal (Y), per Controller bedienbar
- Vorschau-Panel links (zuletzt gespielt, Spielzeit, Größe, Launcher, Speicherort)
- Update-Kennzeichnung, „Update starten“ statt „Spielen“
- Controller per XInput, Tastatur, Maus
- Epic-Scanner (portiert aus SteamButler), Demo-Daten mit `--demo`
- Epic-Spiele starten auch, wenn Epic nicht läuft (Epic wird mit `-silent` im Hintergrund gestartet)
- Steam Controller Support über SISR: Installation, Start/Stopp mit Ladeanzeige, Pause bei Steam-Spielen,
  Override pro Spiel, Überwachung samt Warnungen und „Beheben“, Update-Prüfung, Fokus-Handling
- Einstellungsdialog (F2 / View-Taste) für den Steam Controller Support
- Lesen/Schreiben von Steams `shortcuts.vdf` (byte-genau gegen die echte Datei geprüft)
- Lizenz GPL-3.0-only, `CLAUDE.md`, Prüf-Skill und Dev-Skripte
