# Every other controller through SDL3 (#72)

How Arcadia reads Xbox, PlayStation, Nintendo and third-party pads. Overview and the other files: [README](README.md).
Product rules (by position, extra buttons, what counts): `docs/product/controls.md`.

- **One source:** `SdlGamepadSource` replaces XInput. It reads every pad through SDL's gamepad API, polled every 8 ms
  like XInput before (the repeat of held directions and the mouse mode need a steady clock). `GamepadReading.PadId` is
  SDL's id of the device, new on every connect; `arcadia.log` names it as `Controller #<id>` on connect, disconnect
  and Back + Start, with SDL's name, type and USB ids (no serial number).
- **The official `SDL3.dll`** (3.4.18) comes from SDL's GitHub release; `src/Arcadia.Input/Sdl3.targets` downloads it
  once into `obj/`, checks the zip's SHA-256 and copies the DLL next to Arcadia.exe and every test. P/Invoke is our
  own (`SdlGamepads`), no wrapper. A new version is a new URL and hash there.
- **Hints:** background events on (View held and Back + Start beside a game), HIDAPI on (PlayStation and Nintendo pads
  raw, past Steam Input), `SDL_HIDAPI_IGNORE_DEVICES=0x28de/0x0000`, so SDL never opens a Valve device and writes no
  settings to the Steam Controller (lizard mode, IMU; spike #112) until #212. `SDL_JOYSTICK_ENHANCED_REPORTS=auto`:
  a Bluetooth PlayStation pad switches to the enhanced report mode only when Arcadia rumbles it (the mode can't be
  undone until the pad is switched off and breaks DirectInput for games without SDL; read in SDL's docs, not seen).
- **Which devices** (`GamepadDevices.WhyIgnored`, the one place): Steam's virtual pads `28DE:11FF` (they only mirror a
  physical pad Arcadia reads anyway), every other Valve device and SDL's type `Steam` (the Steam Controller still
  comes as SISR's virtual Xbox 360 pad `045E:028E`), single Joy-Cons, and devices SDL types as wheel, flight stick and
  the like. Ignored devices are logged with the reason.
  SISR's virtual pad looks to SDL exactly like a wired Xbox 360 pad (`045E:028E`, path `XInput#0`; #212 checks, P3), so
  it is read like one: until #212 it is the only way the Steam Controller reaches Arcadia. Once Arcadia reads the
  Steam Controller itself, that pad has to be told apart by the device tree (usbip's root hub) or SISR's API.
- **Fresh look:** `SteamDesktopInputFilter` asks `IGamepadSource.AnyNow` when the last poll saw no controller in use;
  it calls `SDL_UpdateGamepads` under the source's lock and gives up after 2 ms, so the hook stays fast.
- **Rumble:** `SDL_RumbleGamepad` for 150 ms on the pad whose Back + Start switched the mouse mode; SDL stops it.
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
