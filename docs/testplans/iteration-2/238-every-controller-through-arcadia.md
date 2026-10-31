# #238 Pass every controller to games through Arcadia

Pan hides every controller Arcadia reads, and Arcadia gives each one its own emulated pad
([every-controller.md](../../controller-support/every-controller.md)). Cut into parts: **part 1** passes every
controller on as an Xbox 360 pad with combos, rest, Guide, rumble, failure handling and the D7/D8 rows; **part 2** the
1:1 kinds (DualShock 4, DualSense incl. Edge, Switch 2 Pro) with touchpad, gyro and light bar (T238-1, -5, -14);
part 3 "Controller zuordnen" (T238-13), part 4 the latency budget (T238-11).

T335-1 to T335-3 (Pan's hiding on real hardware, `335-pan-hides-every-controller.md`) run with this build too.

- [ ] **T238-1** DualSense (USB) and 8BitDo Ultimate 2 (dongle) connected, Arcadia starts: a test game and Steam see
      exactly one pad of Arcadia's per controller, in connection order, and neither real pad: a DualSense and an Xbox
      360 pad (Windows' "Game Controllers" dialog, `joy.cpl`, names them). The 8BitDo (XInput)
      is gone for a few seconds when Pan hides it (port cycle), then emulated without a reconnect by hand; the D7 row
      doesn't name it. Also with the 8BitDo plugged in while Arcadia and Steam run. Note the XInput slot Arcadia's pad
      gets (known limit: the hidden pad keeps slot 0).
      · E2E: no (real pads, HidHide); the flow with fakes in `EveryControllerForGamesTests`
- [ ] **T238-2** As T238-1 with the DualSense by Bluetooth, switched on while the game runs: the game sees one new pad,
      never two. · E2E: no (real pads)
- [ ] **T238-3** Back + Start with each pad in a running game: Arcadia reacts, the game's input log never shows it; a
      short ⧉ reaches the game as a tap; ⧉ held brings Arcadia back and the game sees ⧉ held (after 100 ms).
      · E2E: yes (`EveryControllerForGamesTests`)
- [ ] **T238-4** Arcadia in front, then the mouse mode on: the game reads rest from every pad. With "Steams
      Desktop-Steuerung zulassen" on, Steam's desktop layout works with the DualSense in front of Arcadia and in the
      mouse mode, ⧉ + ≡ switches Steam's action set. · E2E: yes (`EveryControllerForGamesTests`)
- [ ] **T238-5** Rumble in a game reaches DualSense, 8BitDo and Steam Controller and stops with the game's rumble; Guide /
      PS opens Steam's overlay or the Game Bar. In a game with native DualSense support (or Steam's controller test):
      touchpad (position and click), gyro (turning the pad turns the same way, at rest nothing drifts), mute button and
      the light bar colour the game sets on the real DualSense. Adaptive triggers and haptics are not carried (VIIPER has
      no output for them). · E2E: no (real hardware); the path from the pad's output to SDL with fakes in
      `EveryControllerForGamesTests`
- [ ] **T238-6** "Nein" in the takeover question: Steam Controller and puck stay Steam's, the DualSense is still hidden
      and arrives as Arcadia's pad. · E2E: yes (`EveryControllerForGamesTests`)
- [ ] **T238-7** A controller Pan can't hide: it reaches the game directly, no pad of Arcadia's; the info row
      "… lässt sich nicht verstecken" lists it with its reason. Pan off: every pad reaches the game directly and the row
      is gone. · E2E: yes (`EveryControllerForGamesTests`), by hand with any controller the row names
- [ ] **T238-8** Kill `Arcadia.exe` in Task Manager mid-game: the real pads are back in the game within 10 s.
      · E2E: no (Pan, HidHide)
- [ ] **T238-9** Kill `viiper.exe` mid-game: the pads come back at the next tick; after 4 kills in 5 minutes the
      emulation stops with a toast and the real pads are visible. · E2E: no (real VIIPER); the give-up with fakes in
      `TakenOverControllersTests`
- [ ] **T238-10** Reboot while pads are hidden (hard reset): after login every pad is visible to games.
      · E2E: no (reboot)
- [ ] **T238-11** Latency and CPU in `arcadia.log` after a 10-minute session with each pad within D9. Part 4.
      · E2E: no (real hardware)
- [ ] **T238-12** Xbox Zubehör open at Arcadia's start: the Xbox pad isn't hidden, the warning row "Xbox Zubehör ist
      geöffnet" says why; closed, the row goes and the pad is taken over within 10 s. With the real tool, check the
      process name Pan looks for. · E2E: yes (`EveryControllerForGamesTests`, fake Pan answer); the real tool by hand
- [ ] **T238-13** Assign kinds and exclude in "Controller zuordnen". Part 3. · E2E: yes (fakes), plus by hand
- [ ] **T238-14** Each other 1:1 kind at hand (DualShock 4, DualSense Edge, Switch 2 Pro; a Switch 1 Pro or GameCube
      pad as a controller without one): the game sees that kind (a pad without one as an Xbox 360 pad), every button by
      position (Nintendo's lower B is the game's south button), sticks in the right direction, Edge paddles and Fn,
      Switch 2 Pro C, GL/GR and Capture, gyro and rumble. Arcadia's own pad is never listed as a controller in Arcadia.
      Kinds not at hand are named in the history. · E2E: no (real hardware); the packets in `VirtualPadReportsTests`

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T238-1, -3 to -7, -12 (fakes) | 2026-10-31 | Claude | branch `feat/emulate-every-controller-238` (part 1), Linux container, Avalonia headless, fake SDL, Pan and VIIPER | ✅ 7 acceptance tests, 14 `TakenOverControllersTests`; full suite green (Core 2156, Pan 279, App 466) | Mutation check: dropping the rejection check from "hidden" turns T238-7 and T238-12 red, dropping the D2 condition T238-6 |
| T238-3, -4 (fakes), QE review | 2026-10-31 | Claude (QE) | branch `feat/emulate-every-controller-238` at 265c32e, Linux container, Avalonia headless, fakes | ✅ 8 acceptance tests incl. the new T238-3 test for the second pad; full suite green (Core 2156, App 466) | Red once: without `ComboWithholding` in `Feed` both T238-3 tests fail, without the rest while Arcadia is in front T238-4 fails |
| QE findings 1–3 | 2026-10-31 | Claude | branch `feat/emulate-every-controller-238` after 0b406a3, Linux container; QE's probe `checks/338/` with Viiper.Client 0.9.0 over loopback | ✅ with the read's token cancelled `DisposeAsync` returns (without: hangs); new tests for the firmware tool retry and the Steam Controller's pad at rest, each red once without its fix | – |
| T238-4 with desktop control (fakes) | 2026-10-31 | Claude | branch `feat/emulate-every-controller-238` after the merge of `feat/replace-sisr` (#315), Linux container, Avalonia headless, fakes | ✅ new acceptance test; red once each without the pass rule and without Steam's combos | – |
| port cycle for T238-1 | 2026-10-31 | owner (check script `Run-Check-338-portcycle.ps1`) | Windows 11, owner's PC, 8BitDo Ultimate 2 dongle on a root hub port, Steam running | ✅ in-place restart refused ("needs a restart"), port cycle accepted, device back after ~4 s and working | Pan reconnects newly hidden XInput pads by port cycle; `ControllerHidingTests` red once without it |
| QE finding 5 (fakes) | 2026-10-31 | Claude | branch `feat/emulate-every-controller-238`, Linux container, fakes | ✅ a constant rumble carries over to the controller's new SDL id after a let-go; `TakenOverControllersTests` red once without it | – |
| T238-11 pre-measurement (part 1) | 2026-10-31 | Claude from the owner's `arcadia.log` | Windows 11, owner's PC, branch at 9030a1b, DualSense USB, 8BitDo dongle, short sessions | ⚠️ poll → VIIPER mean 0.1 ms, max 3.9 ms (DualSense, 1750 states), 0.3–0.4 ms (8BitDo); first state after a plug up to 4.2 ms. Not covered: the wait for the 8 ms poll (`Thread.Sleep`), CPU, p99, 10 minutes | Within D9 only for the measured stretch; the fixed poll breaks D9 → part 4 (event-driven forwarding, CPU and p99 per pad) |
| real pads, Pan, VIIPER | – | – | – | not run: no Windows with pads in the cloud container | owner's test of part 1 |
| T238-1, -5 part 2 (fakes) | 2026-10-31 | Claude | branch `feat/emulate-kinds-238` (part 2), Linux container, Avalonia headless, fake SDL, Pan and VIIPER | ✅ 2 new or extended acceptance tests (kinds per controller; touchpad, motion, mute and light bar through the App), 4 new `TakenOverControllersTests`, 17 `VirtualPadReportsTests` (packets against the fork's `state.go`, SDL's sensor and HD rumble maths), own pads ignored in `EmulatedXboxPadsTests`; full suite green (Core 2221, Pan 285, App 480) | Red once: with every PS5 pad an Xbox 360 pad T238-1 and T238-5 fail, without the light bar's wiring T238-5 |
| QE findings of #346 (fakes) | 2026-10-31 | Claude | branch `feat/emulate-kinds-238`, Linux container, fakes | ✅ own pads known while plugged (`OwnPadModels`), counted as Arcadia's when the device tree can't be read; an Xbox 360 pad gets no state for motion alone; each new test red once without its fix | – |
| T238-1, -5, -14 on hardware | – | – | – | not run: no Windows with pads in the cloud container | owner's test of part 2 (check script `Run-Check-238-kinds.ps1`) |
