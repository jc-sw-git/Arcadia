# #213 Steam Controller as an Xbox controller in every game, without SISR

Part A (the pad itself, this PR); part B (Steam's layout, per-game mode "Arcadia" / "Steam-Eingabe", #119, no driver)
adds its cases here. The PR targets `feat/replace-sisr`. Until part B, Arcadia's pad runs only while "Steam Controller
Kompatibilität" is **off** (SISR off), and Steam's desktop layout is not silenced yet: with Steam running, a game outside
Steam may still get mouse and keys from the trackpads, so T213-1 runs with Steam closed.
Needed: the Steam Controller with its puck, an Xbox pad or the 8BitDo, usbip-win2 installed, `viiper.exe` in
`%LOCALAPPDATA%\SISR`. "Game controllers" = `joy.cpl` (Win+R). `arcadia.log`: `%APPDATA%\Arcadia\logs\arcadia.log`.

- [ ] **T213-1** Steam closed, Arcadia in the background, a game from another launcher in front (or `joy.cpl` →
      Properties): it sees exactly one Xbox controller; every button, both sticks, D-pad and both triggers (analog,
      not only on/off) arrive as on the Steam Controller. · E2E: yes (what reaches the pad; the real game is manual)
- [ ] **T213-2** In that game: ⧉ + ≡ pressed together and held switches the mouse mode and the game reacts to
      neither; a short ⧉ and a short ≡ act in the game (e.g. map, pause) when let go; ≡ held alone acts as held in the
      game (e.g. "hold to skip"); ⧉ held brings Arcadia back, and the game may see ⧉ held too. ≡ held, ⧉ added after
      a second: no mouse mode, the game gets both. Mouse mode "Aus": ⧉ and ≡ act at once. · E2E: yes
- [ ] **T213-3** Arcadia in front while the game runs, and the mouse mode on in the game: the game gets no input from
      the Steam Controller; back in the game it gets it again. · E2E: yes
- [ ] **T213-4** The Xbox pad (or 8BitDo) connected too: "Game controllers" lists it and one more Xbox controller
      (Arcadia's), nothing twice; the other pad's presses never come through Arcadia's pad. · E2E: yes
- [ ] **T213-5** Steam Controller off (Steam button held), on again, puck unplugged and plugged in: Arcadia's controller
      leaves "Game controllers" within about 2 s and comes back. "Steam Controller Kompatibilität" on: no pad of
      Arcadia's (only SISR's), off again: it comes back. `arcadia.log`: "Xbox-Pad: … angesteckt" / "abgesteckt". · E2E: yes
- [ ] **T213-6** `viiper.exe` ended in the Task Manager: the controller is back within a few seconds, Arcadia runs on.
      Arcadia closed normally: `viiper.exe` and the controller are gone. Arcadia ended in the Task Manager: `viiper.exe`
      ends with it; after the next start the controller is back and nothing is listed twice. · E2E: yes (the losses and
      the toast; the real processes are manual)
- [ ] **T213-7** Budget (Release build): buttons and sticks in the game feel as direct as with a wired Xbox pad; after
      unplugging, `arcadia.log` has "Verzögerung im Mittel … ms, höchstens … ms" (mean well under 50 ms). · E2E: no
      (real VIIPER and usbip)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T213-1 to T213-6 | 2026-10-26 | Claude | Branch `feat/steam-controller-xbox-pad-without-sisr-213`, Linux, `SteamControllerXboxPadTests` (headless, SDL and VIIPER faked), unit tests | ✅ green; each acceptance test was red once with its feature broken on purpose (named in the PR) | Owner's manual test |
| Codex review | 2026-10-26 | Codex | PR #231 at `af0bda3` | ❌ held state kept after SDL is lost; ≡ held alone reached the game although ⧉ added later still made the combo; a failed unplug skipped the server's end | Fixed: `Drop` on `Lost`, ⧉/≡ held back until let go, shutdown steps on their own; also send timeout and bus rollback. New tests red once (named in the PR) |
| Owner's decision | 2026-10-26 | Juri | – | ⧉/≡ held alone must reach the game; ⧉ + ≡ only within 200 ms | `ComboWithholding` and `MouseMode.ComboWindow` changed, T213-2 rewritten |
| T213-1 to T213-7 | 2026-10-26 | Claude | – | ➖ not runnable in the cloud (no Windows, no VIIPER, no controllers); `ViiperXboxPads` and the job object are only built, not run | Owner's PC |
