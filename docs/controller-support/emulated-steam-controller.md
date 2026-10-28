# Emulated Steam Controller for Steam (spike #239)

Verified on the owner's PC, 2026-10-26: new Steam Controller ("Triton") on its wireless puck `28DE:1304`,
Windows 11, HidHide 1.5.230, usbip-win2 ≥ 0.9.8.1. Tools: `tools/dev/SteamInputProbe` (mode `describe`) and
`tools/dev/SteamControllerProxy` (throwaway proxy, built inside a VIIPER checkout). Check scripts and logs: project
files `spikes/239/`.

## Hiding the real puck (check 1)

- HidHide hides the puck from Steam completely: hide every `HID\VID_28DE&PID_1304&…` instance (interfaces 2–6,
  each collection), turn cloaking on, restart the puck (`pnputil /restart-device` on the `USB\…` instance; exit 3010
  means replug). Steam then lists no Steam Controller and the desktop layout sends nothing, also after a Steam
  restart.
- A process on HidHide's allow list keeps reading it: SDL3 saw every input at ~250 Hz.
- Steam picks a device change up only after a focus change (any window, not necessarily Steam's). Hiding and
  showing again both needed one.
- Undo: unhide exactly what was added, unregister the app, cloak off only if it was off, restart the puck. The
  check scripts compare `--dev-list`/`--app-list` before and after. If the hiding process can die, something
  else must undo it (the proxy watched its parent and ran the restore script).

## The puck as USB device

- Full speed, `bcdUSB` 2.01, 7 interfaces with an IAD: CDC ACM (0, 1), four controller slots (HID 2–5), the
  dongle (HID 6, vendor `FF00/2`). Strings "Valve Software", "Steam Controller Puck", a 13-character serial.
- Each slot has three collections: mouse (report `0x40`), keyboard (`0x41`), vendor `FF00/1` with inputs `0x42`
  (state, 53 bytes), `0x43`–`0x45`, `0x79`, `0x7B`, outputs `0x80`–`0x89` (report ID = message type) and
  features 1 and 2 (63 bytes; message type is the first byte after the ID).
- The report descriptors can't be read through the hub IOCTL (error 31); the probe merges SDL's
  reconstructions per collection, in collection order. With `bcdUSB` 2.01 Windows asks for a BOS descriptor VIIPER
  doesn't serve, so the copy says 2.00.
- Windows refuses to open the mouse and keyboard collections for reading; only the vendor collection is read.
- Interface 0 is a COM port (`usbser`, "Serielles USB-Gerät"); HidHide doesn't hide it, so Steam can always open the
  real puck's COM port (owner's PC, 2026-10-28).
- Every restart of the puck (unplug, Pan's or HidHide's restart, a reset) passes a few seconds through its bootloader
  `28DE:1007` "Steam Controller Bootloader" (same serial, CDC ACM, a COM port of its own), then the puck comes back.
  Six such cycles between 2026-10-25 and -27 left the firmware unchanged. Arcadia and Pan never touch `1007`.
- Proof of a real update: Steam's `logs\controller.txt` names the firmware at every connect (dongle HWID 71
  `FWTimestamp 0x6AC6869B`, controller HWID 73 `0x6ACFF323` on 2026-10-28); a changed timestamp means it was flashed.
- The Triton by cable is `28DE:1302`, one HID interface (mouse, keyboard, vendor collection), no COM port. Steam
  updates the controller only by cable (owner). Pan neither hides nor copies `1302`.

## Steam accepts the copy (check 2)

The proxy hid the puck, attached a VIIPER device with the puck's descriptors and serial through usbip-win2 and
passed every report both ways (input reports up, output and feature reports down to the real puck). Steam
took it as the Steam Controller itself: trackpad mouse, Steam button, controller list with the usual name and
icon, gyro and trackpads in the input test, haptics, and a game's own layout all worked as usual.

- Steam's traffic: `GET_ATTRIBUTES_VALUES`, `GET_STRING_ATTRIBUTE`, `SET_SETTINGS_VALUES` (every few seconds),
  `LOAD_DEFAULT_SETTINGS`, `CLEAR_DIGITAL_MAPPINGS`, `SET_DEFAULT_DIGITAL_MAPPINGS`, `GET_DIGITAL_MAPPINGS`,
  `DONGLE_GET_WIRELESS_STATE`, `SET_AUDIO_MAPPING`, haptics outputs, and `0xA3` on every slot.
- The proxy blocked firmware and bootloader messages (`0x86`, `0xA9`, `0xAF`, `0xB0`, `0xB7`–`0xB9`) and every
  unknown one; Steam sent `0xDC`, `0xE2`, `0xED`, `0xF3` (unknown) and worked anyway.
- Steam offered no firmware update during the run, so blocking one is unverified.

## Latency (check 3)

- The proxy's own delay, real report read to usbip queue: p50 ~20 µs, p95 ~50 µs, max < 1 ms (2,650 reports per
  10 s on one slot).
- The whole hop, real report read to the same report read from the copy (proxy, usbip-win2, Windows' HID stack
  once more), 30 s idle, ~8,000 reports: p50 ~150 µs, p95 ~290 µs, p99 ~600 µs. The maxima of several seconds are
  an artefact of matching reports by content (an idle report repeats once its sequence byte wraps), not real.
  Against the 50 ms input budget that is nothing; no frame of a game notices it.

## Open for an implementation

- Firmware updates: block or pass through on purpose (e.g. hiding off while Steam updates); unverified.
- The unknown messages `0xDC`, `0xE2`, `0xED`, `0xF3`: what they do, and whether blocking them costs anything.
- Only the Triton puck with one controller was tried: not a second controller, nor the 2015 Steam Controller. The
  Triton by cable was only listed, not used through Arcadia.
- VIIPER needs a device type for this (Go, in VIIPER's server) and a fix so it does not resend the last input
  report every interval (`tools/dev/SteamControllerProxy/viiper-no-in-repeat.patch`).
