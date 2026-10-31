# Every other controller as an Xbox pad for games (#238)

`TakenOverControllers` (`src/Arcadia.ControllerSupport/Pan/`) takes every controller Arcadia reads, except the Steam
Controller (its own pad, [xbox-pad.md](xbox-pad.md)) and what `ControllerList.MayTakeOver` excludes (Steam's virtual
pads, Arcadia's own pads, single Joy-Cons). It runs inside Pan's emulation (`PanEmulation.Others`): the emulation
starts as soon as a Steam Controller *or* another controller is connected, and `KeepAsync` keeps the others after the
Steam Controller's pad. The Steam Controller's consent (puck and controller) is its own: "Nein" there leaves the other
controllers taken over. Product rules: `docs/product/controls.md`, the settings rows: `docs/product/settings.md`.

## Hide, then plug

- Pan hides one **model** (vendor:product) at a time, all its connected instances
  ([pan-hiding-controllers.md](pan-hiding-controllers.md)). Windows refuses the restart while any handle is open, so
  Arcadia lets go of **every** controller first: `IGamepadSource.ReadControllers(false)` closes the pads and quits SDL
  (a close alone isn't enough, check A3), then all pads rest, the Steam Controller's too. Pan's heartbeat is pulsed
  every 250 ms meanwhile, and SDL is opened again on the next poll. SDL gives the pads new ids then; controllers are keyed by their SDL path (by
  `model#index` without one), and for `RereadGrace` (1 s) after the reread a controller not seen yet isn't dropped.
- A model is hidden once per emulation generation. A rejected model is tried again when a new instance of it appears
  (a reconnect), `FirmwareToolRuns` after 10 s once no process of the vendor's tool runs any more
  (`ControllerList.FirmwareTools`): each try lets go of every controller.
- A pad is plugged only for a hidden controller (never both): not after a rejection, and for `ControllerNeedsReplug`
  (XInput interfaces are hidden only from programs that open them later) only once that controller reconnected.
  Pads are plugged in connection order, so the first controller gets the first slot Arcadia owns.
- The hidden XInput pad still holds its XInput slot (check A2): Arcadia's pad for it gets the next slot. Known limit.

## Forwarding

- The poll thread feeds `Feed`: per controller a one-slot channel (the newest state wins) and a sender with a 1 s
  timeout; a hung send counts as a lost pad. `ComboWithholding` filters on every poll (Back + Start, ⧉/≡), Guide is
  passed (`XboxPadState.From(…, withGuide: true)`).
- Neutral while Arcadia is in front or the mouse mode is on; unlike the Steam Controller's pad also while Steam is in
  front.
- Rumble: VIIPER's output report (2 bytes, large/small motor) raises `IVirtualXboxPad.Rumbled`; Arcadia holds the
  rumble on the real controller with `HoldRumble` (2 s, renewed every second while it lasts, 0 stops it). Unplugging a
  pad stops it. The report is read with the pad's own token, cancelled before `ViiperDevice.DisposeAsync`: that waits
  for its read loop, which otherwise waits for the next report, so an unplug would hang until the game rumbles.
- A lost pad is replugged; Pan taking it away doesn't count. Three losses in 5 minutes give up (`GaveUp`, a notice),
  "Controller wiederherstellen" and Retry forget them.

## The settings rows (D7, D8)

While the emulation runs, `NotHidden` lists every controller Pan rejected or that waits for a reconnect, with Pan's
reason; `WaitingForTool` those whose vendor's firmware tool runs (Xbox Zubehör, DualSense-Firmware-Updater).
`PanRows` turns them into the info row "… lässt sich nicht verstecken" and one warning row per tool.

## Not done yet

The pad's own kind (DualShock 4, DualSense, Switch 2 Pro 1:1), the "Controller zuordnen" modal and the latency budget
(D9) follow in #238's later parts. Not detected: VIIPER refusing another pad (no free slot). `ReleaseForFirmware` ends
the whole emulation, so the other controllers' pads are replugged too.
