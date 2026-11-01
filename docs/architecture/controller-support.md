# Architecture: `Arcadia.ControllerSupport`, `Arcadia.Pan.Core`, `Arcadia.Pan`

The controller emulation for games and Pan, its guardian service. What was verified and why: `docs/controller-support/`
(README first). The other projects: [README.md](README.md).

## `src/Arcadia.ControllerSupport`

**The emulation and its parts**
- `ControllerSupportService`: the emulation's parts and their fixes (`HealthIssues.WorstProblem`); in code the
  feature keeps this old name. `ControllerSupportPlatform`: Windows and Steam, replaced in tests.
- `EmulationParts`: usbip-win2, VIIPER, Pan and HidHide, and the rows that say one is missing. `UsbipDriver`
  (installed version, `MinimumVersion`, the latest installer), `HidHideInstallation` (installed or not, the pinned
  installer), `GitHubReleases.cs` (`GitHubReleaseClient`, `VerifiedDownload`: GitHub's SHA-256 checked).
- `ControllerSupportRemoval`: what goes at Arcadia's uninstall. `SisrInstallation`/`WindowsSisrSystem`: what an older
  Arcadia left of SISR, removed on request ([sisr-removal.md](../controller-support/sisr-removal.md)).

**VIIPER, the emulated pads**
- `ViiperRelease` (the fork's build pinned in `Viiper.targets`), `ViiperInstallation` (fetches it next to Arcadia,
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
  restore), `PanCompatibility` (only the bundled Pan, by SHA-256).
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
- Installers: `InstallerSignature` (`PublisherPin`, `WinTrustAuthenticode`, `UsbipRelease`: who may have signed one).
- VIIPER's ports: `UsbipPorts`, `TcpListeners`, `ViiperListeners` (only Arcadia's `viiper.exe` on 127.0.0.1).
- Uninstall: `UninstallOrder`, `UninstallHandOff` (the copy in `%ProgramFiles%`, #270), `DriverUninstall`.
- `StaleDevices.cs` (`StaleDeviceCleanup`, #347), `DataFolderRights`, `PanPaths`.

## `src/Arcadia.Pan`

`Pan.exe`, the guardian service, Native AOT, `InvariantGlobalization`. `Program` (arguments), `ServiceDescriptions`,
`PanLog`; `Windows/`: `ServiceHost` (SCM), `PipeServer`, `ServiceSetup` (`--install`/`--uninstall`), `UsbipInstall`,
`HidHideInstall`, `DriverRemoval` (`--remove-drivers`), `Restore` (`--restore`), `WindowsCleanup`, `WindowsHiding`,
`WindowsStaleDevices`, `UnbiasedClock`, `Native`. Details: `docs/controller-support/pan.md`.
