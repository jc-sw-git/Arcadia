# Architecture

Where things live. `CLAUDE.md` has the short version; read this when you need to find a class or decide where new
code goes, then only the file of the project you touch.

| Project | Content | File |
|---|---|---|
| `src/Arcadia.Launchers.Abstractions` | What a launcher is made of; the future plugin interface | [core.md](core.md) |
| `src/Arcadia.Core` | Platform-free: launchers, library, running games, search, keyboard layouts, settings | [core.md](core.md) |
| `src/Arcadia.Launchers` | The built-in launchers (Steam, Epic, demo), game start; uses only the contract and `Arcadia.Platform` | [launchers.md](launchers.md) |
| `src/Arcadia.Platform` | `ProcessWatch`: the PC's processes (by name or folder, `PathOf(pid)`, the foreground process, waits) | this table |
| `src/Arcadia.Input` | Every controller via SDL3, buttons to actions, the mouse mode | [input.md](input.md) |
| `src/Arcadia.ControllerSupport` | The controller emulation for games: VIIPER pads, Steam's copy of the Steam Controller, Pan's client | [controller-support.md](controller-support.md) |
| `src/Arcadia.Pan.Core`, `src/Arcadia.Pan` | Pan, the guardian service: its decisions, and `Pan.exe` | [controller-support.md](controller-support.md) |
| `src/Arcadia.App` | Avalonia 12 UI (MVVM); Avalonia's pitfalls in [avalonia.md](avalonia.md) | [app.md](app.md) |

## Tests

| Project | Content |
|---|---|
| `tests/Arcadia.Core.Tests` | xUnit: Core, Launchers, Input and ControllerSupport; `ArchitectureTests` (which project may reference which) |
| `tests/Arcadia.Pan.Tests` | xUnit: Pan's core with a fake clock and cleanup; `ServiceTests` installs the real service on the Windows CI runner only |
| `tests/Arcadia.App.Tests` | Acceptance tests of the core flows, xUnit v3 with `Avalonia.Headless.XUnit` only for the UI thread: `ArcadiaDriver` builds Arcadia through the App's own `ArcadiaComposition`, with `TestLauncher`s (demo games, starts recorded), a fake clock, settings in a temp folder and the log out of the user's folder (`AppLog.Directory`); `Press`/`Hold` are the controller (`PressButton` through `GamepadRouting`), `HoldPad` holds D-pad or stick through `GamepadPolls`, `Installs`/`Starts.Failure` play the launcher, `FollowInstallsAsync`/`FollowRunningGamesAsync` are the App's timers, `IsDemo` uses the real demo data; `FakeSteam`/`FakeSisrPc`/`FakeNetwork` stand in for Windows, Steam and what an older Arcadia left of SISR, `FakeReleases` for GitHub and Velopack. `ArcadiaWindow` shows the real `MainWindow` headless with Skia (`TestApplication`): clicks, keys, wheel, pixels, focus rings. One test per test case ID (`Trait("TestCase", …)`). `StartWithLaunchersAsync` uses the real `BuiltInLaunchers` over Steam and Epic files built in a temp folder (`Fixtures/`, `LauncherFolders`); launcher URIs go to `GameLaunchService`'s `open` and are recorded, `SteamPc` pretends Windows for Steam Controller Support |

## Big Picture layout

Big Picture UI is laid out at 1920×1080 inside a `Viewbox`; a window of another shape gets bars in the window's
background beside or above and below it. Bands that run from edge to edge (footer line, the search's legend and
keyboard, dialog dimming, the search's background) reach into those bars with the `bleed` classes in `Theme.axaml`,
so the `Viewbox` doesn't clip. Focus/navigation is done in the
view models (`MainViewModel.Handle(UiAction)`), not with Avalonia focus: buttons are
`Focusable=False`, keys are handled in a tunnelling `KeyDown` handler.
