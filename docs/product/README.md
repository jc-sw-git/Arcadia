# Product principles

Decisions that hold across areas; the files beside this one hold the decisions per screen or flow.

- **One console over all launchers.** Primary use is full Big Picture mode. All launchers feel like one piece: ideally
  the player never notices the launcher behind a game, never leaves the sofa and never touches mouse or keyboard.
  Every flow (first start, installer and updater, error dialogs, launcher windows, UAC prompts) is reachable and
  dismissable by controller and hides the launcher where it can; what forces mouse or keyboard is named in the PR.
  Desktop mode is secondary.
- **Launchers are opt-in.** Arcadia never activates a launcher just because it is installed
  ([launchers.md](launchers.md) → First start).
- **Feature parity.** A feature for one launcher is built for every launcher unless it is truly specific to one. A
  launcher integration fills every `Game` field its launcher can supply (playtime, last played, install date, size,
  update, owned but not installed, install action); a field it can't supply is said so, with a proposed way.
- **Every controller alike.** All common pads behave the same with or without Steam running and whatever Steam Input
  is set to; a setting that works for one controller only is not built. The Steam Controller works in every game of
  every launcher without a global Arcadia toggle: Arcadia picks the path per game, the player keeps the per-game mode
  "Arcadia" / "Steam-Eingabe" ([controls.md](controls.md)). Direction for later: hide every real pad and emulate
  every controller for every game.
- **Pan** is the product name of the controller guardian ("Pan, der Wächter von Arcadia"). UI texts are written as if
  Pan already covered all controllers; the info panel says it covers only the Steam Controller so far
  ([settings.md](settings.md)).
- **Platforms.** Windows x64 today. Long-term goal, not scheduled: Arcadia on Linux (Bazzite, ideally SteamOS) with
  the same feel, control and scope, without notable compromises. macOS is dropped. New code keeps Win32 calls behind
  the existing `OperatingSystem.IsWindows()` seams, and a Windows-only design keeps a Linux path in mind.
- **Input latency.** The controller emulation (every pad Arcadia gives a game) adds from a controller's report to its
  emulated pad's at most mean 1 ms, p99 2 ms, max 4 ms; how it is reached and measured:
  `docs/controller-support/pad-latency.md`.
- **Performance yardstick.** Beyond the budgets in `CLAUDE.md`: start, focus move and search at least 2x faster than
  comparable products with a similar library and feature set, RAM not higher. Measured in #109 once artwork and metadata exist;
  a measurement that loses to a comparable product becomes a follow-up issue.
- **Plugins, later.** Third-party plugins come later: themes that can change the design fundamentally, and features
  on top. Core functions can't be changed by a plugin, at most read or interacted with through a small versioned
  interface. .NET has no sandbox, so in-process vs out-of-process (IPC) is decided in spike #71 before any plugin
  loading is built. Already in place: the DoD items "Themeable" and "Modular", `Arcadia.Launchers.Abstractions`.
- **Open source.** Arcadia is GPL-3.0 and will be fully open source with external contributors; prefer choices that
  lower their entry barrier (English, small files, launcher contributions without core changes).
