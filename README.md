# Arcadia

Ein Game-Launcher, der alle anderen Launcher vereint. Jeder verbundene Launcher
(Steam, Epic, GOG, Ubisoft, EA, Xbox, Battle.net, Amazon, eigene) bekommt einen
eigenen Tab. Arcadia läuft im **Big-Picture-Modus** komplett per Controller und im
**Desktop-Modus** mit Maus und Tastatur.

> Status: **vor der ersten Version (0.x).** Steam und Epic laufen echt (Bibliothek,
> Spielstart, Installationen, Updates), die übrigen Launcher gibt es bisher nur als
> Demo-Daten. Dazu Detailseite, Kontextmenü, Suche über alle Launcher mit
> Bildschirmtastatur und Steam Controller Support. Alles Weitere siehe
> [docs/roadmap.md](docs/roadmap.md).
>
> Arcadia ist aus SteamButler hervorgegangen. Der alte Rust-Code liegt als
> Vorlage für die Portierung unter [`legacy/steambutler-rs`](legacy/steambutler-rs).

## Installation

Nur Windows x64. Den Installer `Arcadia-win-Setup.exe` gibt es unter
[Releases](https://github.com/jc-sw-git/Arcadia/releases); er installiert ohne Adminrechte
nach `%LOCALAPPDATA%\Arcadia` und Arcadia aktualisiert sich danach selbst.

Der Installer ist **noch nicht signiert**. Windows SmartScreen warnt deshalb beim ersten
Start („Der Computer wurde durch Windows geschützt“): „Weitere Informationen“ → „Trotzdem
ausführen“. Wer dem nicht traut, baut Arcadia selbst (siehe unten).

## Aufbau

.NET 10, C#, Oberfläche mit [Avalonia UI](https://avaloniaui.net/) (Windows, macOS, Linux).

| Projekt | Zweck |
|---------|-------|
| `src/Arcadia.Core` | Plattformunabhängig: Modell (`Game`, `LauncherInfo`, `LaunchTarget`), Sortierung, Paginierung samt Seitengrößen-Regel, Such-Präfix (`/steam-epic …`) |
| `src/Arcadia.Launchers` | Scanner je Launcher (Steam und Epic real, Demo-Daten für die übrigen), Spielstart und Installation |
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
| Spielen (beim Loslassen) | A | Enter / Leertaste |
| Zurück / Dialog schließen | B | Esc |
| Launcher wechseln | LB / RB | Q / E, Strg+Tab |
| Seite wechseln (Detailseite: Spiel wechseln) | LT / RT | Bild ↑ / Bild ↓, Umschalt+← / Umschalt+→ |
| Ansicht (Sortierung, pro Seite) | Y | V |
| Optionen eines Spiels (Kontextmenü) | View (⧉) | Kontextmenü-Taste, Umschalt+F10, Rechtsklick |
| Details eines Spiels | A halten (1 s) oder ⧉ → „Details anzeigen“ | Enter halten |
| Einstellungen | Menü (≡) | F2 |
| Suche über alle Launcher | X | F, Strg+F |
| In der Suche: löschen / Leerzeichen / Filter und Sortierung / Treffer starten / Tastaturlayout | X / Y / L3 / ≡ / R3 | Rücktaste / Leertaste / Klick auf den Sortier-Knopf / Enter / Klick auf den Globus |

Maus: Klick wählt ein Spiel aus, Doppelklick startet es. Auf der Detailseite blättert das Mausrad durch die Spiele.

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

## Mitmachen

Beiträge sind willkommen: siehe [CONTRIBUTING.md](CONTRIBUTING.md). Sicherheitslücken bitte
nicht als Issue, sondern wie in [SECURITY.md](SECURITY.md) beschrieben melden.

## Lizenz

Copyright (C) 2026 Juri Himmelreich

Arcadia ist freie Software unter der **GNU General Public License v3.0** — siehe
[LICENSE](LICENSE). Wer Arcadia oder eine abgewandelte Version weitergibt, muss sie ebenfalls
unter GPL-3.0 inklusive Quellcode weitergeben. Drittsoftware: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
