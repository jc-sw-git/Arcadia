# #213 Steam Controller as an Xbox controller in every game, without SISR

Part A (the pad itself, PR #231) and part B (per-game mode "Arcadia" / "Steam-Eingabe", #119, no driver, below); both
PRs target `feat/replace-sisr`. Steam's desktop layout is not silenced yet: with Steam running, a game outside Steam may
still get mouse and keys from the trackpads, so T213-1 runs with Steam closed.
Needed: the Steam Controller with its puck, an Xbox pad or the 8BitDo, usbip-win2 installed, `viiper.exe` in
`%LOCALAPPDATA%\SISR`. "Game controllers" = `joy.cpl` (Win+R). `arcadia.log`: `%APPDATA%\Arcadia\logs\arcadia.log`.

- [x] **T213-1** Steam closed, Arcadia in the background, a game from another launcher in front (or `joy.cpl` →
      Properties): it sees exactly one Xbox controller; every button, both sticks, D-pad and both triggers (analog,
      not only on/off) arrive as on the Steam Controller. · E2E: yes (what reaches the pad; the real game is manual)
- [x] **T213-2** In that game: ⧉ + ≡ pressed together and held switches the mouse mode and the game reacts to
      neither; a short ⧉ and a short ≡ act in the game (e.g. map, pause) when let go; ≡ held alone acts as held in the
      game (e.g. "hold to skip"); ⧉ held brings Arcadia back, and the game may see ⧉ held too. ≡ held, ⧉ added after
      a second: no mouse mode, the game gets both. Mouse mode "Aus": ⧉ and ≡ act at once. · E2E: yes
- [x] **T213-3** Arcadia in front while the game runs, and the mouse mode on in the game: the game gets no input from
      the Steam Controller; back in the game it gets it again. · E2E: yes
- [x] **T213-4** The Xbox pad (or 8BitDo) connected too: "Game controllers" lists it and one more Xbox controller
      (Arcadia's), nothing twice; the other pad's presses never come through Arcadia's pad. · E2E: yes
- [x] **T213-5** Steam Controller off (Steam button held), on again, puck unplugged and plugged in: Arcadia's controller
      leaves "Game controllers" within about 2 s and comes back. "Steam Controller Kompatibilität" on: no pad of
      Arcadia's (only SISR's), off again: it comes back. `arcadia.log`: "Xbox-Pad: … angesteckt" / "abgesteckt". · E2E: yes
- [x] **T213-6** `viiper.exe` ended in the Task Manager: the controller is back within a few seconds, Arcadia runs on.
      Arcadia closed normally: `viiper.exe` and the controller are gone. Arcadia ended in the Task Manager: `viiper.exe`
      ends with it; after the next start the controller is back and nothing is listed twice. · E2E: yes (the losses and
      the toast; the real processes are manual)
- [x] **T213-7** Budget (Release build): buttons and sticks in the game feel as direct as with a wired Xbox pad; after
      unplugging, `arcadia.log` has "Verzögerung im Mittel … ms, höchstens … ms" (mean well under 50 ms). · E2E: no
      (real VIIPER and usbip)

Part B (per-game mode, #119, no driver, SISR no longer started). Arcadia no longer starts SISR, so "Steam Controller
Kompatibilität" doesn't matter for these cases. Keeping Steam away from the controller (Steam's desktop layout silent
beside a game on "Arcadia", Back + Start not switching Steam's action set, #93) comes with spike #239's decision and
adds its cases then; until then, with Steam running, a game on "Arcadia" may still get mouse and keys from Steam.

- [ ] **T213-8** No switch to turn on: with the Steam Controller on, the preview, the detail page and the context menu
      show "Steam Controller: Arcadia" on a game of another launcher and "Steam-Eingabe" on a Steam game; the Steam
      Controller off, the row goes. With only an Xbox pad, no row. · E2E: yes
- [ ] **T213-9** A Steam game (Steam-Eingabe) started from Arcadia: "Game controllers" no longer lists Arcadia's Xbox
      controller before the game is up, the game uses its own Steam configuration; after the game ended, Arcadia's
      controller is back within about 2 s. `arcadia.log`: "Steam Controller in Spielen: kein Xbox-Pad … auf Steam-Eingabe".
      · E2E: yes
- [ ] **T213-10** A game of another launcher on "Arcadia" keeps Arcadia's controller while it runs; set to "Steam-Eingabe"
      in its preview (A on the value, or ⧉ → context menu), the next start runs without it. While a game runs, its mode
      can't be changed ("Gilt erst beim nächsten Start …"). · E2E: yes
- [ ] **T213-11** (#119) A Steam game started directly in Steam (not from Arcadia), also one not in Arcadia's library:
      Arcadia's controller goes within about 2 s; set that game to "Arcadia" in Arcadia first and start it in Steam
      again: the controller stays. · E2E: yes
- [ ] **T213-12** Without the driver (usbip-win2 uninstalled, or `viiper.exe` renamed in `%LOCALAPPDATA%\SISR`): the
      mode shows "Steam-Eingabe" greyed out with the line "„Arcadia“ braucht den usbip-Treiber und VIIPER – …", A and
      a click do nothing, no Xbox controller of Arcadia's appears; the driver back, the mode and the controller come
      back by themselves. · E2E: yes
- [ ] **T213-13** SISR no longer starts: with "Steam Controller Kompatibilität" on from before, Task Manager shows no
      `SISR.exe` after Arcadia's start; a SISR started by hand before Arcadia is ended by it. The setting's row shows
      "Nicht mehr nötig – …" once SISR is set up. · E2E: yes

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T213-1 to T213-6 | 2026-10-26 | Claude | Branch `feat/steam-controller-xbox-pad-without-sisr-213`, Linux, `SteamControllerXboxPadTests` (headless, SDL and VIIPER faked), unit tests | ✅ green; each acceptance test was red once with its feature broken on purpose (named in the PR) | Owner's manual test |
| Codex review | 2026-10-26 | Codex | PR #231 at `af0bda3` | ❌ held state kept after SDL is lost; ≡ held alone reached the game although ⧉ added later still made the combo; a failed unplug skipped the server's end | Fixed: `Drop` on `Lost`, ⧉/≡ held back until let go, shutdown steps on their own; also send timeout and bus rollback. New tests red once (named in the PR) |
| Owner's decision | 2026-10-26 | Juri | – | ⧉/≡ held alone must reach the game; ⧉ + ≡ only within 200 ms, then 100 ms (less delay for ⧉ and ≡) | `ComboWithholding` and `MouseMode.ComboWindow` changed, T213-2 rewritten |
| T213-1 to T213-7 | 2026-10-26 | Claude | – | ➖ not runnable in the cloud (no Windows, no VIIPER, no controllers); `ViiperXboxPads` and the job object are only built, not run | Owner's PC |
| T213-1 to T213-7 | 2026-10-26 | Juri | PR #231 at `a22e208`, Windows, owner's PC with Steam Controller, VIIPER and usbip-win2 | ✅ looked fine | Ready to merge into `feat/replace-sisr` |
| T213-8 to T213-13 | 2026-10-26 | Claude | Branch `feat/steam-controller-mode-per-game-213`, Linux, `SteamControllerModeTests` and the rewritten mode tests (headless, SDL, VIIPER, Steam's `RunningAppID` faked), unit tests | ✅ green; each new acceptance test was red once with its feature broken on purpose (named in the PR) | Owner's manual test once #239 decided the layout part |
| T213-8 to T213-13 | 2026-10-26 | Claude | – | ➖ not run on Windows (no VIIPER, no controller, no Steam in the cloud) | Owner's PC |
