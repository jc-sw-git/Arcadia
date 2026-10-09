# Roadmap

Grundlage sind die Entwürfe auf dem Design-Canvas
(https://claude.ai/artifact/8A9qxc67RcsuFmavaqCAuN).

## Erledigt (Grundgerüst)

- Solution-Struktur: Core, Launchers, Input, App, Tests
- Bibliothek im Big-Picture-Layout (1920×1080, skaliert auf das Fenster)
- Launcher-Tabs, horizontal scrollbar mit Rand-Pfeilen, LB/RB
- Paginiertes Raster (10/25/50/100) mit der Regel „größer nur, wenn die kleinere Größe nicht schon alles zeigt“
- Sortierung nach Name, Installationsdatum, Zuletzt gespielt — jeweils auf-/absteigend
- „Ansicht“-Modal (Y), per Controller bedienbar
- Vorschau-Panel links (zuletzt gespielt, Spielzeit, Größe, Launcher, Speicherort)
- Update-Kennzeichnung, „Update starten“ statt „Spielen“
- Controller per XInput (inkl. Steam Controller, solange Steam läuft), Tastatur, Maus
- Epic-Scanner (portiert aus SteamButler), Demo-Daten mit `--demo`
- Epic-Spiele starten auch, wenn Epic nicht läuft (Epic wird mit `-silent` im Hintergrund gestartet)
- Steam Controller Support über SISR: Installation, Start/Stopp, Pause bei Steam-Spielen,
  Override pro Spiel, Überwachung samt Warnungen, Update-Prüfung
- Lesen/Schreiben von Steams `shortcuts.vdf` (byte-genau gegen die echte Datei geprüft)

## Als Nächstes

1. **Weitere Scanner:** Steam (`libraryfolders.vdf` + `appmanifest_*.acf`), GOG (Registry), Xbox/Game Pass, Ubisoft, EA, Battle.net, Amazon
2. **Einstellungen speichern:** Sortierung, Seitengröße, Favoriten, Launcher-Reihenfolge (JSON unter `%APPDATA%\Arcadia`)
3. **Favoriten-Tab** mit Launcher-Badge auf den Covern
4. **Kontextmenü** (Menü-Taste / Rechtsklick): Spielen, Ohne Update spielen, Details, Favorit, Artwork ändern, über Launcher starten, Controller-Layout, Deinstallieren
5. **Artwork** über SteamGridDB, inkl. „Bei mehreren Treffern selbst auswählen“
6. **Suche** über alle Launcher mit Bildschirmtastatur und `/keyword`-Präfix (Parser existiert bereits)
7. **Lokalisierung** DE/EN/FR/ES (Texte in Ressourcendateien)
8. **Detailseite**, **Einstellungen** (Launcher, Big Picture, Darstellung, Lokalisierung)
9. **Big Picture:** Anzeige-/Audiogerät umschalten (aus BigPictureAudio), Autostart
10. **Sounds & Musik** inkl. Spotify (OAuth PKCE)
11. **Desktop-Modus** als eigenes Layout
12. **Controller unter macOS/Linux** über SDL

## Offen

- Schriftart: Entwürfe nutzen Figtree, das Gerüst vorerst Inter (mit Avalonia ausgeliefert)
- Launcher-Logos: neutrale Standard-Symbole mitliefern, Original-Logos per Upload
