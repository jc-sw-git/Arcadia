# The copy of the puck on Windows (#242 parts B and C)

How the copy described in [steam-controller-copy.md](steam-controller-copy.md) is made: Arcadia reads the real puck
raw and VIIPER shows Steam a USB device with the puck's descriptors. Measured descriptors and latency of the spike:
[emulated-steam-controller.md](emulated-steam-controller.md).

## VIIPER fork (part B)

- `jc-sw-git/VIIPER`, branch `arcadia`: upstream VIIPER plus the device type `hidproxy` (`device/hidproxy`, docs in
  the fork's `docs/devices/hidproxy.md`, what the fork is: `ARCADIA.md`). Kept small so it rebases onto upstream.
- `hidproxy` takes the device, configuration, string and HID report descriptors and the speed as hex options
  (`PuckDescriptors.DeviceSpecific`, from `Copy/puck-descriptors.json`; only string 3, the serial number, is the
  connected puck's, checked to be 1–32 ASCII letters and digits). bcdUSB 2.01 is served as 2.00 (no BOS descriptor).
- No IN repeat: an interrupt IN endpoint without a new report NAKs instead of resending the last one (upstream repeats
  it every interval, so Steam would read each report many times). A device opts in with `NoInRepeat()`.
- Answered by VIIPER itself, never handed on: the standard requests, HID `SET_IDLE`/`SET_PROTOCOL`/`GET_IDLE`/
  `GET_PROTOCOL` and the CDC line settings (`SET_LINE_CODING`, `GET_LINE_CODING`, `SET_CONTROL_LINE_STATE`; Windows'
  drivers send them to any device of their class). Every other control request (GET/SET_REPORT, other CDC requests
  such as a break, vendor, unknown) goes to Arcadia and waits up to 1 s.
- Stream protocol, one client at a time: 4-byte header (kind, interface, length LE16), then the payload. `0x01` input
  report (Arcadia → VIIPER), `0x81` interrupt OUT data, `0x82` control request (sequence LE16, setup packet, OUT data),
  `0x02` its reply (sequence, status 0 done or 1 refused, IN data). VIIPER's server can't stall: a refused request (or
  no answer within 1 s) reaches the host as an empty, successful data stage. `HidProxyFrames` in Arcadia, `protocol.go`.
- The fork's workflow "Arcadia build" runs `go vet` and `go test` (an end-to-end test through the real usbip server
  reads every report exactly once) and cross-builds `viiper.exe`; with `publish` it releases `arcadia-<commit>` with
  the exe, its SHA-256 and the licenses. `src/Arcadia.ControllerSupport/Viiper.targets` pins commit, URL, size and
  hash: the build downloads it into `obj/`, checks it and ships it next to Arcadia.exe; the same pin is compiled into
  `ViiperRelease`. An empty pin downloads nothing, the App then takes SISR's `viiper.exe` and makes no copy.
- Arcadia starts VIIPER with `VIIPER_UPDATE_NOTIFY=none`: it would offer an upstream release every hour, which lacks
  `hidproxy`. The env var, not the flag, as a `viiper.exe` from SISR doesn't know the flag.
- "VIIPER fehlt" → "Installieren" (`ViiperInstallation.InstallAsync`): the pinned file into Arcadia's folder (the
  user's own, no administrator rights), downloaded beside it and moved in only once its hash matched. A
  `viiper.exe` the Xbox pad still runs from is renamed aside first (Windows won't overwrite a running image) and
  deleted once nothing runs from it.
- The copy needs exactly the pinned build (`ViiperInstallation.IsPinned`, hash cached per size and write time): with
  another `viiper.exe` `CanCopy` is false and Pan hides nothing, and "VIIPER fehlt" offers "Installieren", which
  replaces it. It also needs the server Arcadia started from that file, as the only process listening on the port
  (Windows' TCP table, both loopbacks: `localhost` may reach either): otherwise `AddRawDeviceAsync` refuses (another
  program would get the puck's serial and all of Steam's traffic), so the copy fails and Pan shows the puck.

## Arcadia's side (part C)

- `WindowsPuck` opens the vendor collection (usage page ≥ `0xFF00`) of each HID interface of the real puck by SetupDi,
  only a device the device tree places on a hardware (PCI, ACPI) host: the copy is on a software host, and a device
  whose place can't be read is left out too (fail closed, `PuckDescriptors.IsRealPuck`). With several pucks plugged
  in, only one USB device's collections are copied (`PuckDescriptors.OnePuck`, logged). One reader handle (blocking `ReadFile`) and one writer
  handle (features, outputs) each. Windows' buffers (led by the report id, padded to the longest report) are cut to
  the USB length from the report descriptor (`HidReportSizes`). Closing cancels the reads until none is left, also one
  that began after the first cancel.
- `SteamControllerCopyBridge`, free of Windows (tested with a fake puck over a loopback socket): one thread per HID
  interface reads the puck and writes `ToSteam`'s result to VIIPER; a task answers VIIPER's frames in order.
  `SteamControlRequests.Classify` names each control request: SET_REPORT feature/output and GET_REPORT feature/input
  go to the filter; HID idle and protocol and the CDC line settings are housekeeping (answered done, never counted, a
  second guard besides the fork; the fork never sends them); other CDC, vendor and unknown requests are refused and
  count as update traffic. A dropped message is answered done, as the real puck would. A GET_REPORT of an input
  report never reaches the puck: it gets the last report of that id as `ToSteam` let it through (rest while the copy
  rests), or a refusal.
- Closing stops every call to the puck first (under the lock the calls take), then closes the stream and the puck;
  then VIIPER's bus is removed. A call to the puck that still runs after the puck closed, or a removal that fails,
  throws, so Pan's teardown ends `viiper.exe` before the puck shows (also for a copy lost while running: no new one
  beside it). A write to VIIPER that hangs over 1 s (VIIPER stopped reading) breaks the copy and closes the stream.
- Arcadia's SDL ignores the copy: a Steam pad whose HID path leads to a software host (`DeviceTree.InterfaceOnSoftwareHost`)
  is "Kopie des Steam Controllers für Steam" (`GamepadDevices.WhyIgnored`). This side fails open: a path that can't
  be looked up counts as real, so SDL would read the copy as a second Steam Controller (rare; logged only when the
  device list throws). Deliberate: losing the real controller would be worse than counting it twice. SDL's Triton driver reads the real puck as
  before and sets lizard mode every 3 s, so Steam's GET_REPORT may read SDL's answer (as before #242).
- Latency (T242-18): every report's delay from the puck read to the VIIPER write is kept (last 8192); `arcadia.log`
  gets n, p50, p99 and max every 10 minutes and when the copy ends. Budget: p99 under 1 ms.

## Open (owner's PC)

- Steam's real update path for the Triton and the routine drops: spike #295 with the check script
  `spikes/242/Record-SteamTraffic-242-v2.ps1` (project files; the copy's log lines, Steam's `controller*.txt`, the
  puck's devices and COM ports per phase). Until then the consent modal, the firmware dialog and Pan's hold stay as a
  precaution (owner, 2026-10-28).
- An update offered in Steam's in-game overlay: the game stays in front, so a short gap there skips the question.
  The script asks whether the overlay offers one.
- "Neu installieren" for "HidHide antwortet unerwartet" runs the pinned installer with Windows Installer's reinstall
  (`REINSTALL=ALL REINSTALLMODE=vamus`, `HidHideRelease.RepairArguments`): not yet run on a real PC. Offered and run
  only over the very driver it installs (`HidHideRelease.IsPinnedDriver`), refused while Pan hides the puck or a
  firmware attempt runs (`ControllerSupportService.PanHoldsPuck`; Pan runs the installer before it replaces itself),
  and run with Steam closed (#296): the installer restarts the puck, which then is Steam's and may be mid-update.
- Puck restart and Steam (#296): every step that runs HidHide's installer ("Treiber installieren" and its part rows
  when `InstallsHidHide`: missing or unsupported, no removal pending; "Neu installieren" always) runs only with Steam
  closed. The settings ask "Steam beenden?" (or name a running Steam game); `InstallWithSteamClosedAsync` closes Steam,
  installs, starts it again also after a failure, and waits up to `SteamStartTimeout`. Without HidHide's installer
  Steam is left alone. No flash risk either way (second review of PR #294): a flash runs only on the bootloader `1007`.
- The real puck's COM port (interface 0) stays Steam's while Pan hides: accepted (`pan-risks.md`).
