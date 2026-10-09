# Arcadia

Ein Game-Launcher, der alle anderen Launcher vereint. Jeder verbundene Launcher
(Steam, Epic, GOG, Ubisoft, EA, Xbox, Battle.net, Amazon, eigene) bekommt einen
eigenen Tab. Arcadia läuft im **Big-Picture-Modus** komplett per Controller und im
**Desktop-Modus** mit Maus und Tastatur.

> Status: **Grundgerüst.** Bibliothek mit Launcher-Tabs, paginiertem Raster,
> Vorschau, Sortierung/Seitengröße und Controller-Steuerung läuft. Echter Launcher
> bisher: Epic. Alles Weitere siehe [docs/roadmap.md](docs/roadmap.md).
>
> Arcadia ist aus SteamButler hervorgegangen. Der alte Rust-Code liegt als
> Vorlage für die Portierung unter [`legacy/steambutler-rs`](legacy/steambutler-rs).

## Aufbau

.NET 10, C#, Oberfläche mit [Avalonia UI](https://avaloniaui.net/) (Windows, macOS, Linux).

| Projekt | Zweck |
|---------|-------|
| `src/Arcadia.Core` | Plattformunabhängig: Modell (`Game`, `LauncherInfo`, `LaunchTarget`), Sortierung, Paginierung samt Seitengrößen-Regel, Such-Präfix (`/steam-epic …`) |
| `src/Arcadia.Launchers` | Scanner je Launcher (Epic real, Demo-Daten), Spielstart |
| `src/Arcadia.Input` | Controller-Eingabe (XInput unter Windows) |
| `src/Arcadia.ControllerSupport` | „Steam Controller Support“: verwaltet SISR (Installation, Start/Stopp, Updates) und überwacht SISR, VIIPER, usbip-Treiber und Steam |
| `src/Arcadia.App` | Avalonia-Oberfläche (MVVM mit CommunityToolkit.Mvvm) |
| `tests/Arcadia.Core.Tests` | xUnit-Tests für Kern und Scanner |

## Build & Ausführen

Voraussetzung: [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build
dotnet test
dotnet run --project src/Arcadia.App -- --demo
```

| Argument | Wirkung |
|----------|---------|
| `--demo` | Ergänzt fiktive Bibliotheken für Steam, GOG, Ubisoft, EA, Xbox, Battle.net und Amazon |
| `--fullscreen` | Startet im Vollbild (umschalten mit `F11`) |

## Steuerung

| Aktion | Controller | Tastatur |
|--------|-----------|----------|
| Navigieren | D-Pad / linker Stick | Pfeiltasten |
| Spielen bzw. Update starten | A | Enter / Leertaste |
| Zurück / Dialog schließen | B | Esc |
| Launcher wechseln | LB / RB | Q / E, Strg+Tab |
| Seite wechseln | LT / RT | Bild ↑ / Bild ↓ |
| Ansicht (Sortierung, pro Seite) | Y | V |
| Einstellungen | View (⧉) | F2 |

Maus: Klick wählt ein Spiel aus, Doppelklick startet es.

## Steam Controller Support

Steam reicht den Steam Controller an Programme, die es nicht selbst gestartet hat, nur als
Maus und Tastatur weiter. Ist „Steam Controller Support“ in den Einstellungen eingeschaltet,
betreibt Arcadia deshalb [SISR](https://github.com/Alia5/SISR): Es nimmt den Controller von
Steam Input entgegen und stellt ihn systemweit als Xbox-Controller bereit — für Arcadia und für
Spiele aus anderen Launchern.

- Arcadia lädt SISR von GitHub (SHA-256-geprüft) nach `%LOCALAPPDATA%\Arcadia\sisr`, startet es
  unsichtbar und beendet es mit Arcadia.
- Bei Spielen aus dem Steam-Launcher pausiert SISR (sonst doppelte Eingaben); pro Spiel umstellbar.
- Überwacht werden SISR, VIIPER, der usbip-Treiber (min. 0.9.8.1), Steam und Steams
  Debug-Schnittstelle; Probleme erscheinen als Warnung mit „Beheben“-Knopf.
- Neue SISR-Versionen werden täglich geprüft und zum Update angeboten.

Logs: `%LOCALAPPDATA%\Arcadia\logs\` (`arcadia.log`, `sisr.log`). Drittsoftware: siehe
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Lizenz

Copyright (C) 2026 Juri Himmelreich

Arcadia ist freie Software unter der **GNU General Public License v3.0** — siehe
[LICENSE](LICENSE). Wer Arcadia oder eine abgewandelte Version weitergibt, muss sie ebenfalls
unter GPL-3.0 inklusive Quellcode weitergeben. Drittsoftware: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
