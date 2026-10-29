# The Steam Controller as an Xbox pad for games (#213)

Part A of #213 (owner, 2026-10-26: split in two PRs into `feat/replace-sisr`): the pad itself. Part B: the per-game mode
"Arcadia" / "Steam-Eingabe" and SISR no longer started; keeping Steam away from the controller is story #242 (owner, 2026-10-26,
after spike #239): HidHide hides the real controller from Steam and an emulated copy goes silent while Arcadia serves it;
no empty layout forced on a marker, no focus bounce. Overview and the other files:
[README](README.md); what the spike saw: [steam-input-helper.md](steam-input-helper.md).

## How it works

- `SteamControllerXboxPad` (ControllerSupport) takes every SDL poll (`Feed`, poll thread), keeps only the Steam
  Controller (`GamepadReading.IsSteamController`) and hands the newest state to a sender task through a one-slot channel
  (drop oldest), so a slow VIIPER never holds up Arcadia's input. Only changes are sent; VIIPER keeps the last state.
  Other controllers are never emulated.
- Back + Start never reaches the game (`ComboWithholding`, Input): ⧉ and ≡ are held back for the 100 ms in which the
  other may still join (`MouseMode.ComboWindow`, the same rule the mouse mode switches on by). The rules are a product
  decision: `docs/product/controls.md`.
- While Arcadia is in front or the mouse mode is on, the pad reports rest (`ForwardingAllowed`); the combo filter keeps
  running, so a combo begun in Arcadia stays Arcadia's.
- `TickAsync` (every 2 s, `App`) plugs the pad in while a Steam Controller is connected and `Wanted` holds, and out
  otherwise: a game never sees an idle extra pad. `Wanted` comes from `SteamControllerRouting` (below).
- `ViiperXboxPads` runs `viiper.exe server --no-tray` (Arcadia's own build of its VIIPER fork next to Arcadia.exe, #242;
  the one an older SISR install left in `%LOCALAPPDATA%\SISR` while no build is pinned) as its own process, log `viiper.log` beside `arcadia.log`, API `localhost:3242`, and drives it with VIIPER's MIT
  client `Viiper.Client` 0.9.0: one bus, one `xbox360` device per plug. A VIIPER that already answers on the port (another
  program's) is used, not replaced. A plug-in that fails after the bus was created removes the bus again.
- `viiper.exe` is in a job object with kill-on-close, so it ends with Arcadia also after a crash; the usbip port it held
  is cleaned up at the next start.
- `IGamepadSource.Lost` (SDL failed for good) calls `Drop`: the game gets a pad at rest at once, and the next tick
  unplugs it.
- A pad lost (server ended, stream closed, send failed or hung for 1 s) is plugged in again at the next tick; more than 3 losses in
  5 minutes and Arcadia stops until its next start or "Controller wiederherstellen", with a toast. A pad Pan took away
  (`IVirtualXboxPad.TakenAway`: its teardown, or Pan gone) is plugged in again but is no loss; "Controller
  wiederherstellen" ends VIIPER on purpose and clears the count (`ForgetLosses`). On exit, also for an update, the pad and the server go
  (each step on its own, so a failed unplug still ends the server);
  after the restart the pad comes back by itself.
- Unplugging logs the delay from an SDL poll to the state being with VIIPER (mean and maximum). The SDL poll itself adds
  up to 8 ms before that.

## Per-game mode (part B)

- `SteamControllerRouting.Decide` (ControllerSupport) takes the running games: `RunningGamesTracker.ActiveGames` (started
  from Arcadia, also while "Wird gestartet …", or seen in their launcher) plus Steam's `RunningAppID` as `steam:<id>`, so
  a Steam game started directly in Steam counts even when it isn't in the library (#119). The mode is
  `ControllerSupportService.UsesArcadiaPad(key)`: the game's own choice, else its launcher's default from its page
  (#178), else Steam-Eingabe for games whose launcher says they use Steam Input (`LauncherInfo.GamesUseSteamInput`,
  only Steam) and Arcadia for all others, also for a launcher that isn't registered (#156).
- Any running game on Steam-Eingabe: no pad, Steam may use the controller. Without the driver (`IVirtualXboxPads.Unavailable`:
  VIIPER or usbip missing) no game gets the pad, and Steam may use it while any game runs. Otherwise the pad is in and
  Steam is kept away (`ISteamControllerHold`, a no-op until #242: `Apply(false)` = the copy reports
  only rest, `Apply(true)` = it forwards again, without Arcadia's combos).
- Right before a game starts (`GameActions`, after "Wird gestartet …" is set) the App decides again and unplugs at once
  (`TickNowAsync`, 3 s at most), so a game on Steam-Eingabe never finds Arcadia's pad. Its end brings the pad back at
  the next 2 s tick.
- SISR is never started; at the start only an SISR the older Arcadia left running is stopped, and the settings remove
  what is left of it on request ([sisr-removal.md](sisr-removal.md), #214).

## Not seen yet (owner's PC)

- Where Steam keeps the layout of a non-Steam shortcut such as SISR's marker: not in
  `steamapps\common\Steam Controller Configs\<account>\config\configset_controller_triton.vdf` (empty on the owner's
  PC, 2026-10-26); `controller_base\empty.vdf` is Steam's empty template ("Empty Bindings - Use as Base").
- Everything with the real VIIPER: plug-in time, the delay figures, a game seeing exactly one Xbox controller, rumble
  (not forwarded yet; VIIPER reports it as `Xbox360Output`).
