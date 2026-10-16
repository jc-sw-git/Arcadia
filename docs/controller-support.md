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
  from `…\Steam\Apps\<id>\Name` (the name checked on the owner's PC, 2026-10-16); that writing the flag
  file is all SISR's own "enable CEF" does is inferred. The settings ask both questions in a modal over the
  dialog ("Steam-Spiel läuft", "Steam beenden?") that re-reads the running game every second, because
  nothing else reports a game ending; `LastError` is cleared when the settings open or close.
- A Steam Controller counts as connected (per-game mode in the preview) only while SISR emulates it
  (`SisrState.Running`); `WatchAsync` (every 2 s) moves Running ↔ NoController as the controller is switched on or off.
  Windows' HID list is no use: the wireless dongle shows as a Valve device (`VID_28DE`) even with the
  controller off (owner's PC, 2026-10-16).
- SISR may only run while Steam is ready: `steam` and `steamwebhelper` run and a user is logged in
  (`ActiveProcess\ActiveUser`). Started earlier, SISR shows its own full-screen "Steam is not running"
  window, or reads no marker (it reads the logged-in user's `shortcuts.vdf` once at start) and asks to add
  another one. So `StartSisr` skips without a ready Steam or without the marker.
- SISR's own cleanup on quit opens `steam://forceinputappid/0` (SISR source, `cli/cmd/sisr/sisr.go`),
  which starts a closed Steam. That is why closing Steam (and switching off afterwards) started Steam again
  (owner, 2026-10-16). Unless Steam is ready Arcadia kills SISR instead of quitting it, also while Steam
  shuts down (steam.exe outlives the logout by a few seconds; quitting SISR then restarted Steam); `WatchAsync` does that
  within 2 s of Steam closing, the tick starts SISR again once Steam is ready (not counted as a crash), and
  the settings show "Steam läuft nicht" with "Steam starten".
- Arcadia does SISR's first-run setup itself so SISR never shows a window: `.initial_setup_done` next to
  `SISR.exe` (SISR's setup window shows while it is missing), the CEF flag, and the "SISR Marker" shortcut
  (`SISR.exe`, launch options `--marker`), written into `shortcuts.vdf` while "SISR installieren" has Steam
  closed. A missing marker counts as not set up ("SISR installieren", or "Anlegen" when on, both through
  the same prompts). Switching on starts Steam and waits up to 60 s (`SteamStartTimeout`); if
  Steam doesn't come up the setting is off again and the settings show "Steam startet nicht" with
  "Erneut versuchen". Arcadia still doesn't start Steam on its own when it starts with the option on.
- SISR's WebView2 data lands in `%APPDATA%\SISR.exe`, VIIPER's key file in `%APPDATA%\VIIPER`; neither goes
  with SISR's own uninstaller (seen 2026-10-16). `ControllerSupportRemoval.DataDirectories` removes both.
