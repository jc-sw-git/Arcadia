# Steam Controller Support — findings

All verified on the owner's PC, 2026-10-09. Read before touching `src/Arcadia.ControllerSupport`,
focus handling or anything SISR/usbip related.

- Steam gives the Steam Controller to processes it didn't start only as mouse/keyboard
  (desktop layout). `steam://forceinputappid/<appid>` only switches the layout; it does not
  deliver XInput to a foreign process. Restarting Arcadia through Steam was tried and dropped:
  everything Arcadia then starts (e.g. the Epic launcher) keeps "Arcadia" running in Steam.
- Therefore Arcadia runs **SISR** (github.com/Alia5/SISR, GPL-3.0, Go) as a separate process
  from `%LOCALAPPDATA%\SISR` (the official install location — SISR uses the marker shortcut
  that points to its own exe; a copy elsewhere creates a second marker). Started hidden with
  `--api.listen-address localhost:47640 --update-notify none --log.file …`, stopped via
  `POST /api/v1/quit`. Ready = API answers and `/api/v1/devices` shows a `viiper_device`.
- Default: SISR runs except while a game from the Steam launcher runs (double input);
  per-game override in `AppSettings.ControllerSupportOverrides`.
  On 2026-10-18 the owner saw no double input in one Steam game set to "Xbox Controller (SISR)". His guess:
  SISR takes the controller's input before Steam Input hands it to the game. Unverified and one game only, so the
  default stays.
- Everything that starts or stops SISR (periodic tick, game pause and its end, on/off, update, fixes, exit)
  runs under `ControllerSupportService`'s gate, and only `StartSisr` starts it — after checking there that the
  support is on and no game has it paused. The state is one `SisrState`; every change goes through `TryMove`,
  which logs transitions outside the allowed table. Ticks never overlap; GitHub calls have their own timeout.
- After SISR stops, Steam keeps SISR's layout until the foreground changes — `force-config
  enforce:false` and `forceinputappid/0` are not enough. Arcadia bounces the focus to the
  desktop for 150 ms (40 ms is too short; a second Arcadia window doesn't count) and takes it
  back via `AttachThreadInput`. While SISR starts, `LockSetForegroundWindow` keeps Arcadia in front.
- usbip-win2 ≥ 0.9.8.1 is required. Its uninstall entry was renamed between versions
  ("USBip version 0.9.7.7" → "USBip 0.9.8.1"). Updating it replaces the emulated host
  controller → SISR must restart; Windows wants a reboot.

More findings, one file per topic (read only the one you touch):

| Topic | File |
|---|---|
| SISR and the Steam Input of other controllers (#124, #143) | [steam-input.md](steam-input.md) |
| Starting and stopping SISR: Steam ready, marker, VIIPER, exit, usbip installer, first-run setup | [sisr-lifecycle.md](sisr-lifecycle.md) |
| Steam's desktop layout and the mouse mode (#64) | [desktop-layout.md](desktop-layout.md) |
| Steam Input without SISR: overlay helper, raw SDL3, VIIPER from C# (spike #112) | [steam-input-helper.md](steam-input-helper.md) |
| Every controller through SDL3, the Steam Controller raw, SISR's virtual pad told apart (#72, #212) | [sdl-gamepads.md](sdl-gamepads.md) |
| The Steam Controller as an Xbox pad for games, without SISR: VIIPER, combos, plugging in and out (#213) | [xbox-pad.md](xbox-pad.md) |

## Setup and naming (#85, #92, 2026-10-16)

- In the UI the setting is "Steam Controller Kompatibilität", short "Steam Controller (SISR)".
  Per game there are two modes: "Xbox Controller (SISR)" (SISR runs) and "Steam Input" (SISR paused).
  Steam games start on Steam Input, all others on SISR; `ControllerSupportOverrides` stores only deviations.
  In code the feature keeps its old name (`ControllerSupportService`, `AppSettings.SteamControllerSupport`).
- The switch is offered only when `ControllerSupportService.Setup` is `Ready`: SISR installed, usbip-win2 ≥
  0.9.8.1 installed and loaded, Steam's CEF flag `.cef-enable-remote-debugging` in the Steam folder and the
  "SISR Marker" shortcut (see below).
  Otherwise "SISR installieren" (`InstallAsync`). Once on, the switch stays even if a part breaks later;
  the problem shows with its fix. Without Windows or Steam the row is greyed out.
- Steam reads the CEF flag only when it starts, so `InstallAsync` closes a running Steam first (the
  settings ask), writes the flag itself, installs what is missing and starts Steam again if it closed it.
  It refuses while a Steam game runs: `HKCU\Software\Valve\Steam\RunningAppID` is non-zero, the name comes
  from `…\Steam\Apps\<id>\Name` (the name checked on the owner's PC, 2026-10-16); that writing the flag
  file is all SISR's own "enable CEF" does is inferred. The settings ask both questions in a modal over the
  dialog ("Steam-Spiel läuft", "Steam beenden?") that re-reads the running game every second, because
  nothing else reports a game ending; `LastError` is cleared when the settings open or close.
- A Steam Controller counts as connected (per-game mode in the preview) when SDL sees it (#212,
  [sdl-gamepads.md](sdl-gamepads.md)); until SDL has seen one, while SISR emulates it (`SisrState.Running`).
  `WatchAsync` (every 2 s) moves Running ↔ NoController as the controller is switched on or off.
  Windows' HID list is no use: the wireless dongle shows as a Valve device (`VID_28DE`) even with the
  controller off (owner's PC, 2026-10-16).
