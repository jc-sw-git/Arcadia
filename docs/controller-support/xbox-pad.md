# The Steam Controller as an Xbox pad for games (#213)

Arcadia's own Xbox 360 pad for games outside Steam, and the per-game mode. Keeping Steam away from the real controller
is the copy's job ([steam-controller-copy.md](steam-controller-copy.md)); no layout is forced on a marker, no focus bounce.

## The pad

- `SteamControllerXboxPad` (ControllerSupport) takes every SDL read (`Feed` on `Sampled`), keeps only the Steam
  Controller (`GamepadReading.IsSteamController`) and sends the new state from that thread (`PadForwarder`, a slow
  VIIPER never holds up Arcadia's input; [every-controller.md](pad-latency.md)). Only changes are sent;
  VIIPER keeps the last state.
  Every other controller gets a pad of its own: [every-controller.md](every-controller.md) (#238).
- A game's rumble on the pad (VIIPER's `Xbox360Output`, two motor bytes) goes back to the Steam Controller through SDL
  (`IGamepadSource.HoldRumble`), and stops when the pad goes. The Steam button is never the pad's Guide: it is Steam's.
- Back + Start never reaches the game (`ComboWithholding`, Input): ⧉ and ≡ are held back for the 100 ms in which the
  other may still join (`MouseMode.ComboWindow`). The rules are a product decision: `docs/product/controls.md`.
- The pad passes input only while a library game on "Arcadia" owns the foreground window and the mouse mode is off
  (`SteamControllerCopyRules.PadPasses`, #357); otherwise it reports rest (`ForwardingAllowed`). Passing anywhere else
  let Steam read the Steam Controller through the pad (its glyphs flashed to Xbox icons) right after a switch to
  Steam. The foreground is read on every poll; `ForegroundOwner` looks up a process's path only when the foreground
  process changed, and with no foreground window (some switches) the pad rests. `arcadia.log`: "Xbox-Pad: gibt
  Eingaben weiter" / "meldet Ruhe". The combo filter keeps running, so a combo begun in Arcadia stays Arcadia's.
- `TickAsync` (every 2 s, `App`) plugs the pad in while a Steam Controller is connected and `Wanted` holds, and out
  otherwise: a game never sees an idle extra pad. `Wanted` comes from `SteamControllerRouting` (below).
- `ViiperXboxPads` runs Arcadia's own `viiper.exe` (the pinned fork build next to Arcadia.exe,
  [steam-controller-copy-windows.md](steam-controller-copy-windows.md)) as its own process, bound to the loopback with
  its own key (#304; a VIIPER another program runs is never used), log `viiper.log` beside `arcadia.log`, and drives it
  with VIIPER's MIT client `Viiper.Client`: one bus, one `xbox360` device per plug, each bus id used once per run
  (`ViiperBusIds`, #331, below). A plug-in that fails after the bus
  was created removes the bus again. `PrepareAsync` (each emulation's start, before Pan learns the port) starts it, or
  keeps the running one while its listeners check out and it answers with its key; a plug never starts one.
- `ViiperServer.StartAsync`, every step fail closed (VIIPER ended, start failed, logged): end a `viiper.exe` left from
  the same path; write a fresh key below Arcadia's data folder and start `viiper.exe` with `--usb.addr=127.0.0.1:0
  --api.addr=127.0.0.1:0 --api.require-local-host-auth`, `AppData` pointed there and other `VIIPER_*` variables
  removed; job object; wait for exactly two listeners of its own, only on 127.0.0.1, nobody else on either number
  (IPv4 and IPv6); the API is the one port answering with the key; delete the key file; a ping without key and one
  with a wrong key must be refused. Before each pad or copy the listeners are checked again. Auth and encryption are
  VIIPER's (`ViiperConnection`, checked against `Viiper.Client`'s own code and the fork's real server).
- `viiper.exe` is in a job object with kill-on-close, so it ends with Arcadia also after a crash; usbip-win2 drops its
  devices when the connection closes, and Pan's teardown detaches what the journaled port served ([pan.md](pan.md)).
- `IGamepadSource.Lost` (SDL failed for good) calls `Drop`: the game gets a pad at rest at once, and the next tick
  unplugs it.
- A pad lost (server ended, stream closed, send failed or hung for 1 s) is plugged in again at the next tick; more
  than 3 losses in 5 minutes and Arcadia stops until its next start or "Controller wiederherstellen", with a toast. A
  pad Pan took away (`IVirtualXboxPad.TakenAway`: its teardown, or Pan gone) is plugged in again but is no loss;
  "Controller wiederherstellen" ends VIIPER on purpose and clears the count (`ForgetLosses`). On exit, also for an
  update, the pad and the server go (each step on its own, so a failed unplug still ends the server); after the
  restart the pad comes back by itself.
- Unplugging and every 10 minutes log the delay from an SDL read to the state being with VIIPER (n, mean, p50, p99,
  max); while the pad is plugged SDL reads every 1 ms.

## usbip-win2 attaches a removed device again (#331)

- A device whose usbip connection ends without `usbip detach` (Arcadia removed it from VIIPER, VIIPER ended) is
  attached again by usbip-win2's driver itself: first after ~30 s, then with growing delays, up to 20 tries (its
  `ReattachFirstDelay`, `ReattachMaxAttempts`). It asks for the same server port and bus id.
- VIIPER gives a new bus the lowest free id, so the next pad or copy on the same VIIPER had the removed device's id and
  the retry attached it a second time: Steam listed the Steam Controller and the Xbox pad twice (owner's PC, usbip-win2
  0.9.8.1, `usbip port` showed each bus id once from `localhost` and once from `127.0.0.1`).
- So Arcadia names every bus itself (`ViiperBusIds`): never 0, never twice in a run, from a random start (Arcadia
  started again may get its old port from Windows). A retry for an old id finds nothing and gives up.

## Per-game mode

- `SteamControllerRouting.Decide` (ControllerSupport) takes the running games: `RunningGamesTracker.ActiveGames`
  (started from Arcadia, also while "Wird gestartet …", or seen in their launcher) plus Steam's `RunningAppID` as
  `steam:<id>`, so a Steam game started directly in Steam counts even when it isn't in the library (#119). The mode
  is `ControllerSupportService.UsesArcadiaPad(key)`: the game's own choice, else the launcher's default
  (`LauncherPreferences.UsesArcadiaPad`, #178), else the launcher's own (`LauncherInfo.GamesUseSteamInput`, only
  Steam; Arcadia for all others, also for a launcher that isn't registered, #156). A change applies at the game's
  next start.
- Any running game on Steam-Eingabe: no pad, Steam may use the controller. Without the driver
  (`IVirtualXboxPads.Unavailable`: VIIPER or usbip missing) no game gets the pad, and Steam may use it while any game
  runs. Otherwise the pad is in and the copy holds Steam off (`ISteamControllerHold`: `Apply(false)` = the copy
  reports only rest, `Apply(true)` = it forwards again, without Arcadia's combos).
- Right before a game starts (`GameActions`, after "Wird gestartet …" is set) the App decides again and unplugs at
  once (`TickNowAsync`, 3 s at most), so a game on Steam-Eingabe never finds Arcadia's pad. Its end brings the pad
  back at the next 2 s tick.

## Not seen yet (owner's PC)

- Everything with the real VIIPER: plug-in time, the delay figures, a game seeing exactly one Xbox controller, rumble
  reaching the Steam Controller.
