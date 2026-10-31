# The puck and the Triton by cable as USB devices, and what Steam does with them (spikes #239, #249)

Seen on the owner's PC: new Steam Controller ("Triton") on its wireless puck `28DE:1304`, Windows 11, HidHide 1.5.230,
usbip-win2. Tools: `SteamInputProbe` (mode `describe`; in git at
`f938531:tools/dev/SteamInputProbe`) and `tools/dev/SteamControllerProxy` (the spike's
throwaway proxy). Check scripts and logs: project files `spikes/239/`.

## The puck

- Full speed, `bcdUSB` 2.01, 7 interfaces with an IAD: CDC ACM (0, 1), four controller slots (HID 2–5), the
  dongle (HID 6, vendor `FF00/2`). Strings "Valve Software", "Steam Controller Puck", a 13-character serial.
- Each slot has three collections: mouse (report `0x40`), keyboard (`0x41`), vendor `FF00/1` with inputs `0x42`
  (state, 53 bytes), `0x43`–`0x45`, `0x79`, `0x7B`, outputs `0x80`–`0x89` (report ID = message type) and
  features 1 and 2 (63 bytes; message type is the first byte after the ID).
- The report descriptors can't be read through the hub IOCTL (error 31); the probe merges SDL's reconstructions per
  collection, in collection order. With `bcdUSB` 2.01 Windows asks for a BOS descriptor VIIPER doesn't serve, so the
  copy says 2.00.
- Windows refuses to open the mouse and keyboard collections for reading; only the vendor collection is read.
- Interface 0 is a COM port (`usbser`, "Serielles USB-Gerät"); HidHide doesn't hide it, so Steam can always open the
  real puck's COM port.
- Every restart of the puck (unplug, Pan's or HidHide's restart, a reset) passes a few seconds through its bootloader
  `28DE:1007` "Steam Controller Bootloader" (same serial, CDC ACM, a COM port of its own), then the puck comes back.
  Many such cycles left the firmware unchanged. Arcadia and Pan never touch `1007`.
- Proof of a real update: Steam's `logs\controller.txt` names the firmware at every connect (`FWTimestamp …` per
  dongle and controller); a changed timestamp means it was flashed.
- The Triton by cable: [The Triton by cable](#the-triton-by-cable-spike-249). Steam updates the controller only by
  cable. Pan neither hides nor copies `1302` yet.

## HidHide and Steam

- HidHide hides the puck from Steam completely: hide every `HID\VID_28DE&PID_1304&…` instance (interfaces 2–6, each
  collection), turn cloaking on, restart the puck (exit 3010 of `pnputil /restart-device` means replug). Steam then
  lists no Steam Controller and the desktop layout sends nothing, also after a Steam restart.
- A process on HidHide's allow list keeps reading it: SDL3 saw every input at ~250 Hz.
- In the spike Steam picked a device change up only after a focus change; with Pan's restart of the puck it doesn't
  need one ([pan-hiding.md](pan-hiding.md)).
- Undo: unhide exactly what was added, unregister the app, cloak off only if it was off, restart the puck. If the
  hiding process can die, something else must undo it (hence Pan).

## Steam accepts the copy

The proxy hid the puck, attached a VIIPER device with the puck's descriptors and serial through usbip-win2 and passed
every report both ways. Steam took it as the Steam Controller itself: trackpad mouse, Steam button, controller list
with the usual name and icon, gyro and trackpads in the input test, haptics, and a game's own layout all worked.

- Steam's traffic: `GET_ATTRIBUTES_VALUES`, `GET_STRING_ATTRIBUTE`, `SET_SETTINGS_VALUES` (every few seconds),
  `LOAD_DEFAULT_SETTINGS`, `CLEAR_DIGITAL_MAPPINGS`, `SET_DEFAULT_DIGITAL_MAPPINGS`, `GET_DIGITAL_MAPPINGS`,
  `DONGLE_GET_WIRELESS_STATE`, `SET_AUDIO_MAPPING`, haptics outputs, and `0xA3` on every slot.
- The proxy blocked firmware and bootloader messages (`0x86`, `0xA9`, `0xAF`, `0xB0`, `0xB7`–`0xB9`) and every unknown
  one; Steam sent `0xDC`, `0xE2`, `0xED`, `0xF3` (unknown) and worked anyway. Steam offered no firmware update during
  the run, so blocking one is unverified.
- Latency of the whole hop (real report read to the same report read from the copy), 30 s idle, ~8,000 reports: p50
  ~150 µs, p95 ~290 µs, p99 ~600 µs. Against the 50 ms input budget that is nothing.
- Only one controller on the puck was tried: not a second one (#342), nor the 2015 Steam Controller.

## The Triton by cable (spike #249)

Check script and logs: project files `checks/249/`. The puck stayed plugged in.

- `USB\VID_28DE&PID_1302\<serial>`, full speed, `bcdUSB` 2.01, release `0404`, strings "Valve Software", "Steam
  Controller". One HID interface, so no `&MI_` in any id: `HID\VID_28DE&PID_1302&COL01…COL03` (mouse `0x40`,
  keyboard `0x41`, vendor `FF00/1` with the same reports as a puck slot). No COM port. The report descriptor can't be
  read through the hub either (error 31); hidapi's reconstruction is used.
- HidHide hides it from Steam completely (hide the three `HID\VID_28DE&PID_1302&…` instances, cloak on, restart the
  USB device): Steam lists no controller, buttons do nothing in Steam. Unlike the puck, the trackpad doesn't move the
  pointer while hidden. The restart worked without replug and passed no bootloader `1007`.
- An allow-listed process keeps reading it: SDL3 saw gyro and accelerometer at ~246 Hz.
- Steam takes a usbip copy with these descriptors as the controller by cable: trackpad mouse, Steam button,
  controller list with the usual name and icon, gyro and trackpads in the input test, haptics and a game's own layout.
  Same traffic as on the puck (`GET_ATTRIBUTES_VALUES`, `SET_SETTINGS_VALUES`, digital mappings, `SET_AUDIO_MAPPING`);
  unknown `0xDC`, `0xE2`, `0xED` blocked, Steam worked anyway.
- **Steam offers the copy a firmware update** (it logs the copy with the real controller's `FWTimestamp`). It starts
  with feature `0x90`, which the copy drops; the real controller flashes only after "Controller freigeben", in its
  bootloader `28DE:1005` (#295, [steam-controller-firmware.md](steam-controller-firmware.md)).
- Latency of the hop, ~3 min of play: p50 ~75 µs, p95 ~150 µs, p99 ~230 µs.
- With both `1302` and `1304` hidden, unplugging the cable moved the controller to the puck and Steam saw none.
  A controller connected to the puck stays on it when the cable is plugged in (it charges); it becomes `1302` only
  when plugged in switched off.
