# #212 Read the Steam Controller directly with SDL3

Controllers at hand: the new Steam Controller with its puck (`28DE:1304`), an Xbox pad or the 8BitDo Ultimate 2
(owner). "Fully control" as in [072](072-support-every-controller-via-sdl3.md). A Steam game on Steam Input: e.g.
Castlevania: Belmont's Curse (4231820), as in check P1. `arcadia.log`: `%APPDATA%\Arcadia\logs\arcadia.log`.
The PR targets `feat/replace-sisr`; SISR is still there, so "SISR runs" means "Steam Controller Kompatibilität" on.

- [ ] **T212-1** A Steam game on Steam Input in front, Arcadia in the background (SISR paused for it): with the Steam
      Controller, View held brings Arcadia back and Back + Start switches the mouse mode (and rumbles the Steam
      Controller). The game keeps getting the controller from Steam Input, nothing odd in it. · E2E: no (real game and Steam Input)
- [ ] **T212-2** Arcadia brought to the front while that game runs: the Steam Controller fully controls Arcadia, each
      press acts once; "Steams Desktop-Belegung ignorieren" on: no mouse pointer moving from the trackpads, no keys
      from Steam's desktop layout (arrows, Esc, Page Up/Down, Shift, the Windows key). · E2E: yes (the operation;
      Steam's desktop layout needs the real Steam)
- [ ] **T212-3** "Steam Controller Kompatibilität" on, SISR running, Arcadia in front: each press of the Steam
      Controller acts once. `arcadia.log` has "ignoriert (virtueller Xbox Controller, z. B. von SISR)" for SISR's
      pad and no line "unklar, ob XInput#… virtuell ist". · E2E: yes (the device tree faked; the real one is the manual part)
- [ ] **T212-4** Steam closed (SISR off): the Steam Controller fully controls Arcadia. The right trackpad moves the
      mouse before Arcadia starts, not while Arcadia runs, and again after Arcadia is closed (lizard mode). · E2E: no
      (lizard mode is the controller's own)
- [ ] **T212-5** While Arcadia runs: switch the Steam Controller off (Steam button held) and on again, then unplug
      the puck and plug it in again. Each time it works again once SDL sees it (3–6 s, P2), and with SISR on the
      per-game mode in the preview goes and comes within 2 s of that. `arcadia.log` has "Controller #n getrennt" /
      "verbunden" with `(Steam, 28DE:1304)`. · E2E: yes (SDL's events faked)
- [ ] **T212-6** The Steam button, "…" and the four back grips do nothing in Arcadia; Steam's menu still opens on the
      Steam button (Steam running). Trackpad clicks do nothing either. · E2E: yes (Steam's menu is the manual part)
- [ ] **T212-7** The Xbox pad (or 8BitDo) connected too, Steam-Eingabe on for it: each pad counts once, with SISR on
      and off; the other pad behaves exactly as in T72-1 to T72-3. · E2E: yes
- [ ] **T212-8** Budget (Release build): idle CPU of Arcadia < 1 % over 60 s with the Steam Controller connected and
      untouched (it sends about 840 events a second; `Get-Counter '\Process(Arcadia)\% Processor Time'`). · E2E: no
      (measurement on the owner's PC)
- [ ] **T212-9** Arcadia started through Steam (as a non-Steam shortcut): `arcadia.log` says "Arcadia wurde über
      Steam gestartet"; nothing else is expected of the Steam Controller there (out of scope). · E2E: no (Steam)
- [ ] **T212-10** "Steam Controller Kompatibilität" on: the per-game mode shows in the preview as soon as the Steam
      Controller is on, also while SISR is paused for a game; off: it doesn't show. · E2E: yes

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T212-2, T212-3, T212-5, T212-6, T212-7, T212-10 | 2026-10-25 | Claude | Branch `feat/steam-controller-sdl3-212`, Linux, `SteamControllerTests` (headless, SDL and the device tree faked), unit tests | ✅ green; each acceptance test was red once with its feature broken on purpose (named in the PR) | Owner's manual test |
| Device tree, LED slot | 2026-10-25 | Claude | – | ➖ `XusbDevices` (SetupAPI, cfgmgr32, `IOCTL_XUSB_GET_LED_STATE`) can't run in the cloud; only its decision logic is tested | T212-3 on the owner's PC; if the log says "unklar", the slot read doesn't work there |
| First test | 2026-10-25 | Juri | PR #225, owner's PC, Windows, Steam running | ✅ the Steam Controller works without SISR. ❌ Steam's trackpad pointer still moved a bit on each release (Steam glides on longer than 150 ms). ❌ Back + Start also switches Steam's action set (Desktop/Gamepad) | 1 s grace after a trackpad touch; Back + Start goes to #93 (Steam switches the set internally, nothing to filter). Neither Steam's `controller.txt` nor `SteamClient.Input` callbacks over CEF showed the action set |
| T212-1 to T212-10 | 2026-10-25 | Claude | – | ➖ not runnable in the cloud (no Windows, no controllers) | Owner's PC |
