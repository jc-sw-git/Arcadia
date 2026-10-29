# Steam Controller Support — findings

All verified on the owner's PC, 2026-10-09 onwards. Read before touching `src/Arcadia.ControllerSupport`, focus
handling or anything usbip related.

Arcadia emulates the Steam Controller itself: its own Xbox pad for games and the per-game mode "Arcadia" /
"Steam-Eingabe" ([xbox-pad.md](xbox-pad.md)), with Pan hiding the real controller and Steam getting a copy
([pan.md](pan.md)). SISR, which an older Arcadia started for this, is never started any more; Arcadia only removes it on
request or leaves it alone ([sisr-removal.md](sisr-removal.md), #214).

- Steam gives the Steam Controller to processes it didn't start only as mouse/keyboard
  (desktop layout). `steam://forceinputappid/<appid>` only switches the layout; it does not
  deliver XInput to a foreign process. Restarting Arcadia through Steam was tried and dropped:
  everything Arcadia then starts (e.g. the Epic launcher) keeps "Arcadia" running in Steam.
- The per-game mode: Steam games start on "Steam-Eingabe", all others on "Arcadia";
  `AppSettings.ControllerSupportOverrides` stores only deviations (in code the feature keeps its old name,
  `ControllerSupportService`).
- usbip-win2 ≥ 0.9.8.1 is required. Its uninstall entry was renamed between versions
  ("USBip version 0.9.7.7" → "USBip 0.9.8.1"). Updating it replaces the emulated host
  controller; Windows wants a reboot.
- A running Steam game is `HKCU\Software\Valve\Steam\RunningAppID` non-zero, its name from
  `…\Steam\Apps\<id>\Name` (owner's PC, 2026-10-16). Fixes that close Steam ask "Steam beenden?" in a modal that
  re-reads the running game every second, because nothing else reports a game ending.

More findings, one file per topic (read only the one you touch):

| Topic | File |
|---|---|
| What an older Arcadia left of SISR, "SISR entfernen" / "SISR behalten", the marker with Steam closed (#214) | [sisr-removal.md](sisr-removal.md) |
| Steam's desktop layout and the mouse mode (#64) | [desktop-layout.md](desktop-layout.md) |
| Steam Input without SISR: overlay helper, raw SDL3, VIIPER from C# (spike #112) | [steam-input-helper.md](steam-input-helper.md) |
| Every controller through SDL3, the Steam Controller raw, SISR's virtual pad told apart (#72, #212) | [sdl-gamepads.md](sdl-gamepads.md) |
| The Steam Controller as an Xbox pad for games, without SISR: VIIPER, combos, plugging in and out (#213); Arcadia's own VIIPER on loopback with auth (#304) | [xbox-pad.md](xbox-pad.md) |
| Hiding the real Steam Controller from Steam and showing it a 1:1 copy: HidHide, puck descriptors, latency (spike #239) | [emulated-steam-controller.md](emulated-steam-controller.md) |
| Pan, the guardian service: protocol, journal, teardown, setup with one UAC prompt, update, uninstall, `--restore` (#243) | [pan.md](pan.md) |
| Pan: setup with one UAC prompt (usbip-win2, HidHide), missing parts in the settings, uninstall hook with the drivers, `--restore`, securing the data folder | [pan-setup.md](pan-setup.md) |
| Pan: the mandatory update at start (exactly the bundled Pan, SHA-256), its page and failure cases (#297) | [pan-update.md](pan-update.md) |
| Pan hides the real Steam Controller with HidHide and shows it again in every teardown (#243 B1) | [pan-hiding.md](pan-hiding.md) |
| Pan keeps the puck hidden through a crash mid firmware attempt until Steam is closed (#242) | [pan-firmware-hold.md](pan-firmware-hold.md) |
| Pan's puck restart: Arcadia's SDL let go first, the copy is never hidden (#308) | [pan-puck-restart.md](pan-puck-restart.md) |
| Steam gets a copy of the Steam Controller: lifecycle, who gets input, the allow list and the firmware lock (#242) | [steam-controller-copy.md](steam-controller-copy.md) |
| The copy on Windows: VIIPER fork `hidproxy`, pinned `viiper.exe`, raw puck, bridge, SDL ignoring it (#242 B, C) | [steam-controller-copy-windows.md](steam-controller-copy-windows.md) |
| Pan: known limits and accepted risks (reused server port, VIIPER nonce reuse, late usbip step, unsigned `Pan.exe`, HidHide races) | [pan-risks.md](pan-risks.md) |
| Answering the UAC consent prompt by controller through a VIIPER virtual keyboard/mouse (spike #118) | [uac.md](uac.md) |
