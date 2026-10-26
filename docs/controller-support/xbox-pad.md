# The Steam Controller as an Xbox pad for games (#213)

Part A of #213 (owner, 2026-10-26: split in two PRs into `feat/replace-sisr`): the pad itself. Part B adds Steam's layout
(forcing an empty one on Arcadia's marker, releasing it) and the per-game mode "Arcadia" / "Steam-Eingabe". Overview and
the other files: [README](README.md); what the spike saw: [steam-input-helper.md](steam-input-helper.md).

## How it works

- `SteamControllerXboxPad` (ControllerSupport) takes every SDL poll (`Feed`, poll thread), keeps only the Steam
  Controller (`GamepadReading.IsSteamController`) and hands the newest state to a sender task through a one-slot channel
  (drop oldest), so a slow VIIPER never holds up Arcadia's input. Only changes are sent; VIIPER keeps the last state.
  Other controllers are never emulated.
- Arcadia's combos never reach the game (`ComboWithholding`, Input): ⧉ and ≡ are held back until it is clear they start no
  combo. The rules are a product decision: `docs/product/controls.md`.
- While Arcadia is in front or the mouse mode is on, the pad reports rest (`ForwardingAllowed`); the combo filter keeps
  running, so a combo begun in Arcadia stays Arcadia's.
- `TickAsync` (every 2 s, `App`) plugs the pad in while a Steam Controller is connected and `Wanted` holds, and out
  otherwise: a game never sees an idle extra pad. Until part B, `Wanted` = SISR is off (`SisrState.Off`), so the two
  never run together.
- `ViiperXboxPads` runs `viiper.exe server --no-tray` from SISR's folder (`%LOCALAPPDATA%\SISR`, until #214 brings its
  own) as its own process, log `viiper.log` beside `arcadia.log`, API `localhost:3242`, and drives it with VIIPER's MIT
  client `Viiper.Client` 0.9.0: one bus, one `xbox360` device per plug. A VIIPER that already answers on the port (another
  program's) is used, not replaced. Before Arcadia's first server it runs SISR's clean-up once
  (`SisrHost.CleanUpViiperAsync`: a VIIPER from SISR's folder left over from a crash, and its usbip ports).
- `viiper.exe` is in a job object with kill-on-close, so it ends with Arcadia also after a crash; the usbip port it held
  is cleaned up at the next start.
- A pad lost (server ended, stream closed, send failed) is plugged in again at the next tick; more than 3 losses in
  5 minutes and Arcadia stops until its next start, with a toast. On exit, also for an update, the pad and the server go;
  after the restart the pad comes back by itself.
- Unplugging logs the delay from an SDL poll to the state being with VIIPER (mean and maximum). The SDL poll itself adds
  up to 8 ms before that.

## Not seen yet (owner's PC)

- Everything with the real VIIPER: plug-in time, the delay figures, a game seeing exactly one Xbox controller, rumble
  (not forwarded yet; VIIPER reports it as `Xbox360Output`).
