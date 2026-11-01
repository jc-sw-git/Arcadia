# Pan (`src/Arcadia.Pan.Core`, `src/Arcadia.Pan`)

The guardian service `Pan.exe` (SYSTEM, Native AOT): named pipe, generations and teardown, HidHide hiding,
installer gate, journal, data folder. All files read, plus `pan.md`, `pan-risks.md`, `pan-setup.md`, `pan-update.md`.

**Assessment.** The threat model as implemented is sound and deliberate: the pipe carries a fixed 12-byte message
with no path, PID or command line; every effect Pan performs as SYSTEM is derived from facts Pan reads itself (pipe
client PID → handle, start time, session, image path; children by PPID + start time + name; TCP table; PnP lists).
Processes are ended only through handles held since they were taken; teardown steps are bounded and journaled under
a lock; data folder and service DACL are hardened against the `%ProgramData%` pre-creation trick; installers run only
after a publisher pin on a copy held open without write sharing. The big remaining hole, unsigned `Pan.exe` run
elevated from the user's folder, is documented as an accepted risk pending #271. Findings below sit under that line
but matter once code signing makes Pan's own checks the real boundary.

| ID | Sev | Cat | Finding |
|---|---|---|---|
| PAN-1 | Medium | Security | usbip installer: client-supplied SHA-256, no version floor → signed downgrade possible |
| PAN-2 | Medium | Security/Robustness | Pipe DoS: a non-reading client blocks `FlushAsync` and exhausts `MaxInstances`; any interactive session may connect |
| PAN-3 | Medium | Robustness | `ClientPresent` ordering race → idle stop tears down a live emulation |
| PAN-4 | Low | Security | `--install` copies any user-named file as SYSTEM into a Users-readable folder before verifying |
| PAN-5 | Low | Robustness | Teardown step budgets (82 s+) exceed `TeardownWait` (80 s); comments say 70 s |
| PAN-6 | Low | Robustness | `Register`/`Hello` ignore `_stopping` |
| PAN-7 | Low | Robustness | Leftover `UsbipServers` detached before the current one inside one 10 s step |
| PAN-8 | Low | Security | `JournalFile.Save` recreates a missing data folder with inherited (user-writable) ACL |
| PAN-9 | Low | Robustness | Install rollback cannot recreate a service "marked for delete" (1072) |
| PAN-10 | Low | CodeQuality | Service description language dead under `InvariantGlobalization` |
| PAN-11 | Medium | Architecture | Guardian ≈ 2,300 lines over four partials with ~15 interlocking flags; static mutable `ServiceHost` |
| PAN-12 | Info | CodeQuality | Dead code and small inconsistencies |
| PAN-13 | Low | Tests | Security decisions (pipe, data folder, SDDL) covered only by one CI-only E2E test |
| PAN-14 | Low | Docs | Docs overstate what the hash buys, understate step timeouts, claim a localised description |

**PAN-1** `src/Arcadia.Pan/Program.cs:87-92` takes `--usbip-sha256` from the command line; `Windows/UsbipInstall.cs:51-85`
verifies signature + that hash; `Pan.Core/InstallerSignature.cs:19` `MinimumVersion` is never consulted by Pan
(verified: `rg MinimumVersion` shows only ControllerSupport/App readers). The hash is computed by the unprivileged
client (`PanSetup.cs:64-70`, verified). `HidHideRelease.Sha256` by contrast is a constant in Pan. Exploit: a process
as the console user runs the bundled `Pan.exe --install --usbip-installer <old release> --usbip-sha256 <its hash>`;
publisher pin passes, hash passes, older usbip-win2 kernel driver installs behind a UAC prompt naming Arcadia. Today
dominated by the accepted unsigned-Pan risk; after #271 it is the only gate. Fix: Pan enforces a floor itself
(`FileVersionInfo.ProductVersion` of the installer ≥ `UsbipRelease.MinimumVersion`) or pins the release like HidHide.
Pair with CS-1. High on fact, Medium on impact.

**PAN-2** `Windows/PipeServer.cs:21` (`MaxInstances = 4`), `:133-140` (DACL `InteractiveSid` ReadWrite), `:166-171`
(refusal reply), `:252-264` (`WriteAsync` + `FlushAsync`, verified). `PipeStream.FlushAsync` on Windows calls
`FlushFileBuffers`, which on a server end blocks until the client has read and ignores the token. Any process with
the Interactive SID, incl. a second logged-on user who is refused with `ForeignSession`, connects four times and never
reads: four threads pinned, `Create` fails, "squatting… retrying in 5 s" forever, the console user's Arcadia cannot
reach Pan; an idle stop then hangs in `Task.WhenAll(open)`. Fix: drop the flush (`WriteThrough` already set) or
write-then-dispose refusals; short lifetime for refused connections; per-session cap; test with a real named pipe and
a non-reading client. Medium (flush semantics not executed here).

**PAN-3** `Windows/PipeServer.cs:166-173,206-212`, `Windows/ServiceHost.cs:134-136`, `Pan.Core/IdleStop.cs:15-29`:
old connection's `finally` runs `guardian.ClientGone()` then `ClientPresent(false)`; a reconnect's `Register` +
`ClientPresent(true)` can land between them; `IdleStop` keeps the last event and `keepRunning` is
`ShowRetryPending`, not `HasClient`. 30 s later `StopAsync` tears down a live emulation (`TornDown`, pad loss,
service restart). Derive presence from the guardian under its lock; interleaving test.

**PAN-4** `Windows/UsbipInstall.cs:28`, `Windows/HidHideInstall.cs:27`: `File.Copy(installer, copy)` into
`%ProgramData%\Arcadia\Pan` (Users `ReadAndExecute`, `ServiceSetup.cs:440`) before hash/WinVerifyTrust; a user whose
admin types the UAC credentials passes a file they cannot read, opens the copy during the check and keeps the handle
(`File.Delete` in `finally` fails and masks the error). Arbitrary-file read as SYSTEM into a user-readable place.
Copy with an admin-only DACL or verify from a handle on the source.

**PAN-5** `Windows/ServiceHost.cs:17-21`, `Windows/ServiceSetup.cs:26`, `Pan.Core/Guardian.Hiding.cs:824-825`:
ports 10 + processes 10 + show 30 + restart 32 = 82 s (+2 s notices, +10 s stale cleanup) vs `TeardownWait` 80 s,
`StopWait` 90 s, comment "up to 70 s". A hung PnP lock during an update → `TimeoutException` at 90 s, "a hung old
Pan". Compute the wait from the step constants. **PAN-6** `Pan.Core/Guardian.cs:156-181,208-220,234`: a client
connecting during a stop's teardown is registered and gets `HelloOk`. **PAN-7** `Pan.Core/Teardown.cs:114-121`,
`WindowsCleanup.cs:16` (5 s per `usbip.exe` call): leftovers first, newest last, inside one 10 s step; two stuck
leftovers and the current pad is never detached. **PAN-8** `Pan.Core/Journal.cs:138-140` `Directory.CreateDirectory`
by path; let `SecureDataDirectory` be the only creator. Low. **PAN-9** `Windows/ServiceSetup.cs:35-95,123-156,193-227`:
`DeleteService` with another handle open → 1072 on `CreateServiceW`, rollback fails the same way; retry with backoff
or `ChangeServiceConfig` in place. **PAN-10** `Arcadia.Pan.csproj:15` + `ServiceDescriptions.cs:18`:
`InstalledUICulture` is invariant under `InvariantGlobalization`, English always written.

**PAN-11** `Guardian.cs`, `Guardian.Hiding.cs`, `Guardian.Controllers.cs`, `Guardian.StaleDevices.cs` (one `_lock`;
`_phase`, `_generation`, `_helloDone`, `_stopping`, `_lastHeartbeatMs`, `_resumeGraceEndsMs`, `_hidingBusy`,
`_hiddenFor`, `_restartOnly`, `_arrivalQueued`, `_releaseRequest`, `_puckReleased`, `_bootloaderSeenMs`,
`_ownRestartMs`, `_showRetry`, `_retryShowing`, `_takenOver`, `_staleCleanup*`); correctness rests on "under the
lock" comments and re-entrancy of `System.Threading.Lock`. `ServiceHost.cs:23-27` static mutable state shared by
tests and `--console`. Extract `PuckRestartCoordinator`, `ShowRetry`, `StaleCleanupScheduler`, `HidHideChangeSet`.
The Core (decisions) / Pan (effects) split itself is clean and kept.

**PAN-12** `Windows/Native.cs` unused constants; `Pan.Core/StaleDevices.cs:78` interpreted `Regex` where the rest is
`[GeneratedRegex]`; `IdleStop.cs:16,33` `lock (this)`; `WindowsCleanup.cs:206` `Kill()` can throw after exit and turn
`TimedOut` into `Failed`; `StartEmulation` partial rollback on a failed journal write; `ServiceHost.cs:223-231` a
same-session `XboxDevices.exe` disables hiding of all Microsoft controllers (worth a comment). **PAN-13**
`tests/Arcadia.Pan.Tests/ServiceTests.cs:87` one `[ServiceFact]`; `PipeServer`, `SecureDataDirectory`/`IsOurs`/
`MoveAside`, `ServiceSddl` have no unit test; `PublisherPin`, journal, Guardian machine are well covered.
**PAN-14** `pan-setup.md` ("the SHA-256 … a signature alone lets any older installer through": the hash is the
client's, PAN-1), `pan.md` ("each with a 10 s timeout", PAN-5), `pan-setup.md` ("description in the system language",
PAN-10). XC-16 (`UseSystemResourceKeys` strips BCL exception texts from Pan's log) belongs here too.

**Done well.** Fixed-size protocol, unknown commands rejected, versions checked at `Hello`; processes ended only via
held handles re-verified by start time; `usbip.exe` from a fixed Program Files path with numeric `ArgumentList`;
uninstallers rebuilt by Pan (`msiexec /x {GUID}`, Inno `unins*.exe` under Program Files), never an `UninstallString`
as it stands; `FirstPipeInstance`, an instance always open, console-session check, one client at a time; data folder
hardening (reparse points deleted, foreign owners set aside, protected descriptor, log writes nothing before the
folder is trusted); journal validated, write-through + `File.Replace`, generation monotonic; publisher pin on
O/C/serialNumber with multi-valued RDNs rejected, file held open from hash to process start, HidHide pinned by hash
and size; unmanaged callbacks swallow everything; `pan-risks.md` honestly lists what is not defended.
