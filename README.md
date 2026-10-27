# Arcadia

A game launcher that unites all other launchers. Every connected launcher
(Steam, Epic, GOG, Ubisoft, EA, Xbox, Battle.net, Amazon, your own) gets its own
tab. Arcadia runs fully by controller in **Big Picture mode** and with mouse and
keyboard in **desktop mode**.

> Status: **before the first release (0.x).** Steam and Epic work for real (library,
> game start, installs, updates); the other launchers exist only as demo data so far.
> On top of that: detail page, context menu, search across all launchers with an
> on-screen keyboard, and Steam Controller support. Everything else: see
> [docs/roadmap.md](docs/roadmap.md).
>
> The user interface is German for now; localization is planned.
>
> Arcadia grew out of SteamButler. The old Rust code lives in
> [`legacy/steambutler-rs`](legacy/steambutler-rs) as a reference for porting.

## Installation

Windows x64 only. The installer `Arcadia-win-Setup.exe` is available under
[Releases](https://github.com/jc-sw-git/Arcadia/releases); it installs without admin rights
to `%LOCALAPPDATA%\Arcadia`, and Arcadia updates itself from then on.

The installer is **not signed yet**, so Windows SmartScreen warns on the first start
("Windows protected your PC"): "More info" → "Run anyway". If you don't trust that,
build Arcadia yourself (see below).

## Structure

.NET 10, C#, user interface with [Avalonia UI](https://avaloniaui.net/) (Windows, macOS, Linux).

| Project | Purpose |
|---------|---------|
| `src/Arcadia.Core` | Platform-independent: model (`Game`, `LauncherInfo`, `LaunchTarget`), sorting, paging including the page size rule, search prefix (`/steam-epic …`) |
| `src/Arcadia.Launchers` | Scanner per launcher (Steam and Epic real, demo data for the others), game start and installation |
| `src/Arcadia.Input` | Controller input (XInput on Windows) |
| `src/Arcadia.ControllerSupport` | "Steam Controller Support": manages SISR (installation, start/stop, updates) and monitors SISR, VIIPER, the usbip driver and Steam |
| `src/Arcadia.App` | Avalonia user interface (MVVM with CommunityToolkit.Mvvm) |
| `tests/Arcadia.Core.Tests` | xUnit tests for core and scanners |

## Build & run

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build
dotnet test
dotnet run --project src/Arcadia.App -- --demo
```

| Argument | Effect |
|----------|--------|
| `--demo` | Adds fictional libraries for Steam, GOG, Ubisoft, EA, Xbox, Battle.net and Amazon |
| `--fullscreen` | Starts in full screen (toggle with `F11`) |

## Controls

| Action | Controller | Keyboard |
|--------|-----------|----------|
| Navigate | D-pad / left stick | Arrow keys |
| Play (on release) | A | Enter / Space |
| Back / close dialog | B | Esc |
| Switch launcher | LB / RB | Q / E, Ctrl+Tab |
| Switch page (detail page: switch game) | LT / RT | Page Up / Page Down, Shift+← / Shift+→ |
| View (sorting, per page) | Y | V |
| Options of a game (context menu) | View (⧉) | Menu key, Shift+F10, right click |
| Details of a game | Hold A (1 s) or ⧉ → "Details anzeigen" | Hold Enter |
| Settings | Menu (≡) | F2 |
| Search across all launchers | X | F, Ctrl+F |
| In search: delete / space / filter and sorting / start result / keyboard layout | X / Y / L3 / ≡ / R3 | Backspace / Space / click the sort button / Enter / click the globe |

Mouse: a click selects a game, a double click starts it. On the detail page the mouse wheel scrolls through the games.

## Steam Controller support

Steam passes the Steam Controller on to programs it didn't start itself only as mouse and
keyboard. When "Steam Controller Support" is switched on in the settings, Arcadia therefore
runs [SISR](https://github.com/Alia5/SISR): it takes the controller from Steam Input and
provides it system-wide as an Xbox controller, for Arcadia and for games from other launchers.

- Arcadia downloads SISR from GitHub (SHA-256 verified) to `%LOCALAPPDATA%\Arcadia\sisr`, starts it
  invisibly and quits it with Arcadia.
- For games from the Steam launcher SISR pauses (otherwise input arrives twice); this can be changed per game.
- Monitored are SISR, VIIPER, the usbip driver (at least 0.9.8.1), Steam and Steam's
  debug interface; problems show up as a warning with a fix button.
- New SISR versions are checked daily and offered as an update.

Logs: `%APPDATA%\Arcadia\logs\` (`arcadia.log`, `sisr.log`). Third-party software: see
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Contributing

Contributions are welcome: see [CONTRIBUTING.md](CONTRIBUTING.md). Please report security
vulnerabilities not as an issue but as described in [SECURITY.md](SECURITY.md).

## License

Copyright (C) 2026 jc-sw-git

Arcadia is free software under the **GNU General Public License v3.0**, see
[LICENSE](LICENSE). Anyone who distributes Arcadia or a modified version must also
distribute it under GPL-3.0 including the source code. Third-party software: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
