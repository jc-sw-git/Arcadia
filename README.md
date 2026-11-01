# Arcadia

A game launcher that unites all other launchers. Every connected launcher
(Steam, Epic, GOG, Ubisoft, EA, Xbox, Battle.net, Amazon, your own) gets its own
tab. Arcadia runs fully by controller in **Big Picture mode** and with mouse and
keyboard in **desktop mode**.

> Status: **before the first release (0.x).** Steam and Epic work for real (library,
> game start, installs, updates); the other launchers exist only as demo data so far.
> On top of that: detail page, context menu, search across all launchers with an
> on-screen keyboard, and controller emulation for games (the Steam Controller and every other common pad).
> Everything else: see the
> [issues](https://github.com/jc-sw-git/Arcadia/issues?q=is%3Aissue+label%3Aroadmap).
>
> The user interface is German for now; localization is planned.

## Installation

Windows x64 only. The installer `Arcadia-win-Setup.exe` is available under
[Releases](https://github.com/jc-sw-git/Arcadia/releases); it installs without admin rights
to `%LOCALAPPDATA%\Arcadia`, and Arcadia updates itself from then on.

The installer is **not signed yet**, so Windows SmartScreen warns on the first start
("Windows protected your PC"): "More info" → "Run anyway". If you don't trust that,
build Arcadia yourself (see below).

## Structure

.NET 10, C#, user interface with [Avalonia UI](https://avaloniaui.net/) (Windows; Linux is possible later, macOS is dropped).

| Project | Purpose |
|---------|---------|
| `src/Arcadia.Launchers.Abstractions` | What a launcher integration is made of (`ILauncherIntegration`, `Game`, `LauncherInfo`, `LaunchTarget`); the future plugin interface |
| `src/Arcadia.Core` | Platform-independent: launcher registry, library, sorting, paging, search, settings, running games |
| `src/Arcadia.Launchers` | One integration per launcher (Steam and Epic real, demo data for the others), game start and installation |
| `src/Arcadia.Input` | Controller input for every controller through SDL3, the mouse mode |
| `src/Arcadia.ControllerSupport` | Controller emulation for games: emulated pads through VIIPER and usbip-win2, Steam's copy of the Steam Controller, the client of Pan |
| `src/Arcadia.Pan.Core`, `src/Arcadia.Pan` | Pan, the elevated guardian service that hides the real controllers (HidHide) and gives them back if Arcadia ends unexpectedly |
| `src/Arcadia.App` | Avalonia user interface (MVVM with CommunityToolkit.Mvvm) |
| `tests/` | xUnit tests: `Arcadia.Core.Tests` (core, launchers, input, controller support), `Arcadia.Pan.Tests`, `Arcadia.App.Tests` (acceptance tests through the view models) |

Which class does what: [docs/architecture/](docs/architecture/README.md).

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

## Controller emulation

Steam passes the Steam Controller on to programs it didn't start itself only as mouse and
keyboard, and many games know only some controllers. Arcadia therefore reads every controller itself
and gives games emulated pads, with the setting "Controller-Wächter (Pan)" (on by default):

- The Steam Controller becomes an Xbox controller in every game of every launcher; per game (or per launcher) the
  mode "Arcadia" / "Steam-Eingabe" decides whether Arcadia's pad or Steam Input serves it. Steam gets a 1:1 copy of
  the Steam Controller, so its own features keep working.
- Every other controller Arcadia takes over gets an emulated pad of its kind ("Controller zuordnen" in the settings).
- The emulated pads run on [VIIPER](https://github.com/jc-sw-git/VIIPER) (Arcadia's fork, shipped with Arcadia,
  on loopback only) and the [usbip-win2](https://github.com/vadimgrn/usbip-win2) driver.
- Pan, Arcadia's guardian service, hides the real controllers from other programs with
  [HidHide](https://github.com/nefarius/HidHide) while Arcadia emulates them, and gives them back when Arcadia ends,
  also when it crashes. "Treiber installieren" installs usbip-win2, HidHide and Pan with one UAC prompt.
- An SISR that an older Arcadia installed is never started; Arcadia removes it on request.

Logs: `%APPDATA%\Arcadia\logs\arcadia.log`, Pan's in `%ProgramData%\Arcadia\Pan\pan.log`. Third-party software:
see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Contributing

Contributions are welcome: see [CONTRIBUTING.md](CONTRIBUTING.md). Please report security
vulnerabilities not as an issue but as described in [SECURITY.md](SECURITY.md).

## License

Copyright (C) 2026 jc-sw-git

Arcadia is free software under the **GNU General Public License v3.0**, see
[LICENSE](LICENSE). Anyone who distributes Arcadia or a modified version must also
distribute it under GPL-3.0 including the source code. Third-party software: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
