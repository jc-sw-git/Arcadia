# Steam Controller Support — findings

Verified on the owner's PC. Read before touching `src/Arcadia.ControllerSupport`, `src/Arcadia.Pan*`, focus handling
or anything usbip related; then only the topic file you touch.

Arcadia emulates the Steam Controller itself: it reads the controller raw with SDL3 ([sdl-gamepads.md](sdl-gamepads.md)),
gives games its own Xbox pad with the per-game mode "Arcadia" / "Steam-Eingabe" ([xbox-pad.md](xbox-pad.md)), and
Pan, an elevated guardian service, hides the real controller from Steam while Steam gets a 1:1 copy
([pan.md](pan.md), [steam-controller-copy.md](steam-controller-copy.md)). SISR, which an older Arcadia started for
this, is never started any more; Arcadia only removes it on request ([sisr-removal.md](sisr-removal.md)).

- Steam gives the Steam Controller to processes it didn't start only as mouse/keyboard (desktop layout).
  `steam://forceinputappid/<appid>` only switches the layout; it doesn't deliver XInput to a foreign process.
  Restarting Arcadia through Steam was tried and dropped: everything Arcadia then starts keeps "Arcadia" running in Steam.
- Per-game mode "Arcadia" / "Steam-Eingabe" (#178): a game's own choice (`AppSettings.ControllerSupportOverrides`), else
  its launcher's default (`LauncherPreferences.UsesArcadiaPad`, set on the launcher's page), else the launcher's own
  (`GamesUseSteamInput`: Steam games Steam-Eingabe, all others Arcadia). Product rule: `docs/product/launchers.md`. In
  code the feature keeps its old name, `ControllerSupportService`.
- usbip-win2 ≥ 0.9.8.1 is required; its uninstall entry was renamed between versions ("USBip version 0.9.7.7" →
  "USBip 0.9.8.1"). Updating it replaces the emulated host controller; Windows wants a reboot.
- A running Steam game is `HKCU\Software\Valve\Steam\RunningAppID` non-zero, its name from `…\Steam\Apps\<id>\Name`.
  Fixes that close Steam ask "Steam beenden?" in a modal that re-reads the running game every second, because nothing
  else reports a game ending.

| Topic | File |
|---|---|
| Every controller through SDL3, the Steam Controller raw, emulated pads told apart | [sdl-gamepads.md](sdl-gamepads.md) |
| The Steam Controller as an Xbox pad for games: VIIPER, combos, plugging in and out, per-game mode | [xbox-pad.md](xbox-pad.md) |
| Steam's desktop layout and the mouse mode, what "Arcadia in front" means | [desktop-layout.md](desktop-layout.md) |
| The puck as a USB device, what Steam sends it, what HidHide does (spike #239) | [emulated-steam-controller.md](emulated-steam-controller.md) |
| Steam gets a copy of the Steam Controller: lifecycle, consent, who gets input, the allow list | [steam-controller-copy.md](steam-controller-copy.md) |
| The copy on Windows: VIIPER fork `hidproxy`, the raw puck, the bridge | [steam-controller-copy-windows.md](steam-controller-copy-windows.md) |
| Pan, the guardian service: identity, generations, heartbeat, teardown, journal | [pan.md](pan.md) |
| Pan hides the real puck with HidHide and shows it again | [pan-hiding.md](pan-hiding.md) |
| Pan's puck restart: who may hold the puck, the copy is never restarted | [pan-puck-restart.md](pan-puck-restart.md) |
| Pan: setup with one UAC prompt, uninstall, `--restore`, the data folder, exit codes | [pan-setup.md](pan-setup.md) |
| Pan: the update at start, announced and declinable | [pan-update.md](pan-update.md) |
| Pan: firmware hold after a crash mid update | [pan-firmware-hold.md](pan-firmware-hold.md) |
| Pan: known limits and accepted risks | [pan-risks.md](pan-risks.md) |
| What an older Arcadia left of SISR, "SISR entfernen" / "SISR behalten" | [sisr-removal.md](sisr-removal.md) |
| Answering the UAC prompt by controller through a VIIPER keyboard (spike #118, story #247) | [uac.md](uac.md) |
