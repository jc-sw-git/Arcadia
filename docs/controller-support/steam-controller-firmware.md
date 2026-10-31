# Steam Controller firmware updates while Arcadia emulates (#295)

## How Steam updates the Triton and its puck (owner's PC, spike #295)

- Steam asks over HID: feature `0x90` (named `REBOOT_INTO_BOOTLOADER` in `SteamControllerMessages`, not an SDL name)
  makes the device restart into its bootloader, a USB device with a COM port and the device's own serial:
  `28DE:1005` the controller by cable, `28DE:1007` the puck. The flash itself is
  `Steam\bin\hardwareupdater\hardwareupdater.exe` (PyInstaller; `--check-for-updates`, `--show-all-devices`,
  `--update-by-serial <serial>`; tracebacks land in `steamui_system.txt`). By cable the bootloader was there ~21 s,
  then `1302` came back. The updater names the devices `Triton_USB` (cable), `Triton_ESB` (through the puck) and
  `Proteus_USB` (the puck).
- The updater is not on HidHide's allow list, so it sees only Arcadia's copies. On the puck's copy it crashes
  (`IndexError` in `get_serial_triton` → `get_feature_report`); on the cable copy the copy drops `0x90` (5 tries),
  Steam reports an error and keeps offering the update. **While a copy runs no update reaches the hardware.**
- Steam + Y sends `0x9F` (`TURN_OFF_CONTROLLER`): on the allow list, or the controller doesn't switch off.

## What Arcadia does

- No question before a hide: Arcadia takes Steam Controller and puck over whether Steam runs or not.
- A firmware message (`SteamMessageKind.Firmware`: `0x90`, `0xB7`–`0xB9`) raises `UpdateTraffic`; other dropped types
  don't. `FirmwareAttemptWatch` raises one notice per attempt (messages under 30 s apart are one). With no library
  game in front Arcadia comes to the front with "Steam will die Firmware aktualisieren" (`FirmwareAttemptViewModel`,
  texts in `docs/product/controls.md`): "Controller freigeben" and "Arcadia beenden" (held 1 s) act at once,
  "Schließen" or [B] keeps the takeover. With a game in front only `SteamControllerForGames.FirmwareInGameText` shows
  over the game.
- "Controller freigeben" (`PanEmulation.ReleaseForFirmwareAsync`) ends the emulation, so Pan shows the puck, and keeps
  Steam Controller and puck out (`PanEmulationProblem.Excluded`, not a problem) until Pan is switched off and on or
  Arcadia restarts. Every other controller stays taken over (#238 D2).
- Pan never hides and never restarts the puck while a bootloader is connected and for `Guardian.BootloaderGrace`
  (5 s) after (`HidingList.IsBootloader`, `ISteamControllerDevices.BootloaderPresent`): a hide or restart then could
  cut the flash off. The hide is answered `BootloaderPresent`, not counted as a failure; Arcadia tries again after 10 s
  and, with no other controller taken over, shows "Steam spielt ein Firmware-Update auf"
  (`PanEmulationProblem.FirmwareUpdate`). A restart stays
  pending (`PuckRestartPending`) for the next show, hide or retry. A device list that can't be read touches
  nothing either but is a failure (`HidingFailed`). A `1007` alone within `Guardian.OwnRestartWindow` (15 s) of Pan's
  own puck restart is the few seconds every restart passes through it
  ([emulated-steam-controller.md](emulated-steam-controller.md)), not a flash; a `1005` always is.
- A puck that arrives (after its flash, a replug) is hidden only while the puck itself is hidden for Arcadia, not while
  Pan hides only other controllers: after "Controller freigeben" it stays Steam's.
- "Controller freigeben" with the puck hidden ends the whole emulation; the other controllers are taken over again a
  tick later (a short gap, accepted: the notice comes only with no game in front).
- Journals of a Pan before protocol 3 may carry `FirmwareHold`; it is ignored.

## Open

- The puck's own update (bootloader `1007`) was not flashed in #295; the rule covers it by its id.
- Whether Steam's in-game overlay offers an update (the notice then stays a toast over the game).
