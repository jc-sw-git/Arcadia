# #403 "Spiele sehen" sometimes leaves out the connected Steam Controller

While Pan hides controllers or restarts the puck, Arcadia lets go of the Steam Controller and SDL doesn't see it. A
refresh of "SPIELE SEHEN" in that moment dropped it, and nothing refreshed the list when it was read again. Now it
stays listed while Arcadia lets go of it and for 3 s after, and the list refreshes when it comes or goes. A controller
left to games shows "Nicht übernommen".

- [ ] **T403-1** Pan on, Steam Controller on the puck, DualSense and 8BitDo Ultimate 2 connected. Start Arcadia,
      Einstellungen → Controller → "Controller zuordnen" (info panel): "SPIELE SEHEN" lists "Steam Controller (Modus
      „Arcadia“) → Xbox 360 Controller" first, then the DualSense and the 8BitDo. Repeat five times: switch Pan off and
      on, and plug the 8BitDo out and in; the Steam Controller stays listed every time. Switch the Steam Controller off:
      within about 3 s it leaves the list with the page open; switched on again, it comes back. Regression: the
      "Controller zuordnen" modal lists it locked as before. · E2E: yes (App `EveryControllerForGamesTests` T403-1, Core
      `SteamControllerSightingTests`; Pan's real hide timing is manual)
- [ ] **T403-2** In the modal, set the 8BitDo to "Nicht übernehmen" and apply: its value in "SPIELE SEHEN" reads
      "Nicht übernommen" (greyed), fully visible. · E2E: yes (App `EveryControllerForGamesTests` T238-13)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T403-1, T403-2 (automated) | 2026-11-02 | Claude | branch build, Linux cloud container, fakes for SDL, Pan and controllers | ✅ green; T403-1 red with the list reading SDL's view directly | Real puck, Pan's hide and the panel's look need the owner's test |
