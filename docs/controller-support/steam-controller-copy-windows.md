# The copy of the puck on Windows (#242)

Arcadia reads the real puck raw and VIIPER shows Steam a USB device with the puck's descriptors
([steam-controller-copy.md](steam-controller-copy.md)). Measured descriptors and latency: [emulated-steam-controller.md](emulated-steam-controller.md).

## VIIPER fork

- `jc-sw-git/VIIPER`, branch `arcadia`: upstream VIIPER plus the device type `hidproxy` (`device/hidproxy`, its docs
  in the fork's `docs/devices/hidproxy.md`, what the fork is: `ARCADIA.md`). Kept small so it rebases onto upstream.
- `hidproxy` takes the device, configuration, string and HID report descriptors and the speed as hex options
  (`PuckDescriptors.DeviceSpecific`, from `Copy/puck-descriptors.json`; only string 3, the serial number, is the
  connected puck's, checked to be 1–32 ASCII letters and digits). bcdUSB 2.01 is served as 2.00 (no BOS descriptor).
- No IN repeat: an interrupt IN endpoint without a new report NAKs instead of resending the last one (upstream repeats
  it every interval, so Steam would read each report many times).
- Answered by VIIPER itself: the standard requests, HID `SET_IDLE`/`SET_PROTOCOL`/`GET_IDLE`/`GET_PROTOCOL` and the
  CDC line settings. Every other control request (GET/SET_REPORT, other CDC requests, vendor, unknown) goes to Arcadia
  and waits up to 1 s. VIIPER's server can't stall: a refused request (or no answer within 1 s) reaches the host as an
  empty, successful data stage. Stream protocol in `HidProxyFrames` (Arcadia) and the fork's `protocol.go`.
- The fork's workflow "Arcadia build" tests and cross-builds `viiper.exe`; with `publish` it releases
  `arcadia-<commit>` with the exe, its SHA-256 and the licenses. `src/Arcadia.ControllerSupport/Viiper.targets` pins
  commit, URL, size and hash: the build downloads it into `obj/`, checks it and ships it next to Arcadia.exe; the same
  pin is compiled into `ViiperRelease`. An empty pin means no VIIPER at all (#304).
- Arcadia starts VIIPER with `VIIPER_UPDATE_NOTIFY=none`: it would offer an upstream release every hour, which lacks
  `hidproxy`.
- "VIIPER fehlt" → "Installieren" (`ViiperInstallation.InstallAsync`): the pinned file into Arcadia's folder (the
  user's own, no administrator rights), downloaded beside it and moved in only once its hash matched. A `viiper.exe`
  the Xbox pad still runs from is renamed aside first (Windows won't overwrite a running image) and deleted once
  nothing runs from it.
- The copy needs exactly the pinned build (`ViiperInstallation.IsPinned`, hash cached per size and write time): with
  another `viiper.exe` `CanCopy` is false and Pan hides nothing. It also needs the server Arcadia started from that
  file (#304: loopback, own port and key): another process would get the puck's serial and all of Steam's traffic.

## Arcadia's side

- `WindowsPuck` opens the vendor collection (usage page ≥ `0xFF00`) of each HID interface of the real puck by SetupDi,
  only a device the device tree places on a hardware (PCI, ACPI) host: the copy is on a software host, and a device
  whose place can't be read is left out too (fail closed, `PuckDescriptors.IsRealPuck`). With several pucks plugged
  in, only one USB device's collections are copied (`PuckDescriptors.OnePuck`, logged). One reader handle (blocking
  `ReadFile`) and one writer handle (features, outputs) each. Windows' buffers (led by the report id, padded to the
  longest report) are cut to the USB length from the report descriptor (`HidReportSizes`). Closing cancels the reads
  until none is left, also one that began after the first cancel.
- `SteamControllerCopyBridge`, free of Windows (tested with a fake puck over a loopback socket): one thread per HID
  interface reads the puck and writes `ToSteam`'s result to VIIPER; a task answers VIIPER's frames in order.
  `SteamControlRequests.Classify` names each control request: SET_REPORT feature/output and GET_REPORT feature/input
  go to the filter; HID idle and protocol and the CDC line settings are housekeeping (a second guard besides the
  fork); other CDC, vendor and unknown requests are refused and count as update traffic. A dropped message is
  answered done, as the real puck would. A GET_REPORT of an input report never reaches the puck: it gets the last
  report of that id as `ToSteam` let it through (rest while the copy rests), or a refusal.
- Steam's requests to empty slots (#314): every few seconds Steam sends `GET_STRING_ATTRIBUTE` (`0xAE`, report 1) and
  the GET_REPORT for its answer to a slot without a controller, and `SET_SETTINGS_VALUES` to every slot when the
  controller connects. The real puck refuses them (Windows error 31, `ERROR_GEN_FAILURE`), with or without the copy;
  Steam gets an empty answer where the real puck stalls and works either way. The bridge logs each kind of refusal
  once (call, interface, report id, message, error) and the counts when the copy ends. At rest Steam sends the copy
  the same as the controller by cable: `SET_SETTINGS_VALUES` every 3.0 s, nothing else (USB capture, 2026-10-29).
- Closing stops every call to the puck first (under the lock the calls take), then closes the stream and the puck;
  then VIIPER's bus is removed. A call to the puck that still runs after the puck closed, or a removal that fails,
  throws, so Pan's teardown ends `viiper.exe` before the puck shows. A write to VIIPER that hangs over 1 s (VIIPER
  stopped reading) breaks the copy and closes the stream.
- SDL's Triton driver reads the real puck as before and sets lizard mode every 3 s, so Steam's GET_REPORT may read
  SDL's answer. Arcadia's SDL ignores the copy ([sdl-gamepads.md](sdl-gamepads.md)).
- Latency (T242-18): every report's delay from the puck read to the VIIPER write is kept (last 8192); `arcadia.log`
  gets n, p50, p99 and max every 10 minutes and when the copy ends. Budget: p99 under 1 ms.

## HidHide's installer and Steam (#296)

Every step that runs HidHide's installer ("Treiber installieren" and its part rows when HidHide is missing or
unsupported; "Neu installieren" for "HidHide antwortet unerwartet", which runs the pinned installer with Windows
Installer's reinstall, `HidHideRelease.RepairArguments`, not yet run on a real PC) runs only with Steam closed: the
installer restarts the puck, which then is Steam's and may be mid-update. The settings ask "Steam beenden?" (or name a
running Steam game); `InstallWithSteamClosedAsync` closes Steam, installs, starts it again also after a failure.
"Neu installieren" is offered only over the very driver it installs (`HidHideRelease.IsPinnedDriver`) and refused
while Pan hides the puck or a firmware attempt runs (`ControllerSupportService.PanHoldsPuck`). No flash risk either
way: a flash runs only on the bootloader `1007`.

## Open (owner's PC)

- Steam's real update path for the Triton and the routine drops: spike #295. Until then the consent modal, the
  firmware dialog and Pan's hold stay as a precaution.
- Whether Steam's in-game overlay offers a firmware update (then the short-gap rule skips the question).
- The real puck's COM port (interface 0) stays Steam's while Pan hides: accepted ([pan-risks.md](pan-risks.md)).
