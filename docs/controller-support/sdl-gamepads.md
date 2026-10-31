# Every controller through SDL3 (#72, #212)

How Arcadia reads Xbox, PlayStation, Nintendo, third-party pads and the Steam Controller. Product rules (by position,
extra buttons, what counts): `docs/product/controls.md`.

- **One source:** `SdlGamepadSource` reads every pad through SDL's gamepad API, polled every 8 ms (the repeat of held
  directions and the mouse mode need a steady clock). `GamepadReading.PadId` is SDL's id of the device, new on every
  connect; `arcadia.log` names it as `Controller #<id>` on connect, disconnect and Back + Start, with SDL's name, type
  and USB ids (no serial number).
- **The official `SDL3.dll`** (3.4.18) comes from SDL's GitHub release; `src/Arcadia.Input/Sdl3.targets` downloads it
  once into `obj/`, checks the zip's SHA-256 and copies the DLL next to Arcadia.exe and every test. P/Invoke is our
  own (`SdlGamepads`), no wrapper. A new version is a new URL and hash there.
- **Hints:** background events on (View held and Back + Start beside a game), HIDAPI on (PlayStation and Nintendo pads
  raw, past Steam Input), `SDL_JOYSTICK_HIDAPI_STEAM=1` (the Steam Controller raw beside Steam; while Arcadia holds it
  open SDL switches its lizard mode off every 3 s) and `SDL_JOYSTICK_HIDAPI_STEAMDECK=0` (the Deck's controls stay
  Steam's). `SDL_JOYSTICK_ENHANCED_REPORTS=auto`: a Bluetooth PlayStation pad switches to the enhanced report mode only
  when Arcadia rumbles it (the mode can't be undone until the pad is switched off and breaks DirectInput for games
  without SDL; read in SDL's docs, not seen).
- **Which devices** (`GamepadDevices.WhyIgnored`, the one place): Steam's virtual pads `28DE:11FF` (they only mirror a
  physical pad Arcadia reads anyway), every Valve device that isn't SDL's type `Steam` (Steam Deck), emulated Xbox pads
  (below), the copy of the Steam Controller for Steam (a Steam pad whose HID path leads to a software host,
  `DeviceTree.InterfaceOnSoftwareHost`; fails open: a path that can't be looked up counts as real, since losing the real
  controller would be worse than counting it twice), single Joy-Cons, and devices SDL types as wheel, flight stick and
  the like. Ignored devices are logged with the reason.
- **Emulated Xbox pads** (SISR's, VIIPER's, ViGEm's) look to SDL exactly like a wired Xbox 360 pad (`045E:028E`, path
  `XInput#n`), so `DeviceTreeEmulatedPads` asks Windows: `XusbDevices` lists the XInput (XUSB) devices, climbs from
  each over its USB levels to the host controller (root-enumerated `ROOT\…` = software, as usbip-win2's; PCI/ACPI =
  hardware) and reads its XInput slot from the player LED (`IOCTL_XUSB_GET_LED_STATE`, undocumented, not yet seen
  working). Without readable slots a pad counts as emulated only if every XInput device without a slot is emulated;
  when unclear it is read and the log says "unklar, ob XInput#n virtuell ist".
- **Steam Controller:** buttons as any pad; Steam button (`guide`), "…" (`misc1`), back grips (`paddle1`–`4`),
  trackpad clicks (left `touchpad`, right `misc2`) and the capacitive touches (`misc3`–`6`, not even read) do nothing.
  Both trackpads (touch, position, click) ride on `GamepadReading`, gyro and accelerometer only after
  `SdlGamepadSource.UseMotionSensors(true)`; no consumer yet (#111). A touched trackpad counts as use in
  `ControllerActivity`, and for 1 s after the finger leaves the pad, so the desktop filter drops Steam's trackpad
  mouse, which glides on after release (150 ms let it jump). "Connected" for the per-game mode comes from SDL
  (`SteamControllerPresence`). Started through Steam, Steam's hooks hide the controller from Arcadia; only logged.
  SDL's Triton driver opens a puck as soon as it appears and keeps it open, also without a connected controller, so
  nothing in `arcadia.log` says it holds it ([pan-puck-restart.md](pan-puck-restart.md)).
- **Fresh look:** `SteamDesktopInputFilter` asks `IGamepadSource.AnyNow` when the last poll saw no controller in use;
  it calls `SDL_UpdateGamepads` under the source's lock and gives up after 2 ms, so the hook stays fast.
- **Rumble:** `SDL_RumbleGamepad` for 150 ms on the pad whose Back + Start switched the mouse mode; SDL stops it.
  The Steam Controller's driver supports it (SDL resends it every 40 ms while on).
- **Errors:** a missing or broken `SDL3.dll` (`DllNotFoundException`, `EntryPointNotFoundException`, `SDL_Init`
  failing) or a native error later ends only the controller input: logged with stack trace, a toast
  (`ControllerInput.UnavailableText`), keyboard and mouse keep working.
- **Seen with other pads** (raw SDL probe beside a running Steam): an 8BitDo Ultimate 2 by cable or dongle reads as
  `"XInput Controller #1"` `2DC8:310B`, path `XInput#0`, with Steam-Eingabe for Xbox pads off and on and with Steam
  closed; Steam gave the probe no virtual pad and no desktop-layout input. A DualSense (`054C:0CE6`, HID) reads in
  front and in the background, touchpad included, without any mouse or keys from Steam. Windows itself sends
  `VK_GAMEPAD_*` keys (`0xC3`–`0xDA`) as simulated input to the window in front for any XInput pad (VIIPER's too), also
  with Steam closed. The owner's Switch Pro (`057E:2009`) vanished after ~10 s each time and flooded `VK_GAMEPAD_*`
  keys; presumably the pad is broken.
