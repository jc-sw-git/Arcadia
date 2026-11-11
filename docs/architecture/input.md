# Architecture: `Arcadia.Input`

Controller input and the mouse mode. Findings on the controllers: `docs/controller-support/`. The other projects:
[README.md](README.md).

**Reading controllers**
- `SdlGamepadSource`: every controller via SDL3, the Steam Controller too (`SdlGamepads`, `SdlPadMapping`).
  `GamepadDevices` picks the devices; `DeviceTreeEmulatedPads`/`XusbDevices` and `DeviceTree` (a device on a hardware
  or a software USB host) spot emulated pads such as VIIPER's; `OwnPadModels` the PlayStation and Switch pads Arcadia
  plugged in itself. `SteamControllerPresence`, `ControllerInUse` (whose symbols the hints show).
- `ReadPacing`/`PreciseWait`/`LatencyStats`: 1 ms reads while pads are forwarded, their log
  ([pad-latency.md](../controller-support/pad-latency.md)).
- `RepeatedErrors`: the unexpected errors of places that repeat them (`GuardedHandlers` of the SDL events, and in
  `Arcadia.ControllerSupport` `TakenOverControllers` and `PanClient`): per source one toast per streak, repeats at most
  one log line a minute, a new streak after 10 quiet minutes (#399); `ErrorStreak` counts its repeats.
- `XboxPadState`, `VirtualPadState` (what an emulated pad of any kind reports) and `ComboWithholding` (a controller's
  reading without Arcadia's combos).

**Buttons to actions**
- `GamepadPolls`: polls → presses, repeats and releases, left stick as D-pad, auto-repeat for D-pad, sticks, LB/RB and
  LT/RT (`ButtonRepeated`, not a press); platform-free, so the acceptance tests use it too.
- `HoldGesture` (tap vs. hold), `RepeatGesture` (held erase), `LatchGesture` (shift: tap or hold), `SoloButtons`
  (View/Menu act alone, not together).
- `ButtonSymbolSets` (each controller's printing for the neutral button names), `ButtonText` (the buttons in a text),
  `KeyboardButton` (the buttons while the mouse mode's keyboard is open).

**Mouse mode**
- `MouseMode` (Back + Start, `Polled` readings, also in the background), `MouseModeMapping` (buttons → mouse/keys),
  `MouseModeAutomation`, `StickCurve`, `SendInputOutput` (`IMouseKeyboardOutput`).
- `ReturnWithView`: View held brings Arcadia back.
- `ControllerActivity`/`InjectedInputGate`/`SteamDesktopInputFilter`: drop Steam's desktop layout
  (`docs/product/mouse-mode.md`).
