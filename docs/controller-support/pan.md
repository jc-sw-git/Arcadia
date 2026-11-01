# Pan, Arcadia's guardian service (#243)

Pan gives the real controllers back to every program when Arcadia ends unexpectedly or hangs: it ends the forwarding
(`viiper.exe`, usbip ports) and shows the real puck again ([pan-hiding.md](pan-hiding.md)). It was designed with
failure cases first; what it deliberately doesn't guard against: [pan-risks.md](pan-risks.md).
Once nothing emulates, it removes the stale device entries Arcadia's emulated devices leave in Windows
([pan-stale-devices.md](pan-stale-devices.md)).

## Pieces

- `src/Arcadia.Pan.Core` (no dependencies, AOT-clean): protocol (`PanMessage`), decisions (`Guardian`), journal
  (`JournalFile`), teardown (`TeardownPlan`, `TeardownRunner`), installer signature check (`InstallerSignature`,
  `WinTrustAuthenticode`, `UsbipRelease`), `UsbipPorts`.
- `src/Arcadia.Pan` → `Pan.exe`, Native AOT, published by `Pack.ps1` to `Pan\Pan.exe` beside `Arcadia.exe` (needs the
  C++ build tools; the CI's Windows runner has them; a Debug build on Windows publishes a framework-dependent one,
  `PublishPanForDebug`, `-p:ArcadiaSkipPan=true` skips it): `ServiceHost` (SCM, pre-shutdown, power resume),
  `PipeServer`, `ServiceSetup` (`--install`, `--uninstall`), `UsbipInstall`, `Restore` (`--restore`), `WindowsCleanup`,
  `PanLog` (`%ProgramData%\Arcadia\Pan\pan.log`, 1 MB rotation). `--console` runs it in a console, `--version` prints
  version and protocol.
- `src/Arcadia.ControllerSupport/Pan`: `PanClient` (`IEmulationGuardian`), `PanPipeTransport`, `GuardedXboxPads` (the
  decorator that registers every plug with Pan), `PanSetup` (runs the bundled `Pan.exe` elevated). The App wires it in
  `SteamControllerForGames.Create`; `SteamControllerXboxPad.FeedAlive` pulses the heartbeat on every poll.

## Identity and protocol

- Named pipe `\\.\pipe\ArcadiaPan`, fixed-size messages (kind, protocol version, generation, value); the commands and
  replies are in `PanMessage`. A command a Pan doesn't know gets `Rejected(NotRegistered)` or `Unsupported`; the client
  reads any reply but the expected one as "not supported", so new commands stay within the version. Since #304 the
  protocol is version 2 (`EmulationStarted` carries the usbip port).
- Pan takes the first client of the active console session (`ProcessIdToSessionId` == `WTSGetActiveConsoleSessionId`);
  a second client or one from another session gets `Refused`. The pipe's DACL: SYSTEM full, Interactive read/write,
  nobody else; `FILE_FLAG_FIRST_PIPE_INSTANCE`, so nobody can squat the name (a taken name is retried every 5 s,
  logged). Arcadia checks that the server's PID is the service's (`OpenService` with `SERVICE_QUERY_STATUS`, no
  elevation) before it says hello.
- The protocol has no correlation: a request's timeout means the link is gone, since a late reply would answer the
  next request. Don't queue more than the pipe buffer holds (192 bytes, 16 messages).

## Generations, heartbeat, teardown

- Every emulation has a generation: `EmulationStarted` returns the next number; Pan replies `WrongGeneration` to
  anything about another one. After a teardown the client must `TeardownAcknowledged` before the next emulation;
  `PanClient` does that itself. While Pan shows the real puck again it answers `Rejected(SteamControllerBeingShown)`;
  the client then says "Pan räumt noch auf" for 10 s, so the pad waits without counting a loss.
- Heartbeat every 500 ms, but only when the pad's `Feed` moved: a stuck poll thread counts as a hang. No heartbeat for
  3 s (`QueryUnbiasedInterruptTime`, sleep excluded) → teardown `Hang`; the pad comes back on the next tick as a new
  generation (one pad loss for the 3-in-5-minutes rule). `PlannedExit` (Arcadia's normal shutdown) unregisters
  without a teardown. Power resume restarts the hang clock and gives Arcadia 30 s (`Guardian.ResumeGrace`) before
  silence counts: Arcadia and the puck took longer than 3 s to run again after sleep.
- Client gone (pipe broke or process ended: Pan holds the client's process handle) → teardown `ClientGone`. Service
  stop or pre-shutdown while emulating → teardown `ServiceStopping`, run while the pipe is still open; its `TornDown`
  notice gets 2 s to go out. While a teardown runs, heartbeats and a new `EmulationStarted` get
  `TeardownNotAcknowledged`, reported as "Pan räumt noch auf" (one loss, not one per tick).
- `EmulationStarted` comes after Arcadia's VIIPER started and before any device goes on it; Pan takes every child of
  the client named `viiper.exe` (parent PID + start time) and holds its handle, so a PID can't be reused under it. It
  takes the start only if one of them alone listens on the usbip port named, only on 127.0.0.1 (IPv4 and IPv6,
  `ViiperListeners.OnlyServedBy`), else lets them go and refuses (`NoForwardingProcess`, `UsbipPortNotServed`), and
  journals that port per generation (`UsbipServers`, #304). Nothing is ever ended by a name lookup alone.
- Teardown steps, each with a 10 s timeout on a worker, never throwing: detach the devices imported from the journaled server
  ports, never another program's (`usbip.exe` from `%ProgramFiles%\USBip`; a server leaves the journal only when all
  its detaches worked), end the held forwarding processes (`TerminateProcess`: VIIPER keeps no state worth
  saving), then show the puck. Each ended process leaves the journal at once (under the guardian's lock, so a step
  left behind after its timeout can't corrupt it); what failed stays listed and goes into the next teardown.
- A restarted Pan (crash, reboot, `--restore`) always undoes its journal first; held processes are re-verified by PID
  + start time, the client itself is never killed.
- Pan stops itself after 30 s without a client (nothing idles in the background), unless a retry still has to show
  the puck. Arcadia starts it on connect (`ServiceController.Start`; users have `SERVICE_START` by the DACL), waiting
  for one still stopping itself. One that ends at once after Arcadia's start counts as permanent (15 s retry), and a
  service stopped with a Win32 exit code (data folder refused, crash) gives the row "Wächterdienst Pan startet nicht"
  with "Neu einrichten" instead of a start every 2 s. Recovery: 3 restarts, 1 s, reset daily, also on non-zero exit.

## Journal

`%ProgramData%\Arcadia\Pan\journal.json`, written before `Started` is answered (temp + write-through + `File.Replace`,
`.previous` kept, a broken one moved to `.broken` and reported as `JournalUnreadable` → toast). A journal that parses
but makes no sense (null list, entry without a process, unknown version, a hidden device that isn't a puck) counts as
unreadable too. A missing journal beside a readable `.previous` counts as that one, and the fallback a load used is
written back at once, so a second load sees the same state: deleting `journal.json` by hand is no reset, delete
`.previous` with it. The journal is never deleted: with nothing to undo it still keeps the generation, so a restarted
Pan never repeats a number a client remembers (a reinstalled Pan with the journal gone may).

A journal change stays readable by the Pan before: new fields only, same `Version` (an older Pan ignores what it
doesn't know). The update at Arcadia's start may install an older Pan (T297-9); one that couldn't read the journal
would count its generations from 0 again and lose what is left to undo. So a journal without `UsbipServers` is one a
Pan from before #304 wrote: with an open emulation it gets VIIPER's old default port 3241, as that Pan detached it.

## Cost

Release build (Native AOT) on the owner's PC (32 logical cores), Arcadia connected, a game on "Arcadia" for 19 min:
Pan 0.001 % CPU of all cores (0.003 % without a game), Arcadia 0.30 %, VIIPER 0.26 %. Pan's private working set grew
about 1 MB a minute from 7 MB to 22.1 MB in 23 min and stayed there for the last 4 min: no leak, the GC's gen-0 budget.
Without a setting it follows the CPU's L3 cache (about half of it: 16 MB with 32 MB of L3), so the heartbeat's small
garbage piles up to that before the first collection. Pan sets `System.GC.Gen0MaxBudget` to 2 MB in its project (read
by Native AOT and the Debug build alike; `pan.log`'s start line names the budget in effect). Pan isn't on the input path
(`Pulse` is an increment; the heartbeat goes every 500 ms from its own task); without Pan nothing is emulated, so
there is no latency without it to compare: read → VIIPER mean 62 µs, p99 120 µs; read gap mean 1.34 ms, p99 2.0 ms; a
game reads a press mean 1.05 ms, p99 1.6 ms after the real controller (`checks/238-latency` probe), as in #238.

## Tests

`tests/Arcadia.Pan.Tests` (fake clock and cleanup) and `ServiceTests`, the real service end to end on the Windows CI
runner only (`CI=true` and administrator; `ARCADIA_PAN_SERVICE_TESTS=1` by hand). `tests/Arcadia.Core.Tests`:
`PanClientTests` (a fake Pan server over an anonymous pipe with the real `Guardian`), `GuardedXboxPadsTests`.
`tests/Arcadia.App.Tests/PanGuardianTests`: what the player sees (`ArcadiaDriver.Pan`, a `FakeEmulationGuardian`).
