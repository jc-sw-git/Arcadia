# Every controller through SDL3 (#72, #212)

How Arcadia reads Xbox, PlayStation, Nintendo and third-party pads, and since #212 the Steam Controller. Overview and the other files: [README](README.md).
Product rules (by position, extra buttons, what counts): `docs/product/controls.md`.

- **One source:** `SdlGamepadSource` replaces XInput. It reads every pad through SDL's gamepad API, polled every 8 ms
  like XInput before (the repeat of held directions and the mouse mode need a steady clock). `GamepadReading.PadId` is
  SDL's id of the device, new on every connect; `arcadia.log` names it as `Controller #<id>` on connect, disconnect
  and Back + Start, with SDL's name, type and USB ids (no serial number).
- **The official `SDL3.dll`** (3.4.18) comes from SDL's GitHub release; `src/Arcadia.Input/Sdl3.targets` downloads it
  once into `obj/`, checks the zip's SHA-256 and copies the DLL next to Arcadia.exe and every test. P/Invoke is our
  own (`SdlGamepads`), no wrapper. A new version is a new URL and hash there.
- **Hints:** background events on (View held and Back + Start beside a game), HIDAPI on (PlayStation and Nintendo pads
  raw, past Steam Input), `SDL_JOYSTICK_HIDAPI_STEAM=1` (the Steam Controller raw beside Steam and SISR; while Arcadia
  holds it open SDL switches its lizard mode off every 3 s, P5) and `SDL_JOYSTICK_HIDAPI_STEAMDECK=0` (the Deck's
  controls stay Steam's). `SDL_JOYSTICK_ENHANCED_REPORTS=auto`:
  a Bluetooth PlayStation pad switches to the enhanced report mode only when Arcadia rumbles it (the mode can't be
  undone until the pad is switched off and breaks DirectInput for games without SDL; read in SDL's docs, not seen).
- **Which devices** (`GamepadDevices.WhyIgnored`, the one place): Steam's virtual pads `28DE:11FF` (they only mirror a
  physical pad Arcadia reads anyway), every Valve device that isn't SDL's type `Steam` (Steam Deck), emulated Xbox pads
  (below), single Joy-Cons, and devices SDL types as wheel, flight stick and the like. Ignored devices are logged with
  the reason. SDL's type `Steam` (both Steam Controllers, Hori's) is read.
- **SISR's virtual pad** (#212): to SDL exactly a wired Xbox 360 pad (`045E:028E`, path `XInput#0`, P3), so
  `DeviceTreeEmulatedPads` asks Windows: `XusbDevices` lists the XInput (XUSB) devices, climbs from each over its USB
  levels to the host controller (root-enumerated `ROOT\…` = software, as usbip-win2's; PCI/ACPI = hardware) and reads
  its XInput slot from the player LED (`IOCTL_XUSB_GET_LED_STATE`, undocumented, not yet seen working). SDL's
  `XInput#n` is slot n. Without readable slots a pad counts as emulated only if every XInput device without a slot is
  emulated; when unclear it is read and the log says "unklar, ob XInput#n virtuell ist". Also catches #213's VIIPER
  pad and ViGEm pads (their bus is root-enumerated too).
- **Steam Controller** (#212): buttons as any pad; Steam button (`guide`), "…" (`misc1`), back grips
  (`paddle1`–`4`), trackpad clicks (left `touchpad`, right `misc2` in SDL 3.4's mapping) and the capacitive touches
  (`misc3`–`6`, not even read) do nothing. Both trackpads (touch, position, click) ride on `GamepadReading`, gyro and
  accelerometer only after `SdlGamepadSource.UseMotionSensors(true)`; no consumer yet (#111). A touched trackpad
  counts as use in `ControllerActivity`, so the desktop filter drops Steam's trackpad mouse, and for 1 s after the
  finger leaves the pad: Steam's trackpad pointer glides on (owner's PC, 2026-10-25; 150 ms let it jump on release). "Connected" for the
  per-game mode comes from SDL (`SteamControllerPresence`); until SDL has seen one, what SISR saw counts.
  Started through Steam, Steam's hooks hide the controller from Arcadia; only logged.
- **Fresh look:** `SteamDesktopInputFilter` asks `IGamepadSource.AnyNow` when the last poll saw no controller in use;
  it calls `SDL_UpdateGamepads` under the source's lock and gives up after 2 ms, so the hook stays fast.
- **Rumble:** `SDL_RumbleGamepad` for 150 ms on the pad whose Back + Start switched the mouse mode; SDL stops it.
  The Steam Controller's driver supports it (SDL resends it every 40 ms while on).
- **Errors:** a missing or broken `SDL3.dll` (`DllNotFoundException`, `EntryPointNotFoundException`, `SDL_Init`
  failing) or a native error later ends only the controller input: logged with stack trace, a toast
  (`ControllerInput.UnavailableText`), keyboard and mouse keep working.
- **Start checks** (owner's PC, 2026-10-25, raw SDL probe, Steam running, SISR not; only the 8BitDo Ultimate 2 by
  cable or dongle, no Bluetooth adapter at hand): SDL read it as `"XInput Controller #1"` `2DC8:310B`, path `XInput#0`,
  with Steam-Eingabe for Xbox pads off and on and with Steam closed; Steam gave no virtual pad (`28DE:11FF`) to the
  probe and no mouse or keys from its desktop layout. Windows itself sends `VK_GAMEPAD_*` keys (`0xC3`–`0xDA`) as
  simulated input to the window in front for an XInput pad, also with Steam closed (they switched tabs in Windows
  Terminal). SDL also listed the Steam Controller (`28DE:1304`, HID), which Arcadia ignores.
  Second run, by cable: the DualSense (`054C:0CE6`, HID) was read with Steam-Eingabe off and on, in front and in the
  background, touchpad included, without any mouse or keys from Steam. The owner's Switch Pro (`057E:2009`, HID)
  connected, SDL got only the real presses, but it vanished after about 10 s both times, and while connected Windows
  sent about 200 `VK_GAMEPAD_*` keys a second (the "error codes" in the terminal); the owner suspects the pad is broken.
