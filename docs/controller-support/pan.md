# Pan, Arcadia's guardian service (#243, part A)

Pan gives the real controllers back to every program when Arcadia ends unexpectedly or hangs. Part A (this file): the
service, its setup and the VIIPER/usbip side of the teardown. Part B (HidHide, hide/unhide, #243 later PR) adds the
commands `Hide`/`Unhide`, which Pan answers with `Unsupported` for now. Overview: [README](README.md); the pad itself:
[xbox-pad.md](xbox-pad.md). Decisions came from the QE decision review on #243 (R1–R16).

## Pieces

- `src/Arcadia.Pan.Core` (no dependencies, AOT-clean): the protocol (`PanMessage`), the decisions (`Guardian`), the
  journal (`JournalFile`), the teardown steps (`TeardownPlan`, `TeardownRunner`), the installer signature check
  (`InstallerSignature`, `WinTrustAuthenticode`, `UsbipRelease`, moved here from ControllerSupport) and `UsbipPorts`.
- `src/Arcadia.Pan` → `Pan.exe`, Native AOT (`Pack.ps1` publishes it to `Pan\Pan.exe` beside `Arcadia.exe`; it needs
  the C++ build tools, the CI's Windows runner has them): `ServiceHost` (SCM, pre-shutdown, power resume), `PipeServer`,
  `ServiceSetup` (`--install`, `--uninstall`), `UsbipInstall`, `Restore` (`--restore`), `WindowsCleanup` (held process
  handles, usbip ports), `PanLog` (`%ProgramData%\Arcadia\Pan\pan.log`, 1 MB rotation). `--console` runs the service in
  a console for development; `--version` prints version and protocol.
- `src/Arcadia.ControllerSupport/Pan`: `PanClient` (`IEmulationGuardian`), `PanPipeTransport`, `GuardedXboxPads` (the
  decorator that registers every plug with Pan), `PanSetup` (runs the bundled `Pan.exe` elevated). The App wires it in
  `SteamControllerForGames.Create`; `SteamControllerXboxPad.FeedAlive` pulses the heartbeat on every poll.

## Protocol and identity

- Named pipe `\\.\pipe\ArcadiaPan`, 12-byte messages: kind, protocol version (1), generation, value. Commands Hello,
  EmulationStarted, EmulationStopped, Heartbeat, TeardownAcknowledged, PlannedExit (Hide/Unhide later); replies HelloOk,
  Started, Stopped, HeartbeatOk, Acknowledged, Rejected(reason), TornDown(reason, notice), Unsupported, Refused(reason).
  Pan takes the first client of the active console session (`ProcessIdToSessionId` ==
  `WTSGetActiveConsoleSessionId`); connections are served side by side, so a second client or one from another session
  gets `Refused` at once. The pipe's DACL: SYSTEM full, Interactive read/write, nobody else;
  `FILE_FLAG_FIRST_PIPE_INSTANCE`, so nobody can squat the name first (a taken name is retried every 5 s, logged).
  Arcadia checks that the server's PID is the service's (`OpenService` with `SERVICE_QUERY_STATUS` only, so without
  elevation; `QueryServiceStatusEx`) before it says hello.
- Every emulation has a generation: `EmulationStarted` returns the next number; Pan replies `WrongGeneration` to
  anything about another one. After a teardown the client must `TeardownAcknowledged` before the next emulation
  (`TeardownNotAcknowledged`); `PanClient` does that itself.
- Heartbeat every 500 ms, but only when the pad's `Feed` moved (`Pulse` counter): a stuck poll thread counts as a hang.
  No heartbeat for 3 s (`QueryUnbiasedInterruptTime`, sleep excluded) → teardown with reason `Hang`, the toast says so;
  the pad comes back on the next tick as a new generation (counts as a pad loss for the 3-in-5-minutes rule).
  `PlannedExit` (Arcadia's normal shutdown) unregisters without a teardown.
- Client gone (pipe broke or process ended: Pan holds the client's process handle) → teardown `ClientGone`. Service
  stop or pre-shutdown while emulating → teardown too (`ServiceStopping`), run while the pipe is still open and its
  `TornDown` notice given 2 s to go out before the pipe closes; then the client sees `Lost` with the service gone. Power
  resume only restarts the hang clock: sleep is no hang. A torn-down generation is remembered per connection only: a
  Pan that counts anew (reinstalled, journal gone) may hand it out again as a live one.

## Teardown and journal

- `EmulationStarted` is registered after the pad is plugged (so `viiper.exe` already runs as Arcadia's child; the gap
  is a few ms); Pan takes every child of the client named `viiper.exe` (Toolhelp32, parent PID + start time) and holds
  its handle, so a PID can't be reused under it. Journal `%ProgramData%\Arcadia\Pan\journal.json` (temp +
  write-through + `File.Replace`, `.previous` kept, a broken one moved to `.broken` and reported as
  `JournalUnreadable` → toast) written before `Started` is answered. The journal is never deleted: with nothing to
  undo it still keeps the generation, so a restarted Pan counts on and never repeats a number a client remembers.
- Steps, each with a 10 s timeout on a worker, never throwing: detach usbip ports attached from VIIPER (`usbip.exe port`
  / `detach -p`, from `%ProgramFiles%\USBip`), then end the held forwarding processes. Each ended process leaves the
  journal at once (under the guardian's lock, so a step left behind after its timeout can't corrupt it); what failed
  stays listed and goes into the next emulation's teardown. With no `viiper.exe` of the client (another program's
  VIIPER) a teardown only detaches the ports, logged at start.
- A restarted Pan (crash, reboot, `--restore`) always undoes its journal first: held processes are re-verified by PID +
  start time, the client itself is never killed, "client N still running/gone" is only logged (#244 adds the crash
  report hook).
- Pan stops itself after 30 s without a client (nothing idles in the background); Arcadia starts it with
  `ServiceController.Start` on connect (users have `SERVICE_START` by the DACL). Recovery: 3 restarts, 1 s, reset daily,
  also on non-zero exit.

## Setup, update, uninstall

- "Treiber installieren" (settings, or the fix row "Wächterdienst Pan fehlt" / "usbip-Treiber fehlt"):
  `ControllerSupportPlatform.InstallDriversAsync` downloads the usbip-win2 installer only when the driver is missing or
  too old (`VerifiedDownload`, GitHub's SHA-256), then runs the bundled `Pan.exe --install --expect-hash <sha256 of the
  bundled exe> [--usbip-installer <path>]` elevated: one UAC prompt for both. Pan (elevated) checks the installer's
  Authenticode signature against `UsbipRelease.Publisher` on a copy in Pan's data folder (the user can't swap it
  after the check), runs that `/S` (5 min), copies itself to
  `%ProgramFiles%\Arcadia\Pan\Pan.exe` and compares the copy's SHA-256 with the source and the expected hash, creates the service with a quoted image path, auto
  start, the description in the system language (de, en, fr, es, it; else en), recovery, 40 s pre-shutdown, and the
  DACL `D:(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;RPLCLORC;;;IU)` (users start and query,
  never change). A declined prompt is a failed action ("… abgelehnt"); Pan's exit code 3 points to `pan.log`.
- Data folder: `%ProgramData%` lets any user create `Arcadia\Pan` first and keep its ownership, so `--install`
  secures both folders before its first log line (`ServiceSetup.SecureDataDirectory`): a reparse point is deleted, a
  folder not owned by SYSTEM/Administrators is renamed to `<name>.untrusted-<time>`, the folder is created with a
  protected DACL (SYSTEM and Administrators full, no inheritance) and Administrators as owner. The service and
  `--restore` refuse to run when the folder doesn't look like that (`CheckDataDirectory`; exit 3 / service failure).
- Update: at start, when the bundled Pan is newer than the installed one (file versions), one dialog "Pan
  aktualisieren" (A updates with one UAC prompt, B later; asked again at the next start). Pan's `--install` stops and
  replaces the old service; its stop is a full teardown.
- Uninstall (Velopack hook, `Program.OnBeforeUninstall`): after the SISR and data questions, `Pan.exe --uninstall`
  elevated, one UAC prompt, no question (Pan exists for Arcadia alone): stop (= teardown), delete the service, undo the
  journal, delete both folders (a file in use goes at the next boot, `MoveFileEx`). Declined: the service stays, idles
  and stops itself; `Pan.exe --uninstall` by hand removes it.
- "Controller wiederherstellen" (settings, shown while Pan is installed): `Pan.exe --restore` elevated, for the case
  the service itself hangs: undoes the journal without the service.
- Without Pan: no pad is plugged in (`GuardedXboxPads.Unavailable`), the per-game mode is greyed out, the Controller
  category shows the warning "Wächterdienst Pan fehlt" also while "Steam Controller Kompatibilität" is off
  (`ControllerSupportService` → `IControllerSupportPlatform.CheckDrivers`, only while the settings watch).

## Tests

- `tests/Arcadia.Pan.Tests`: protocol, journal, teardown, guardian (fake clock and cleanup) and `ServiceTests`: the real
  service end to end on the Windows CI runner only (`CI=true` and administrator; `ARCADIA_PAN_SERVICE_TESTS=1` by hand):
  publishes a single-file Pan, `--install` with the hash, `sc sdshow`, quoted `ImagePath`, pipe ACL, hello, a copy of
  `ping.exe` named `viiper.exe` ended by a service stop and by a hang, `--uninstall`.
- `tests/Arcadia.Core.Tests`: `PanClientTests` (a fake Pan server over an anonymous pipe with the real `Guardian`),
  `GuardedXboxPadsTests`, the service's driver warnings and restore. `tests/Arcadia.App.Tests/PanGuardianTests`: what the
  player sees (`ArcadiaDriver.Pan`, a `FakeEmulationGuardian`).
