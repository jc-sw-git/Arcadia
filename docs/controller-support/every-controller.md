# Every other controller as an emulated pad for games (#238)

`TakenOverControllers` (`src/Arcadia.ControllerSupport/Pan/`) takes every controller Arcadia reads, except the Steam
Controller (its own pad, [xbox-pad.md](xbox-pad.md)) and what `ControllerList.MayTakeOver` excludes (Steam's virtual
pads, Arcadia's own pads, single Joy-Cons). It runs inside Pan's emulation (`PanEmulation.Others`): the emulation
starts as soon as a Steam Controller *or* another controller is connected, and `KeepAsync` keeps the others after the
Steam Controller's pad. "Controller freigeben" for a firmware update leaves only the Steam Controller and its puck to
Steam; the other controllers stay taken over. Product rules: `docs/product/controls.md`, the settings rows: `docs/product/settings-controller.md`.

## Hide, then plug

- Pan hides one **model** (vendor:product) at a time, all its connected instances
  ([pan-hiding-controllers.md](pan-hiding-controllers.md)). Windows refuses the restart while any handle is open, so
  Arcadia lets go of **every** controller first: `IGamepadSource.ReadControllers(false)` closes the pads and quits SDL
  (a close alone isn't enough, check A3), then all pads rest, the Steam Controller's too. The next poll reports every
  controller once with nothing held, so the mouse mode and the routing release what was held (#367). A button still
  held when the controllers come back is no new press until it was up once. View or Menu tapped in that moment acts
  as a tap at the let-go. Pan's heartbeat is pulsed
  every 250 ms meanwhile, and SDL is opened again on the next poll. SDL gives the pads new ids then; controllers are keyed by their SDL path (by
  `model#index` without one), and for `RereadGrace` (1 s) after the reread a controller not seen yet isn't dropped.
- A model is hidden once per emulation generation. A rejected model is tried again when a new instance of it appears
  (a reconnect), `FirmwareToolRuns` after 10 s once no process of the vendor's tool runs any more
  (`ControllerList.FirmwareTools`): each try lets go of every controller.
- A pad is plugged only for a hidden controller (never both): not after a rejection, and for `ControllerNeedsReplug`
  only once that controller reconnected. Pan reconnects a USB XInput pad itself (port cycle); the reason stays for a
  Bluetooth XInput pad and a hub that refuses. The cycled pad is gone after the answer and arrives again as a new
  controller, already hidden.
  Pads are plugged in connection order, so the first controller gets the first slot Arcadia owns.
- The hidden XInput pad still holds its XInput slot (check A2): Arcadia's pad for it gets the next slot, and `joy.cpl` lists no pad at all while it is emulated (seen on the owner's PC; Steam, launchers and games see the pads). Known limit.

## Forwarding

- Every read of the gamepad source feeds `Feed` (`IGamepadSource.Sampled`), and each new state goes to the pad from
  that thread (`PadForwarder`, [pad-latency.md](pad-latency.md)); a send slower than 1 s counts as a lost pad. `ComboWithholding` filters
  on every read (Back + Start, ⧉/≡), Guide is passed (`XboxPadState.From(…, withGuide: true)`).
- Neutral while Arcadia is in front or the mouse mode is on, unless Steam's desktop control is allowed
  (`SteamControllerCopyRules.OtherControllerPasses`): then they pass there, with the copy's combos for Steam
  (`CombosKeptFromSteam`), since Steam sees these controllers only through the pads. Unlike the Steam Controller's
  pad, they also pass while Steam is in front.
- Rumble: VIIPER's output report (2 bytes, large/small motor) raises `IVirtualXboxPad.Rumbled`; Arcadia holds the
  rumble on the real controller with `HoldRumble` (2 s, renewed every second while it lasts, 0 stops it). Unplugging a
  pad stops it. After a let-go the controller's new SDL id gets the motors the game set last again. The report is read with the pad's own token, cancelled before `ViiperDevice.DisposeAsync`: that waits
  for its read loop, which otherwise waits for the next report, so an unplug would hang until the game rumbles.
- A lost pad is replugged; Pan taking it away doesn't count. Three losses in 5 minutes give up (`GaveUp`, a notice),
  "Controller wiederherstellen" and Retry forget them.

## Kinds (part 2, D3)

- `PadKinds.Default`: SDL's type PS4 → DualShock 4, PS5 → DualSense (054C:0DF2 → DualSense Edge), 057E:2069 →
  Switch 2 Pro; everything else (Switch 1 Pro, GameCube, PS3, XInput pads) → Xbox 360. The pad's kind is fixed when the
  controller connects.
- The Xbox 360 pad stays on Viiper.Client's typed stream; the others use VIIPER's raw device stream
  (`ViiperReportPad`, `VirtualPadReports`): fixed-size packets as in the fork's `device/*/state.go` (DualShock 4 31
  bytes, DualSense 33, Switch 2 Pro 24 in; feedback 8, 6, 34). The fork (upstream of 2026-10-15 plus `hidproxy`)
  already has these types.
- Buttons by position (#72): south is Cross / Nintendo's B. Guide → PS / Home; SDL's misc1 → mute (DualSense) or
  Capture (Switch 2 Pro), misc2 → C; paddle 1 → Edge L4/R4 or GR/GL, paddle 2 → Edge Fn. A PlayStation trigger's
  button is set as soon as it moves; the Switch's ZL/ZR past half way.
- Touchpad: SDL's first finger only, scaled to 1920 × 942 (DS4) or 1920 × 1080 (DualSense); the second finger stays
  lifted.
- Motion: SDL's gyro (rad/s) and accelerometer (m/s²) back to the device's counts, so SDL on the host reads the same
  values: PlayStation axes as SDL's (16 resp. 16.384 counts per °/s, 8192 per g); Switch 2 Pro the inverse of SDL's
  swap (x, z, −y), gyro 34.8 rad/s and accelerometer 8 g per 32767. Without motion a pad lies flat (gravity on +Y).
  The real controllers' sensors are on (`IGamepadSource.UseMotionSensors`) only while such a pad is plugged.
- Feedback: DualShock 4 rumble and light bar by its flags; DualSense rumble from every report (VIIPER ignores the rumble flag, so a
  light-only report with zero motors stops it; SDL always sends both), the light bar only when not black (VIIPER sends
  zero without the LED flag); Switch 2 Pro HD rumble decoded from SDL's encoding (low band →
  large motor, high band → small, the stronger side, amplitude max 29000 = 255). The light bar goes to the real
  controller with `SDL_SetGamepadLED` and is set again after a let-go, like the rumble. Not carried: adaptive triggers
  and DualSense haptics (VIIPER has no output for them), player LEDs.
- Arcadia's own pads carry the real ids: `GamepadDevices.WhyIgnored` leaves out a Sony or Nintendo pad whose HID path
  is on a software host (`IEmulatedPads.IsVirtualHidPad`). Where Windows' device tree can't be read, a pad with the ids
  of one Arcadia has plugged in (`OwnPadModels`) counts as Arcadia's, so pads never feed pads. Pan never hides devices
  on a software host. The light bar keeps the game's colour after its pad goes.

## The settings rows (D7, D8)

While the emulation runs, `NotHidden` lists every controller Pan rejected or that waits for a reconnect, with Pan's
reason; `WaitingForTool` those whose vendor's firmware tool runs (Xbox Zubehör, DualSense-Firmware-Updater).
`PanRows` turns them into the info row "… lässt sich nicht verstecken" and one warning row per tool.

## Controller zuordnen (part 3, D3a)

- `AppSettings.ControllerLayouts`: per model ("054C:0CE6") a kind's name or "NotTakenOver"; read by
  `ControllerLayouts.Find`, which ignores anything else in the file. `TakenOverControllers.LayoutOf` reads it each tick.
- A kind is used only where the model may be it (`ControllerLayouts.Choices`: the Edge only for an Edge), else its own.
  A pad of another kind than chosen is unplugged at the next tick and the chosen one plugged (the game sees it change once).
  The modal stores only on [Übernehmen], all changes in one save, so trying layouts never replaces pads or shows and hides.
- "Nicht übernehmen": the model is never hidden, starts no emulation on its own and isn't named in the D7/D8 rows. A
  hidden one has its pads unplugged first, then Arcadia lets go of every controller and Pan shows it
  (`ShowController`, [pan-hiding-controllers.md](pan-hiding-controllers.md)). A show Pan refuses gets no pad either: Pan
  answers `HidingFailed` also when it showed the model and only the restart failed, so a pad could make two. Taken
  over again, the model is hidden anew; still hidden, a reconnect or "Controller wiederherstellen" frees it.
- `Connected` lists one entry per connected model (excluded ones too) for the modal, also while Pan is off.

## Latency (D9)

Reads every 1 ms while pads get states for a game, sends on the reading thread, logs per pad: [pad-latency.md](pad-latency.md).

## Not done yet

Not verified on hardware yet: the button bits of the Switch 2 Pro's C and the Edge's paddles as SDL maps them, the
Switch 2 Pro's stick calibration with VIIPER's zeroed flash. Not detected: VIIPER refusing another pad (no free slot). `ReleaseForFirmware` ends
the whole emulation, so the other controllers' pads are replugged too.
