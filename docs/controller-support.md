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

## Setup and naming (#85, #92, 2026-10-16)

- In the UI the setting is "Steam Controller als Xbox 360 Gamepad verwenden", short "Steam Controller (SISR)".
  Per game there are two modes: "Xbox 360 Controller (SISR)" (SISR runs) and "Steam Input" (SISR paused).
  Steam games start on Steam Input, all others on SISR; `ControllerSupportOverrides` stores only deviations.
  In code the feature keeps its old name (`ControllerSupportService`, `AppSettings.SteamControllerSupport`).
- The switch is offered only when `ControllerSupportService.Setup` is `Ready`: SISR installed, usbip-win2 ≥
  0.9.8.1 installed and loaded, and Steam's CEF flag `.cef-enable-remote-debugging` in the Steam folder.
  Otherwise "SISR installieren" (`InstallAsync`). Once on, the switch stays even if a part breaks later;
  the problem shows with its fix. Without Windows or Steam the row is greyed out.
- Steam reads the CEF flag only when it starts, so `InstallAsync` closes a running Steam first (the
  settings ask), writes the flag itself, installs what is missing and starts Steam again if it closed it.
  It refuses while a Steam game runs: `HKCU\Software\Valve\Steam\RunningAppID` is non-zero, the name comes
  from `…\Steam\Apps\<id>\Name`. Inferred from Steam's registry, not yet checked on the owner's PC; that
  writing the flag file is all SISR's own "enable CEF" does is inferred too.
- A Steam Controller counts as connected when Windows lists a HID device with Valve's vendor id `28DE`
  other than `PID_11FF` (Steam's virtual gamepad for other pads). Works while SISR emulates its Xbox 360
  pad. The wireless dongle counts even with the controller off. Inferred from the USB ids, not yet
  checked on the owner's PC; if the device list can't be read, the per-game mode is shown anyway.
- SISR's WebView2 data lands in `%APPDATA%\SISR.exe`, VIIPER's key file in `%APPDATA%\VIIPER`; neither goes
  with SISR's own uninstaller (seen 2026-10-16). `ControllerSupportRemoval.DataDirectories` removes both.
