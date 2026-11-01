# SISR: removed on request or kept (#214)

An older Arcadia installed and started SISR (github.com/Alia5/SISR) to make the Steam Controller an Xbox pad outside
Steam. Arcadia never starts SISR any more and removes nothing by itself. The settings group "SISR", its texts and
dialogs: `docs/product/settings-controller.md`.

## What Arcadia looks for (`SisrInstallation.Find`)

- **Program:** `%LOCALAPPDATA%\SISR` exists (SISR's official install folder).
- **Marker:** a non-Steam shortcut whose exe is exactly `%LOCALAPPDATA%\SISR\SISR.exe` (quotes and case ignored), in
  any account's `userdata\<id>\config\shortcuts.vdf`. Its name doesn't count: SISR's marker is named "SISR Marker"
  with launch options `--marker`, but a user's own shortcut to SISR is the same kind of entry.
- **CEF flag:** `.cef-enable-remote-debugging` in Steam's folder; SISR's setup created it. Alone it doesn't show the
  group (other tools may want it), but "SISR entfernen" deletes it.
- The group shows while the program or a marker is there and `AppSettings.KeepSisr` is false.

## What "SISR entfernen" removes (`SisrInstallation.Remove`)

Each step on its own, so one that fails doesn't stop the rest; a second run picks up what is left. No step needs
administrator rights; usbip-win2 stays (Pan needs it), and so do the per-game modes.

1. Stops the `SISR.exe` and `viiper.exe` from SISR's folder (one elsewhere stays); if an SISR stays, nothing else goes
   (`StopFailedCause`). Then what SISR's own quit would have done: with Steam running, `steam://forceinputappid/0`, so
   Steam drops the layout SISR forced (with Steam closed the URL would start it); with no VIIPER left running, `usbip
   detach` for the pads on VIIPER's address, a safety net.
2. `%LOCALAPPDATA%\SISR`, `%APPDATA%\SISR`, `%APPDATA%\SISR.exe` (SISR's WebView2 data, which SISR's own uninstaller
   leaves), the links `SISR.lnk` on the desktop and `SISR.lnk` / `SISR (No Steam).lnk` in the start menu, and SISR's
   entry in HKCU's uninstall list.
3. The markers in every account, through `SteamShortcuts.RemoveByExecutable` (atomic, `.bak` beside it). Only with
   Steam closed: Steam keeps the shortcuts in memory and writes them back when it exits, so a marker removed while it
   runs comes back. Steam running, the step is skipped with `SteamRunningCause`.
4. The CEF flag.
5. SISR's VIIPER: a standalone `%LOCALAPPDATA%\VIIPER` goes (its uninstaller with `--quiet`, then the folder) unless a
   `viiper.exe` from there runs; `%APPDATA%\VIIPER` (VIIPER's key file) goes only when no VIIPER is left and none runs.

## Steam closed for the marker (`ControllerSupportService.RemoveSisrAsync`)

- With a marker and Steam running, the settings ask "Steam beenden?" first; a running Steam game stops it before
  anything changes ("Steam-Spiel läuft"), and so does a Steam that won't close. SISR is stopped only after Steam closed:
  a game may be using SISR's pad. Either cause shows in the row ("SISR wurde nicht entfernt"), like what a removal
  left ("Nicht alles ließ sich entfernen"); the group stays while there is a cause.
- Arcadia closes Steam, removes, and starts Steam again also after a failure (`WithSteamClosedAsync`, shared with the
  driver fixes that restart the puck). If Arcadia crashes in between, Steam stays closed; the user starts it.
- The marker stays gone after a removal with Steam closed, a Steam start and another exit (verified).

## At start and beside Pan

- Once at start, `StopSisrStartedByOldArcadia` stops only an SISR whose command line shows the older Arcadia's start
  (`--api.listen-address localhost:47640` and `--log.file` in one of Arcadia's log folders): the old Arcadia kept SISR
  running across its update, so it would take the controller beside Pan. An SISR the user started stays. A
  `viiper.exe` from SISR's folder goes too while no SISR from there runs, so it can't hold Arcadia's VIIPER's port.
- While the settings are open and Pan is on, a running SISR shows the warning row "SISR läuft" under Pan
  (`EmulationParts.SisrRunning`), also with the group hidden; settings-only, never in the header.
- The old settings `SteamControllerSupport` and `LastSisrUpdateCheck` are ignored on load and dropped at the next save;
  `ControllerSupportOverrides` stays.
- The uninstall still asks about SISR with the other parts ([pan-setup.md](pan-setup.md)).
