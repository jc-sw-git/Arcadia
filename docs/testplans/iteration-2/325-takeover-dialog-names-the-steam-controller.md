# #325 Pan: takeover dialog at Arcadia's start shows only the puck

At Arcadia's start SDL reports the wireless Steam Controller only after the puck, and the question "… übernehmen?"
kept the parts it was opened with. Now the open question follows what is connected, and a change stops a running hold
on "Ja", so the player confirms what the dialog shows.

- [x] ~~**T325-1**~~ (outdated by #295: the question is gone) Question asked with only the puck, then the controller is reported: the question names both, a
      moment with nothing reported keeps what it named, nothing is hidden meanwhile. · E2E: no (Core
      `PanEmulationTests`)
- [x] ~~**T325-2**~~ (outdated by #295: the question is gone) Steam running, Pan on, puck and Steam Controller on, Arcadia started: the dialog reads "Steam
      Controller und Puck übernehmen?" with the chips "Steam Controller", "Puck", "Steam läuft" (it may read "Puck
      übernehmen?" for a moment first). Holding "Ja" while it changes stops the hold; a new 3 s hold takes both over.
      · E2E: yes (`PanEmulationTests` in App; on hardware the change is too quick to hold "Ja" through it)

## Test runs during implementation

- Linux cloud container, .NET 10.0.114: `dotnet build` without warnings, `dotnet test` green (App 450, Core 2094,
  Pan 199); T325-1 is red without the fix. Not run on Windows with a real puck and controller: T325-2 on hardware is
  left for the owner.
- Owner's PC (Windows, puck and Steam Controller, Steam running): T325-2 passed.
