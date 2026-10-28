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
  `GET_PROTOCOL` and CDC class requests to the puck's serial interfaces (Windows' drivers send them to any device of
  their class). Every other control request (GET/SET_REPORT, vendor, unknown) goes to Arcadia and waits up to 1 s.
- Stream protocol, one client at a time: 4-byte header (kind, interface, length LE16), then the payload. `0x01` input
  report (Arcadia → VIIPER), `0x81` interrupt OUT data, `0x82` control request (sequence LE16, setup packet, OUT data),
  `0x02` its reply (sequence, status 0 done or 1 stall, IN data). `HidProxyFrames` in Arcadia, `protocol.go` in the fork.
- The fork's workflow "Arcadia build" runs `go vet` and `go test` (an end-to-end test through the real usbip server
  reads every report exactly once) and cross-builds `viiper.exe`; with `publish` it releases `arcadia-<commit>` with
  the exe, its SHA-256 and the licenses. `src/Arcadia.ControllerSupport/Viiper.targets` pins commit, URL, size and
  hash: the build downloads it into `obj/`, checks it and ships it next to Arcadia.exe; the same pin is compiled into
  `ViiperRelease`. An empty pin downloads nothing, the App then takes SISR's `viiper.exe` and makes no copy.
- Arcadia starts VIIPER with `VIIPER_UPDATE_NOTIFY=none`: it would offer an upstream release every hour, which lacks
  `hidproxy`. The env var, not the flag, as a `viiper.exe` from SISR doesn't know the flag.
- "VIIPER fehlt" → "Installieren" (`ViiperInstallation.InstallAsync`): the pinned file into Arcadia's folder (the
  user's own, no administrator rights), downloaded beside it and moved in only once its hash matched.
- The copy needs exactly the pinned build (`ViiperInstallation.IsPinned`, hash cached per size and write time): with
  another `viiper.exe` `CanCopy` is false and Pan hides nothing.

## Arcadia's side (part C)

- `WindowsPuck` opens the vendor collection (usage page ≥ `0xFF00`) of each HID interface of the real puck by SetupDi,
  leaving out a device on a software host (the copy), with one reader handle (blocking `ReadFile`) and one writer
  handle (features, outputs) each. Windows' buffers (led by the report id, padded to the longest report) are cut to
  the USB length from the report descriptor (`HidReportSizes`).
- `SteamControllerCopyBridge`, free of Windows (tested with a fake puck over a loopback socket): one thread per HID
  interface reads the puck and writes `ToSteam`'s result to VIIPER; a task answers VIIPER's frames in order.
  `SteamControlRequests.Classify` names each control request: SET_REPORT feature/output and GET_REPORT feature/input
  go to the filter; idle, protocol and CDC requests are housekeeping (answered done, never counted, a second guard
  besides the fork); vendor and unknown requests are refused and count as update traffic. A dropped message is
  answered done, as the real puck would.
- Closing stops every call to the puck first (under the lock the calls take), then closes the stream and the puck;
  then VIIPER's bus is removed. A removal that fails throws, so Pan's teardown ends `viiper.exe` before the puck shows.
- Arcadia's SDL ignores the copy: a Steam pad whose HID path leads to a software host (`DeviceTree.InterfaceOnSoftwareHost`)
  is "Kopie des Steam Controllers für Steam" (`GamepadDevices.WhyIgnored`). SDL's Triton driver reads the real puck as
  before and sets lizard mode every 3 s, so Steam's GET_REPORT may read SDL's answer (as before #242).
- Latency (T242-18): every report's delay from the puck read to the VIIPER write is kept (last 8192); `arcadia.log`
  gets n, p50, p99 and max every 10 minutes and when the copy ends. Budget: p99 under 1 ms.

## Open (owner's PC)

- Steam's real update path for the Triton and the routine drops: the check script
  `spikes/242/Record-SteamTraffic-242.ps1` (project files) records the copy's log lines at rest, in Steam's controller
  settings, in a game with the overlay and during an offered update.
- An update offered in Steam's in-game overlay: the game stays in front, so a short gap there skips the question.
  The script asks whether the overlay offers one.
- "Neu installieren" for "HidHide antwortet unerwartet" runs the pinned installer with Windows Installer's reinstall
  (`REINSTALL=ALL REINSTALLMODE=vamus`, `HidHideRelease.RepairArguments`): not yet run on a real PC.
