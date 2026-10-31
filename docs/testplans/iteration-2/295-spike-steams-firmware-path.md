# #295 Spike: Steam's firmware update path for the Steam Controller, and what Arcadia guards

The spike on the owner's PC showed that Steam's update never reaches the real Steam Controller while Arcadia's copy
runs (`docs/controller-support/steam-controller-firmware.md`). So the consent question, the gap rule, the settings row
"… übernehmen" and Pan's firmware hold are gone; Pan instead neither hides nor restarts the puck while a bootloader is
connected, and the firmware dialog is a notice without a wait. Outdated by this: T242-8, T242-20 to T242-25, T325-1,
T325-2. Needed: Windows, a build of this PR (Pan protocol 3, so Arcadia offers the Pan update first), Steam, the Steam
Controller with its puck and a USB cable, a firmware update Steam offers.

Automated: Pan `HidingTests` (T295-1), Core `PanEmulationTests`, `TakenOverControllersTests`, `FirmwareAttemptWatchTests`,
`SteamControllerCopyFilterTests` (T295-2, T295-4), App `PanEmulationTests`, `EveryControllerForGamesTests`,
`PanUpdateAtStartTests`, `Window/HoldButtonFillTests` (T295-2, T295-3, T295-5, T295-6).

- [ ] **T295-1** Released ("Controller freigeben"), the update started in Steam by cable: `pan.log` says "A Valve
      bootloader is connected …" while `28DE:1005` is there and restarts nothing; the update finishes and Steam shows
      the new firmware. Pan switched off and on afterwards: taken over again, the puck restarts once. · E2E: no (real
      bootloader and flash)
- [ ] **T295-2** Taken over, the library in front, the update started in Steam: Arcadia comes to the front with "Steam
      will die Firmware aktualisieren", "Schließen" focused, all three buttons usable at once; Steam reports an error
      and nothing reaches the controller. [B] closes it, the controller stays Arcadia's; starting the update again
      after 30 s brings it back. "Arcadia beenden" (held 1 s) closes Arcadia. · E2E: yes (Steam's real update by hand)
- [ ] **T295-3** "Controller freigeben" (held 1 s): "Steam Controller und Puck freigegeben" with how to go on; Steam
      sees the real controller; Einstellungen → Controller: "An – Steam Controller und Puck freigegeben, bis du Pan
      aus- und wieder einschaltest", no triangle, no row below; other controllers stay taken over. · E2E: yes
- [ ] **T295-4** Taken over, Steam + Y: the controller switches off, no firmware notice. · E2E: no (real controller)
- [ ] **T295-5** A library game in front, the update started in Steam (overlay or Alt+Tab to Steam and back before it
      fires): a notice over the game, Arcadia stays behind. · E2E: yes (Steam's real update by hand)
- [ ] **T295-6** Steam running, Arcadia started: Steam Controller and puck are taken over without a question.
      · E2E: yes

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| Spike recording (cable copy, firmware path, Steam + Y) | 2026-10-31 | owner | `feat/replace-sisr`, Windows, puck and Steam Controller by cable, scripts `spike-295/Run-Check-295-cable-copy.ps1`, `Record-FirmwarePath-295-v6.ps1` | ✅ | `0x90` dropped ×5 at the cable copy, `hardwareupdater.exe` crashes on the puck copy, flash in `1005` for ~21 s after release, `0x9F` dropped (false firmware dialog) → this change |
| T295-1 to T295-6 (automated) | 2026-10-31 | Claude | branch `feat/simplify-firmware-guard-295`, Linux container, fakes, Avalonia headless; Pan 282, Core 2159, App 477 | ✅ | the notice not looked at on screen: Pan's emulation doesn't run off Windows; Windows run open |
