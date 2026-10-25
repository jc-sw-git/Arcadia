# SISR and the Steam Input of other controllers

Verified on the owner's PC. Overview and the other files: [README](README.md).

- SISR emulates every controller Steam Input hands it, not only the Steam Controller (SISR `input/device_store.go`,
  read 2026-10-20). With Steam Input on for Xbox pads in a game's controller settings, SISR took the owner's
  physical Xbox pad as soon as the game started, Steam took SISR's virtual Xbox 360 controller over again, and
  the game got no input from it in either mode until the support was switched off (owner, 2026-10-20, #124).
  So only an emulated device whose `real_device` in `/api/v1/devices` is a Valve device counts as a Steam
  Controller: `VID_28DE` in its SDL path, except Steam's virtual gamepad `28DE:11FF`; without a vendor id in the
  path, a name with "Steam Controller" or "Steam Deck" (`SisrGamepad.IsValveController`). An Xbox pad behind
  Steam Input has SDL's XInput path (`XInput#0`) without a vendor id (seen, below); what SISR reports for the
  Steam Controller isn't seen yet. `arcadia.log` names every other controller SISR emulates.
- In a Steam game whose Steam Input also takes other controllers, the game gets no input from them while SISR
  runs, whether SISR emulates them yet or not. Owner's PC, 2026-10-21, Castlevania: Belmont's Curse (4231820)
  set to Steam Input for every controller, 8BitDo Ultimate 2 (an Xbox-style pad) and the Steam Controller, game
  on "Xbox Controller (SISR)": once SISR never took the pad within a minute, once only 85 s after the start,
  and the game ignored the pad all along; after SISR stopped it worked. SISR reports such a pad as `XInput
  Controller #1` or by its name, path `XInput#…`, no USB id. When SISR starts outside a game it sees the pad as
  a Steam virtual gamepad for 2 s, until it forces its marker's layout (sisr.log).
- So Arcadia reads Steam's setting for the game from the account's `localconfig.vdf`
  (`UserLocalConfigStore/apps/<appid>/UseSteamControllerConfig`: "0" off, "1" or missing Steam's default, "2" on
  for every controller; "2" for that game, the only values seen) and the default's global
  `SteamController_XBoxSupport` / `SteamController_GenericGamepadSupport` (`SteamInputSetting`). How Steam
  stores Steam Input for only some controller types isn't known yet; `arcadia.log` names the values read. If
  it covers other controllers ("2", or the default with Xbox or generic support on), SISR pauses for that
  game even on "Xbox Controller (SISR)": before a start from Arcadia (`BeginGameAsync`), or within 2 s for a
  Steam game that runs otherwise (`RunningAppID`, read once per game). In such a game Steam Input brings the
  Steam Controller too. As a fallback, SISR also pauses while a Steam game runs if it emulates any other
  controller (Steam's default can be changed by the game itself). The settings show "SISR pausiert für „…“"
  with the way out (Steam Input off for the other controllers in that game); Arcadia can't make SISR leave one
  device alone (only a filter in SISR or #112 could). A pause noticed during the game isn't handed over an
  update restart; the new Arcadia pauses again within 2 s. Without a Steam game the settings only warn
  "Anderer Controller verwendet Steam-Eingabe" (the owner's wording, 2026-10-22: German Steam says "Steam-Eingabe", never "Steam Input"; the pad's own name before SISR's re-taken pads named `#controller_…` or `XInput Controller #n`, `SisrGamepad.HasStandInName`): with Steam Input on for a whole controller type
  (Steam → Einstellungen → Controller → Erweiterte Controllereinstellungen) SISR takes that pad outside any game
  and the Steam Controller stops working (owner's PC, 2026-10-22, #143). SDL then names the pad by Steam's
  untranslated type, e.g. `#controller_xbox360`; `SisrGamepad.DisplayName` turns it into plain words. Steam then
  takes SISR's virtual pads again and SISR emulates those too (`#controller_xbox360`, `XInput Controller #2`):
  in `arcadia.log` such pads held both sticks up, or the D-pad down or right, for minutes, so Arcadia's hints kept
  jumping back to the controller. SISR creates its VIIPER device for a pad only on the pad's first input
  (SISR 0.6.2, `event/handler/gamepadupdated`), so emulated devices alone showed the problem only after someone
  touched the pad; `/api/v1/devices` lists `steam_virtual_device` for every pad Steam Input hands to SISR before
  that (`SisrDevice.IsUnderSteamInput`). So once SISR reports another controller under Steam Input in two checks
  in a row outside a Steam game (not in the first 5 s after its start, while it forces its marker's layout), SISR pauses ("SISR pausiert wegen eines anderen Controllers", with "Erneut versuchen"). The pause ends when Steam's
  `SteamController_XBoxSupport` / `SteamController_GenericGamepadSupport` go off (if one was on when it began;
  read from `localconfig.vdf` only when the file changed) or with "Erneut versuchen". Where Steam stores the same
  for PlayStation and Nintendo pads isn't known; `localconfig.vdf` only lists `ControllerTypesUsed`.
- On the owner's PC (2026-10-25, #72 test) SISR several times reported the DualSense as under Steam Input although
  Steam Input was off for it, so SISR stayed paused; restarting SISR ended it. Probably a hang in SISR itself (cause
  not checked); it goes away with SISR (#212, #213).
