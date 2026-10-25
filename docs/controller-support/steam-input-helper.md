# Steam Input without SISR (spike #112)

Observed on the owner's PC on 2026-10-25 (new Steam Controller "Triton", puck `28DE:1304`, SISR 0.6.2 not running,
SISR's marker and Steam's CEF flag present) with the probe `tools/dev/SteamInputProbe` and its runner
`tools/dev/Run-SteamInputProbe.ps1`. Overview and the other files: [README](README.md).

Decision (owner, 2026-10-25): replace SISR. Arcadia reads the Steam Controller itself with SDL3 (#212), a small
helper serves games outside Steam (#213), setup and moving off SISR in #214. Until then SISR stays.

## What was seen

- **SISR's technique works from our own process.** The probe sets SISR's variables (`SteamClientLaunch=0`,
  `SteamAppId=0`, `SteamGameId`/`SteamOverlayGameId` = `(marker appid << 32) | 0x02000000`, `SteamPath`,
  `SteamVirtualGamepadInfo`), loads `<Steam>\GameOverlayRenderer64.dll` and opens
  `steam://forceinputappid/<marker appid>`. 10 ms after the load, XInput slot 0 of the probe showed Steam's virtual
  pad, without any window of its own (no `--window` needed), and with the marker's layout: A/B/X/Y, shoulders,
  triggers, sticks and Back + Start (`0x0030`) arrived. Steam doesn't check that the process is the shortcut's exe:
  the probe borrowed SISR's marker id.
- **In the background too:** with Explorer or Steam's window in front the pad kept delivering, about 64 XInput
  packets a second (Steam's update rate for the virtual pad).
- **No "in game":** Steam's `RunningAppID` stayed 0 and the friends list showed nothing (owner).
- **No desktop layout while forced:** with the layout forced, no simulated mouse or keys arrived.
- **Without the overlay** (control) the process got no XInput pad, and Steam's desktop layout sent Enter, Esc,
  Space, Backspace, arrow keys and trackpad mouse as **simulated input** (`LLKHF_INJECTED`/`LLMHF_INJECTED` set,
  about 220 mouse events a second). That confirms what `SteamDesktopInputFilter` assumes ([desktop-layout.md](desktop-layout.md)).
  The trackpad mouse of the *forced* layout arrived without the injected flag (steps 2 and 5, lining up with the
  touches the raw probe saw), so the filter would not catch it.
- **Releasing is not enough:** after `steam://forceinputappid/0` with the same window in front, Steam kept the
  marker's layout for the whole 14 s that followed (pad data on, no desktop input), as it does after SISR stops
  ([README](README.md): focus bounce). A raw SDL probe started next, in the same console, also saw no desktop keys.
- **Raw SDL3 beside Steam works.** SDL 3.4.18 (`SDL_JOYSTICK_HIDAPI=1`, `SDL_JOYSTICK_HIDAPI_STEAM=1`,
  background events on) opened the controller as `"Steam Controller" 28DE:1304` (HID `MI_02`, Steam handle 0) with
  2 touchpads, gyro and accelerometer, while Steam ran: gyro and accelerometer at about 250 Hz, each trackpad about
  245 events a second, stick/trigger axes 800–900 a second, every button (A/B/X/Y, shoulders, and the capacitive
  touches as `misc3`–`misc6`). Run at the same time as the overlay probe, both got every press; SDL's settings
  writes (lizard mode off every 3 s, IMU on) didn't disturb Steam's virtual pad.
- **VIIPER from C# without `SISR.exe`:** the probe started SISR's `viiper.exe server --no-tray`, which answered after
  535 ms, created an Xbox 360 controller (`045e:028e`) through VIIPER's MIT client `Viiper.Client` 0.9.0 (TCP API
  `localhost:3242`) and auto-attached it through usbip-win2. A separate process saw it in XInput slot 0 with every
  forwarded button and the D-pad. The overlay probe itself never saw that pad (no feedback loop), and Steam didn't
  take it under Steam Input. Removing the device and the bus through the API detached it.
- While a VIIPER (or any) Xbox pad is connected, Windows sends `VK_GAMEPAD_*` keys (`0xC3`–`0xDA`) as simulated key
  input to the foreground window; seen with Windows Terminal in front.

## What was read, not observed

- SISR's gyro and touchpad passthrough come from the **physical** controller, read by SDL in SISR's own process
  (`RealGamepad`, `event/handler/sensorupdated`), not from Steam's virtual pad. That is why SISR removes Steam's
  hooks on `hid.dll` (`cli/cmd/sisr/unhook.go`): the overlay hides the real device from the "game" process. A helper
  that only forwards an Xbox 360 pad needs no unhooking.
- SDL has the Triton driver since 3.4.0 (`SDL_hidapi_steam_triton.c`, added 2025-11-12); SISR 0.6.2 builds SDL from
  `main` of 2026-10-10.
- VIIPER also emulates a HID mouse and keyboard (`docs/devices/mouse.md`, `keyboard.md`), and `libVIIPER.dll`
  embeds the whole server in-process (GPL-3.0). SISR turns its own keyboard/mouse emulation off for a loopback
  VIIPER address (`input/viiperbridge.go`); why is not documented. Relevant for #118.

## Not checked yet

- Whether the overlay route needs Steam's CEF flag (it was present; the probe uses no CEF).
- How the layout comes back after a release with a focus change, and how fast a second force takes over.
- Which XInput slot is the Steam Controller when Steam Input also hands other controllers (several Steam virtual
  pads); XInput doesn't say. SDL's Steam handle (`SDL_GetGamepadSteamHandle`) is SISR's way to pair them.
- Whether Steam's own use of the gyro still works after SDL had the IMU on and closed the controller (both write the
  same IMU setting to the device).
