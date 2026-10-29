# #307 A demo game shown as running no longer locks the Pan switch

In demo mode (`--demo`) one game per launcher shows "Läuft" although nothing runs, and it can't be ended. It used to keep
"Controller-Wächter (Pan)" locked ("Erst änderbar, wenn „…“ beendet ist") for the whole session. A monitor now says its
games are simulated (`IRunningGamesMonitor.ReportsSimulatedGames`), and the tracker's `RealStateOf` leaves them out of
the lock and out of `ActiveGames`, which steers the Steam Controller. Needed: Windows, a build of this PR, a Pan
installed (drivers set up), a Steam Controller or puck connected.

Automated: `PanEmulationTests` (T307-1, T307-2: real game started through a fake launcher), unit test in
`RunningGamesTrackerTests`.

- [x] **T307-1** Start with `--demo`: a game shows "Läuft". Settings → Controller: the row "Controller-Wächter (Pan)" is not
      greyed out, shows no "Erst änderbar …", and [A] switches Pan off and on again; the emulation follows. · E2E: yes
- [x] **T307-2** Without `--demo`, start a game from a real launcher: the row is locked with "Erst änderbar, wenn „…“
      beendet ist" until the game ends. · E2E: yes (acceptance test with a fake launcher; the real launcher by hand)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T307-1 | 2026-10-29 | Claude | PR branch, Debug, Linux cloud container, acceptance test (Pan faked); red once by counting demo games as running again; after QE review also red for the demo game steering the Steam Controller | ✅ | owner's PC open |
| T307-1, T307-2 | 2026-10-29 | Owner | Owner's PC, build of this PR; passed by the owner's word, no details recorded | ✅ | merge |
