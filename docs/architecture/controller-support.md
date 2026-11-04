# Architecture: `Arcadia.ControllerSupport`, `Arcadia.Pan.Core`, `Arcadia.Pan`

The controller emulation for games and Pan, its guardian service. What was verified and why: `docs/controller-support/`
(README first). The other projects: [README.md](README.md).

## `src/Arcadia.ControllerSupport`

**The emulation and its parts**
- `ControllerSupportService`: the emulation's parts and their fixes (`HealthIssues.WorstProblem`); in code the
  feature keeps this old name. `ControllerSupportPlatform`: Windows, replaced in tests. Steam comes in as the program
  of the launcher that owns Steam Input (`ILauncherProgram`); the project doesn't reference `Arcadia.Launchers`.
- `EmulationParts`: usbip-win2, VIIPER, Pan and HidHide, and the rows that say one is missing. `UsbipDriver`
  (installed version, `MinimumVersion`, the latest installer), `HidHideInstallation` (installed or not, the pinned
  installer), `GitHubReleases.cs` (`GitHubReleaseClient`, `VerifiedDownload`: GitHub's SHA-256 checked, bytes reported).
- A driver install from the settings (#413): `DriverInstallation` runs its steps (Windows prompt first, VIIPER, Steam,
  downloads, hand-over, Pan's report and exit), `DriverInstallProgress` is the dialog's model (steps, weighted bar that
  never goes back), `ControllerSupportService.Installation` shows it to the App.
- `ControllerSupportRemoval`: what goes at Arcadia's uninstall. `SisrLeftovers` (`ControllerSupportService.Sisr`): the
  group "SISR" and "SISR entfernen", run as one of the service's actions; `SisrInstallation`/`WindowsSisrSystem`: what
  an older Arcadia left of SISR on disk ([sisr-removal.md](../controller-support/sisr-removal.md)).

**VIIPER, the emulated pads**
- `ViiperRelease` (the fork's build pinned in `Viiper.targets`), `ViiperInstallation` (fetches it into `%APPDATA%\Arcadia\viiper`,
  SHA-256 checked), `ViiperServer`/`WindowsViiperHost` (starts Arcadia's own `viiper.exe` on loopback with a fresh key
  and trusts it only once checked, #304), `ViiperConnection` (handshake, `ViiperEncryptedStream`).
- `IVirtualXboxPads`/`ViiperXboxPads` (`Viiper.Client`), `VirtualPads.cs` (`IVirtualPad`, `XboxPadAsVirtualPad`),
  `ViiperReportPad` and `VirtualPadReports` (DualShock 4, DualSense (Edge), Switch 2 Pro on VIIPER's raw stream),
  `ViiperRawDevice` (a device driven by its raw stream, the copy of the puck).
- `SteamControllerXboxPad`: feeds and plugs the Steam Controller's Xbox pad; `PadForwarder` sends on the reading
  thread; `FastReads` (1 ms reads only while a pad's states reach a game). Wired in the App by
  `SteamControllerForGames`.
- `SteamControllerRouting` (Xbox pad plugged or not, per running game and mode), `SteamControllerCopyRules`/
  `SteamControllerCopyGate` (when the copy and when the Xbox pad pass input), `ForegroundNow`.

**Steam's copy of the Steam Controller** (`Copy/`)
- `ViiperSteamControllerCopy`: the copy on Windows, the real device read raw and VIIPER's `hidproxy` device;
  `SteamControllerCopyBridge` runs it free of Windows; `WindowsPuck` (`IRealPuck`: the puck or the controller by cable
  through Windows' HID API); `HidProxyFrames` (the stream protocol); `PuckDescriptors` (the real descriptors, from
  `puck-descriptors.json`, `cable-descriptors.json`).
- `SteamControllerCopyFilter` (what passes, without Arcadia's combos), `SteamControllerMessages`, `ISteamControllerHold`,
  `FirmwareAttemptWatch` (Steam tried a firmware update through the copy, #295).

**Pan's client** (`Pan/`)
- `PanClient` (`IEmulationGuardian`, heartbeat, reconnect), `PanTransport.cs` (`PanPipeTransport`, `PanServiceStart`:
  starting the service and waiting for it), `PanSetup` (runs the bundled `Pan.exe` elevated: install, uninstall,
  restore), `PanHandOver` (Arcadia's end of the hand-over pipe of an elevated Pan: the server check, the one message,
  `abort` on dispose), `PanInstallRun` (an elevated install waiting for its installers: the hand-over, its report),
  `PanExitCodes` (what Arcadia says to each exit code), `PanCompatibility` (only the bundled Pan, by SHA-256).
- `PanEmulation`: hide once at the start, show at the end, every plug registered with Pan; its states and problems
  for the settings. `TakenOverControllers`: every other controller Arcadia takes over, hidden and emulated, sent from
  the reading thread. `ControllerLayouts`: the choices of "Controller zuordnen" per model.

## `src/Arcadia.Pan.Core`

Pan's decisions, no dependencies, AOT-clean.
- `PanMessage`/`PanCommands` (protocol), `Guardian` (generations, hang, teardown; partials for hiding, controllers,
  stale devices), `Journal.cs` (`JournalFile`), `TeardownPlan`/`TeardownRunner`, `IdleStop` (stop without a client),
  `IPanClock` (time that stands still in standby), `PanTimeouts` (each elevated step's budget and Arcadia's wait).
- Hiding: `Hiding.cs` (`SteamControllerHiding`: HidHide versions and states Pan works with, the user's entries kept),
  `ControllerHiding.cs` (`ControllerModel`: a model by its USB ids, never an instance), `HidHideWire` (HidHide's driver interface),
  `HidHideRelease`/`HidHideSetup`.
- An install run (#413): `InstallRun` (its command line), `InstallHandOver` (the `go`/`abort` message, checked against
  the run), `InstallReport` (the step report and its writer).
- The restore's run (#471): `RestoreRun` and `RestoreHandOver` (only `go`/`abort`); `HandOverWait` is the wait both share.
- Installers: `InstallerSignature` (`PublisherPin`, `WinTrustAuthenticode`, `UsbipRelease`: who may have signed one).
- VIIPER's ports: `UsbipPorts`, `TcpListeners`, `ViiperListeners` (only Arcadia's `viiper.exe` on 127.0.0.1).
- Uninstall: `UninstallOrder`, `PendingUninstall` (the marker a later start finishes, #422), `SelfRemoval` and
  `ArcadiaInstallations` (no Arcadia left, #422), `UninstallHandOff` (the copy in `%ProgramFiles%`, #270), `DriverUninstall`.
- `StaleDevices.cs` (`StaleDeviceCleanup`, #347), `DataFolderRights`, `PanPaths`.

## `src/Arcadia.Pan`

`Pan.exe`, the guardian service, Native AOT, `InvariantGlobalization`. `Program` (arguments), `ServiceDescriptions`,
`PanLog`; `Windows/`: `ServiceHost` (SCM), `PipeServer`, `InstallCommand` (`--install`: hand-over, report, steps),
`RestoreCommand` (`--restore`: waits for Arcadia's go, #471),
`HandOverPipe` (the run's pipe), `ServiceSetup` (the service), `PanUninstall` (`--uninstall`, the finish at start), `ArcadiaProbe`, `UsbipInstall`,
`HidHideInstall`, `DriverRemoval` (`--remove-drivers`), `Restore` (`--restore`), `WindowsCleanup`, `WindowsHiding`,
`WindowsStaleDevices`, `UnbiasedClock`, `Native`. Details: `docs/controller-support/pan.md`.
