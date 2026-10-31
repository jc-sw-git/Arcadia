# #238 Pass every controller to games through Arcadia

Pan hides every controller Arcadia reads, and Arcadia gives each one its own emulated pad
([every-controller.md](../../controller-support/every-controller.md)). Cut into parts: **part 1** passes every
controller on as an Xbox 360 pad with combos, rest, Guide, rumble, failure handling and the D7/D8 rows; **part 2** the
1:1 kinds (DualShock 4, DualSense incl. Edge, Switch 2 Pro) with touchpad, gyro and light bar (T238-1, -5, -14);
**part 3** "Controller zuordnen" (T238-13), part 4 the latency budget (T238-11).

T335-1 to T335-3 (Pan's hiding on real hardware, `335-pan-hides-every-controller.md`) run with this build too.

- [ ] **T238-1** DualSense (USB) and 8BitDo Ultimate 2 (dongle) connected, Arcadia starts: a test game and Steam see
      exactly one pad of Arcadia's per controller, in connection order, and neither real pad: a DualSense and an Xbox
      360 pad (Steam's controller settings name them; `joy.cpl` may show an empty list while a hidden XInput pad is
      emulated, which games and launchers don't mind). The 8BitDo (XInput)
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
- [ ] **T238-11** A game runs 10 minutes with DualSense (USB), 8BitDo (dongle) and the Steam Controller (mode
      „Arcadia“) in use beside each other, the DualSense's gyro on (a game with DualSense support, or Steam's
      controller test). `arcadia.log` says "Arcadia liest alle 1 ms" while the game gets a pad's states, and "liest wieder
      alle 8 ms" once Arcadia is in front (pads at rest) or the last pad goes. In the summaries (after 10 minutes and when the pad goes): per pad "Verzögerung Lesen →
      VIIPER" and "Abstand der Lesevorgänge" give half the gap's mean plus the pad's mean ≤ 1 ms and the gap's max plus
      the pad's max ≤ 4 ms; "CPU Arcadia" ≤ 1 % aller Kerne. Arcadia's own input (menu, mouse mode) feels as before.
      · E2E: no (real hardware, timer); pacing, forwarding and the switch in `ReadPacingTests`, `PadForwarderTests`,
      `TakenOverControllersTests`, `SdlGamepadSourceTests`
- [ ] **T238-12** Xbox Zubehör open at Arcadia's start: the Xbox pad isn't hidden, the warning row "Xbox Zubehör ist
      geöffnet" says why; closed, the row goes and the pad is taken over within 10 s. With the real tool, check the
      process name Pan looks for. · E2E: yes (`EveryControllerForGamesTests`, fake Pan answer); the real tool by hand
- [x] **T238-13** DualSense, a controller without a 1:1 kind (Switch 1 Pro or GameCube adapter) and the Steam
      Controller connected, a game with an input view (Steam's controller test) runs: they arrive as DualSense and Xbox
      360 pad. With the gamepad alone, Controller → "Controller zuordnen" → [A]: the box "SPIELE SEHEN" and the modal
      list each model once (two of a model: one row), the Steam Controller row is locked, takes the focus and shows its
      note, ◀ ▶ do nothing there. Switching layouts back and forth (also across "Nicht übernehmen") changes nothing in the
      game or Steam, B closes and the next open shows the old layouts. ◀ ◀ on the DualSense, ▶ ▶ and A (A also steps on)
      on the other, ▼ to [Übernehmen], A: the modal closes, within 2 s the game sees an Xbox 360 pad and a Switch 2 Pro (each reconnects once). Again ▶ on the
      other ("Nicht übernehmen"), [Übernehmen]: the game sees the real controller, Steam lists it as the real one, never two. The
      focus stays on the row. After a restart of Arcadia the choices hold. With the mouse: a click on ◀ ▶ changes,
      [Übernehmen] applies, ✕ closes without. · E2E: yes (`EveryControllerForGamesTests`, the drawn
      modal in `ControllerLayoutsViewTests`), plus by hand
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
| T238-1, -5 on hardware (part 2) | 2026-10-31 | owner (check script `Run-Check-238-kinds.ps1`) | Windows 11, owner's PC, branch `feat/emulate-kinds-238` at 5176e67, DualSense USB, 8BitDo Ultimate 2 dongle, Steam's controller test, Castlevania Belmont's Curse | ✅ one DualSense of Arcadia's, no second; buttons, analog triggers, sticks, touchpad (position, click), gyro without drift, PS, mute, light bar colour and rumble from the game on the real DualSense. With the 8BitDo added Steam and Arcadia see one DualSense and one Xbox 360 pad, the virtual DualSense is ignored by Arcadia, but `joy.cpl` shows an empty list while the 8BitDo is on, with or without the DualSense | ⚠️ `joy.cpl` empty as soon as the hidden 8BitDo (XInput) is emulated: a part 1 behaviour, Steam and Arcadia see the pads. T238-14 (DualShock 4, Edge, Switch 2 Pro) not run: no such pad at hand |
| T238-13 (fakes, part 3) | 2026-10-31 | Claude | branch `feat/assign-controllers-238` (part 3), Linux container, Avalonia headless, fake SDL, Pan and VIIPER | ✅ 2 acceptance tests (each made red once by a mutation: layouts not applied, row always shown), 9 new `TakenOverControllersTests`, `ControllerLayoutsTests`, 2 window tests (rings of first, locked and last row, scroll and fades with 8 models, looked at in their captures); full suite green | The real pads, Pan's show on hardware and the game's view only by hand |
| QE findings of #350 (fakes) | 2026-10-31 | Claude | branch `feat/assign-controllers-238`, Linux container, Avalonia headless, fakes | ✅ a show Pan refuses gives the model no pad and leaves it taken over anew later (`TakenOverControllersTests` red once without the fix); two controllers of one model are one row, also after one goes; the bottom fade covers a cut row at open (looked at in the capture); full suite green (Core 2219, Pan 284, App 483) | – |
| T238-13 apply on A (fakes) | 2026-10-31 | Claude | branch `feat/assign-controllers-238`, Linux container, Avalonia headless, fakes | ✅ owner's finding (every ◀ ▶ replaced the pad, across "Nicht übernehmen" restarted the real controller): the acceptance test tries layouts, ticks and B without any pad change or show, then A on [Übernehmen] below the rows applies both at once (A on a row steps on); red once with a store per step; ring around [Übernehmen] looked at in its capture | – |
| T238-13 on hardware | 2026-10-31 | owner | Windows 11, owner's PC, branch `feat/assign-controllers-238` at 0eaae00, DualSense and further controllers | ✅ passed. First round: every ◀ ▶ applied at once, so trying layouts replaced the pad and restarted the real controller, with Steam announcing each | Applied only with [Übernehmen] (0eaae00) |
| T238-11 pacing and forwarding (fakes) | 2026-10-31 | Claude | branch `feat/forward-latency-238` (part 4), Linux container, fakes and fake clock | ✅ 22 tests (`ReadPacingTests`, `PadForwarderTests`, `LatencyStatsTests`, the switch in `TakenOverControllersTests` and `SteamControllerXboxPadTests`, `Sampled` in `SdlGamepadSourceTests`); full suite green. Red once each: fast reads also while the pads rest (QE finding), a fallback wait cut to 0 ms (QE finding), without the one-slot wait behind a slow send. Not run: real pads, the Windows timer, CPU | owner's T238-11 on Windows |
| Latency check script (`checks/238-latency/`, v2) and `arcadia.log` | 2026-10-31 | owner, evaluated by Claude | Windows 11, owner's PC, branch `feat/forward-latency-238` at 4d9fb28 (Debug), DualSense USB, 8BitDo Ultimate 2 dongle, Steam Controller (puck, mode „Arcadia“), a game in the background, ~1 min per pad | ✅ real controller → emulated pad as a game reads it: DualSense (HID, exact) mean 0.80 ms, p99 1.60, max 3.04 (238 changes); 8BitDo (XInput, probe polls 1 kHz, so ±1 ms) mean 1.11, p99 1.56, max 2.17 (274); Steam Controller presses mean 1.14, p99 2.08, max 2.44 (105; releases not measurable: the probe merged SDL's two entries of the puck). Log: read → VIIPER mean 46–85 µs, p99 ≤ 140 µs; read gap mean 1.2–1.4 ms, p99 1.7–2.0 ms, steady-state max 10–21 ms (rare); CPU 0.13–0.37 % of all cores; copy puck → VIIPER mean 37 µs, p99 130 µs. Not run: the 10-minute session | Gaps over SDL's let-go for a hide no longer counted (they showed up to 0.95 s); 10-minute T238-11 still open |
