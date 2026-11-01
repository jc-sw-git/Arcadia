# #371 Cancellation token for monitor polls; a failing or hanging monitor holds no one

No manual test: the failure needs a monitor that throws (none of the built-in ones does) or a launcher folder on a share
that goes away mid-poll; unit tests cover each case with fakes.

- `InstallTrackerTests`: a throwing monitor keeps its progress, the others apply, logged once; a hanging monitor holds
  neither the poll nor the others, isn't asked again while it hangs, and its late answer still counts.
- `RunningGamesTrackerTests`: a failing launcher is logged once; a hanging one keeps its games, the others apply.
- `LibraryServiceTests`: a scan that ignores its token ends at `ScanDeadline`, the other launchers load.
- `PanEmulationTests`: at exit a tick in a long hide is cancelled and the cleanup runs; a tick that ignores the cancel
  lets the exit go after `ShutdownGateTimeout`, with nothing cleaned up beside it.

## Test runs

| Date | Environment | Result |
|---|---|---|
| 2026-11-01 | Linux cloud container, .NET SDK 10.0.114, `dotnet test Arcadia.slnx` | all green |
