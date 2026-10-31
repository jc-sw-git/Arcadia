# #238 Pass every controller to games through Arcadia

Pan hides every controller Arcadia reads, and Arcadia gives each one its own emulated pad
([every-controller.md](../../controller-support/every-controller.md)). Cut into parts: **part 1** (this file so far)
passes every controller on as an Xbox 360 pad with combos, rest, Guide, rumble, failure handling and the D7/D8 rows;
part 2 the 1:1 kinds (DualShock 4, DualSense, Switch 2 Pro), part 3 "Controller zuordnen" (T238-13), part 4 the
latency budget (T238-11). Until part 2, read "Arcadia's DualSense" below as "Arcadia's Xbox 360 pad".

T335-1 to T335-3 (Pan's hiding on real hardware, `335-pan-hides-every-controller.md`) run with this build too.

- [ ] **T238-1** DualSense (USB) and 8BitDo Ultimate 2 (dongle) connected, Arcadia starts: a test game and Steam see
      exactly one pad of Arcadia's per controller, in connection order, and neither real pad. The 8BitDo (XInput) is
      emulated only after it is reconnected once (Windows restarts its XInput interface only then); until then the
      D7 row names it. Note the XInput slot Arcadia's pad gets (known limit: the hidden pad keeps slot 0).
      · E2E: no (real pads, HidHide); the flow with fakes in `EveryControllerForGamesTests`
- [ ] **T238-2** As T238-1 with the DualSense by Bluetooth, switched on while the game runs: the game sees one new pad,
      never two. · E2E: no (real pads)
- [ ] **T238-3** Back + Start and ⧉ held with each pad in a running game: Arcadia reacts, the game's input log shows
      neither combo; a short ⧉ reaches the game. · E2E: yes (`EveryControllerForGamesTests`)
- [ ] **T238-4** Arcadia in front, then the mouse mode on: the game reads rest from every pad. · E2E: yes
      (`EveryControllerForGamesTests`)
- [ ] **T238-5** Rumble in a game reaches DualSense, 8BitDo and Steam Controller and stops with the game's rumble; Guide /
      PS opens Steam's overlay or the Game Bar. Touchpad, gyro and adaptive triggers: part 2. · E2E: no (real
      hardware); the path from the pad's output to SDL with fakes in `EveryControllerForGamesTests`
- [ ] **T238-6** "Nein" in the takeover question: Steam Controller and puck stay Steam's, the DualSense is still hidden
      and arrives as Arcadia's pad. · E2E: yes (`EveryControllerForGamesTests`)
- [ ] **T238-7** A controller Pan can't hide: it reaches the game directly, no pad of Arcadia's; the info row
      "… lässt sich nicht verstecken" lists it with its reason. Pan off: every pad reaches the game directly and the row
      is gone. · E2E: yes (`EveryControllerForGamesTests`), by hand with the 8BitDo before its reconnect
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

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T238-1, -3 to -7, -12 (fakes) | 2026-10-31 | Claude | branch `feat/emulate-every-controller-238` (part 1), Linux container, Avalonia headless, fake SDL, Pan and VIIPER | ✅ 7 acceptance tests, 14 `TakenOverControllersTests`; full suite green (Core 2156, Pan 279, App 466) | Mutation check: dropping the rejection check from "hidden" turns T238-7 and T238-12 red, dropping the D2 condition T238-6 |
| T238-3, -4 (fakes), QE review | 2026-10-31 | Claude (QE) | branch `feat/emulate-every-controller-238` at 265c32e, Linux container, Avalonia headless, fakes | ✅ 8 acceptance tests incl. the new T238-3 test for the second pad; full suite green (Core 2156, App 466) | Red once: without `ComboWithholding` in `Feed` both T238-3 tests fail, without the rest while Arcadia is in front T238-4 fails |
| QE findings 1–3 | 2026-10-31 | Claude | branch `feat/emulate-every-controller-238` after 0b406a3, Linux container; QE's probe `checks/338/` with Viiper.Client 0.9.0 over loopback | ✅ with the read's token cancelled `DisposeAsync` returns (without: hangs); new tests for the firmware tool retry and the Steam Controller's pad at rest, each red once without its fix | – |
| real pads, Pan, VIIPER | – | – | – | not run: no Windows with pads in the cloud container | owner's test of part 1 |
