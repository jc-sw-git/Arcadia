# Roadmap

Offene Aufgaben werden als **GitHub-Issues** geführt (Label `roadmap`, als Nächstes: `prio:next`).
Roadmap: https://github.com/jc-sw-git/Arcadia/issues/26 – darunter je Bereich ein Epic (`type:epic`)
als Sub-Issue, die Aufgaben hängen als Sub-Issues am jeweiligen Epic. Features sind `type:story`
und beginnen mit einer Story-Zeile, Fehler `bug`, Technik `type:tech-debt`.
Manuelle Tests je Iteration und der Ablauf Feature- → Abnahme-Iteration: `docs/testplans/`.

Designs: `docs/designs/` (PNG + Quellen), Canvas: https://claude.ai/artifact/8A9qxc67RcsuFmavaqCAuN

## Refinement

Bevor Code entsteht, gehen Owner und Claude die wahrscheinlichsten Szenarien gemeinsam durch
(Skill `.claude/skills/refinement`). Das Ergebnis steht danach im Issue (Owner, 2026-10-14):

- **Iteration 1 (Rest):** jedes Issue, das noch bearbeitet wird, wird vorher einzeln refined.
- **Ab Iteration 2:** ein Refinement pro Epic, das dessen Umfang konkret absteckt. Das Ergebnis wird
  in schlanke Issues geschnitten, die als Sub-Issues am Epic hängen.
- Jedes Issue ist ein in sich geschlossenes Inkrement, das in das Epic einzahlt.
- Richtwert: Was ein Issue an Kontext braucht (Code, Doku, Logs, Diskussion), bleibt unter etwa
  150k Tokens. Mehr ist ein Zeichen, das Issue weiter zu teilen; keine harte Grenze.
- Ein so geschnittenes Issue wird ohne neues Refinement umgesetzt; offene Fragen klärt der Thread
  kurz mit dem Owner, statt eine neue Fragerunde zu starten (Retro Iteration 1, #150).

## Erledigt (Grundgerüst)

- Solution-Struktur: Core, Launchers, Input, ControllerSupport, App, Tests
- Bibliothek im Big-Picture-Layout (1920×1080, skaliert auf das Fenster)
- Launcher-Tabs, horizontal scrollbar mit Rand-Pfeilen, LB/RB
- Paginiertes Raster (10/25/50/100) mit der Regel „größer nur, wenn die kleinere Größe nicht schon alles zeigt“
- Sortierung nach Name, Installationsdatum, Zuletzt gespielt — jeweils auf-/absteigend
- „Ansicht“-Modal (Y), per Controller bedienbar
- Vorschau-Panel links (zuletzt gespielt, Spielzeit, Größe, Launcher, Speicherort)
- Update-Kennzeichnung (Symbol und Hinweis; A startet das Spiel, der Launcher aktualisiert vorher)
- Controller per XInput, Tastatur, Maus
- Epic-Scanner (portiert aus SteamButler), Demo-Daten mit `--demo`
- Epic-Spiele starten auch, wenn Epic nicht läuft (Epic wird mit `-silent` im Hintergrund gestartet)
- Steam Controller Support über SISR: Installation, Start/Stopp mit Ladeanzeige, Pause bei Steam-Spielen,
  Override pro Spiel, Überwachung samt Warnungen und „Beheben“, Update-Prüfung, Fokus-Handling
- Einstellungsdialog (F2 / View-Taste) für den Steam Controller Support
- Lesen/Schreiben von Steams `shortcuts.vdf` (byte-genau gegen die echte Datei geprüft)
- Lizenz GPL-3.0-only, `CLAUDE.md`, Prüf-Skill und Dev-Skripte
