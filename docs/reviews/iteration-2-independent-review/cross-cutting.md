# Cross-cutting: tests, CI, docs, theme, dependencies

Part A (tests) is a systematic sample: all test infrastructure, 9 test files across the three projects, grep sweeps
over all 44k lines, and a script that resolved every `Trait("TestCase")` against `docs/testplans/`. Parts B-D (CI,
docs, XAML) were read in full. XR-n are the lead reviewer's own cross-project findings. The test and CI blocks (XC-3, XC-5 to XC-10, XC-12 to
XC-15) and "done well" are in [cross-cutting-tests-ci.md](cross-cutting-tests-ci.md).

**Assessment.** The test suite is unusually well built: 1,725 test methods, behaviour-oriented through two drivers
(`ArcadiaDriver` for view models, `ArcadiaWindow` for the real headless window), fake clock everywhere, no real
network, temp folders per test, machine-changing tests gated by custom `Fact` attributes, all 372 test-case traits
resolve to test plan IDs. CI secrets handling is clean (only `GITHUB_TOKEN`, no `pull_request_target`, inputs via
env), native downloads SHA-256 pinned, `TreatWarningsAsErrors` holds with zero `NoWarn`. Weak: third-party notices
and README describe the removed SISR architecture and omit HidHide; no job timeouts, no dependency automation,
analyzers at defaults; a handful of real-clock timing assertions; architecture docs name two classes that don't exist.

| ID | Sev | Cat | Finding |
|---|---|---|---|
| XR-1 | Medium | Security | Embedded browser runs the sign-in script on any https host; the gate checks only the scheme |
| XR-2 | Medium | Architecture | ControllerSupport depends on concrete Steam launcher classes (`Arcadia.Launchers.Steam`) |
| XC-1 | Medium | Docs/Licence | `THIRD-PARTY-NOTICES.md` still describes SISR as downloaded; omits HidHide and `ServiceController` |
| XC-2 | Medium | Docs | README describes a removed architecture (XInput, SISR, `legacy/steambutler-rs`, `sisr.log`) |
| XC-3 | Medium | CI | No `timeout-minutes` on any job; no hang timeout on `dotnet test` |
| XC-4 | Medium | Docs | Architecture docs name `GuardedXboxPads`/`QrCodes` (missing), misplace `UsbipPorts`, omit ~25 classes |
| XC-5 | Medium | CI/Security | No Dependabot; actions pinned by tag only |
| XC-6 | Medium | Tests | Real-clock timing assertions (`HoldButtonFillTests`, `LibrarySpeedTests`, `InstallFolderTests`) |
| XC-7 | Low | Tests | Negative assertions after fixed real delays pass vacuously |
| XC-8 | Low | CodeQuality | Analyzers at SDK defaults; no `.editorconfig` |
| XC-9 | Low | CI/Security | `build`/testplan/doc-size jobs have no `permissions` block |
| XC-10 | Low | CI/Security | Release job exposes the write token to the test step |
| XC-11 | Low | Theme | Literal `Transparent`/`Opacity` in views; placeholder HSL colours hard-coded in C# |
| XC-12 | Low | Tests | Shared mutable static state across tests (`InputHints`, `AppLog.Directory`) |
| XC-13 | Low | Tests/Docs | Two `E2E: yes` cases without a trait or a "deferred" note (T23-5, T243-12) |
| XC-14 | Low | Tests | Run time: 400 ms real sleeps in ~90 window tests; in-test `dotnet publish` |
| XC-15 | Low | CI | `Install HidHide` runs on skipped builds; no `concurrency` group |
| XC-16 | Low | Robustness | Pan AOT `UseSystemResourceKeys` strips BCL exception texts from Pan's log |
| XC-17 | Info | CI | Mixed xunit v2/v3; `Avalonia.Controls.WebView` 12.1.1 behind Avalonia 12.1.4 |

**XR-1** `src/Arcadia.App/Views/WebPageHost.cs:90-103` (comment "The launcher's pages and its sign-in only; a link to
anything else stays unopened", code: `target.Scheme != Uri.UriSchemeHttps && target.Scheme != "about"` → cancel) and
`:175-185` (`view.InvokeScript(page.Script)` after every completed navigation, `page.Accepts(text)` → `Complete`).
`SteamStorePage.AccountScript` reads `#application_config` attributes and `SignInOf` accepts any `steamid` +
`webapi_token` from the JSON. Scenario: the user follows any https link from Steam's sign-in page (footer, support,
a phishing link in a community post); that page renders a `#application_config` element with an attacker's
`steamid`/`webapi_token`; Arcadia completes the sign-in with a foreign account, stores the token via DPAPI and shows
that account's library. No credential theft (the user's cookies stay in the WebView2 profile), but account confusion
and a comment that promises a host restriction the code doesn't have. Fix: cancel navigations whose host is not in
`SteamStorePage.CookieDomains` (or a per-page allowlist on `WebPageViewModel`), and run the script only on an allowed
host. Verified by the lead reviewer; not raised by the App agent. Confidence High.

**XR-2** `src/Arcadia.ControllerSupport/Arcadia.ControllerSupport.csproj` references `Arcadia.Launchers`;
`SteamInstallation.IsRunning/Find/ShutdownAsync/RunningGameName/RunningAppId` and `ProcessWatch` are used in
`ControllerSupportService.cs`, `ControllerSupportPlatform.cs`, `ControllerSupportRemoval.cs`, `SisrInstallation.cs`,
`Pan/TakenOverControllers.cs`. Steam Controller Support legitimately needs Steam the program, but it binds to the
Steam launcher *folder*; with LNCH-5 (launchers → Core), CORE-8 (`KnownLaunchers` in Core) and APP-9 (App → Steam)
the dependency graph is Abstractions ← Core ← Launchers ← ControllerSupport ← App with Steam reachable from every
layer, which is the opposite of "a launcher is one `ILauncherIntegration`, as a plugin would be". Decision needed:
either a small `ISteamPresence` (running, in front, app id, shutdown) owned by ControllerSupport and implemented in
the Steam folder, or an explicit, documented exception in `docs/architecture/README.md`. Medium.

**XC-1** `THIRD-PARTY-NOTICES.md:3-13` ("Arcadia downloads and runs SISR", installed to `%LOCALAPPDATA%\SISR`) while
`SisrInstallation` is "what an older Arcadia left of SISR"; no HidHide entry although `HidHideRelease.cs:18-23` pins
and `Pan.exe --install` installs a kernel filter driver; `System.ServiceProcess.ServiceController` missing from the
library table. Shipped in every build (`Arcadia.App.csproj:26`) and release. Lead reviewer's rating: Medium rather
than the agent's High, since HidHide is installed on request by its own installer rather than redistributed; still
the one doc a GPL project must keep right. Fix: rewrite SISR to "detection and removal only", add HidHide and the
package; a CI check that every `PackageReference` appears in the notices.

**XC-2** `README.md:17` links `legacy/steambutler-rs` (no such folder; `.gitignore:9-11` too), `:37` "XInput", `:38`
"manages SISR", `:82` SISR download, `:89` `sisr.log`; the project table omits Abstractions, Pan, Pan.Core and two
test projects. A security-minded user doesn't learn that Arcadia installs a service and a driver. **XC-4**
`docs/architecture/README.md:12` `GuardedXboxPads` (now private classes in `PanEmulation.cs`), `UsbipPorts` listed
under ControllerSupport but living in Pan.Core; `app.md:69` `Services/QrCodes`; undocumented: ControllerSupport
`Copy/*`, `HidHideInstallation`, `Viiper*`, `SteamControllerRouting`, `FastReads`; Pan.Core `ControllerHiding`,
`StaleDevices`, `HidHideRelease`, `DataFolderRights`, `UninstallHandOff`; App see APP-15. `README.md` is at 9,088 of
10,000 bytes, so this needs a split, not appends. Also LNCH-16, PAN-14, CS-15.

**XC-11** `Views/MainWindow.axaml:83,88` `Background="Transparent"` on buttons that carry `Classes="glyphButton"`
(a local value beats the style, `avalonia.md`), `:173` `Opacity="0.55"`, `:799` and `CountLineView.axaml:7`
transparent hit-test panels, `MouseModeOverlay.axaml:9`/`MouseModeKeyboardWindow.axaml:11` transparent windows
(functional); `Services/Formatting.cs:85-105` HSL saturation/lightness of every placeholder cover, hero backdrop and
launcher tile in C#. No hex colours, no `BoxShadow` literals, no `Focusable="True"`, every view has `x:DataType`,
`XamlBindingTests` enforces nullable paths. Move the two button backgrounds into `Button.glyphButton`
(`Theme.axaml:321`), the HSL constants into theme resources; a grep CI step with an allow-list for the two windows.

**XC-16** `src/Arcadia.Pan/Arcadia.Pan.csproj:13-16`: `ex.Message` for BCL exceptions becomes the resource key
(`IO_FileNotFound_FileName`) in Pan's only diagnostic. **XC-17** `Arcadia.App.Tests.csproj:17` xunit.v3 vs
`Arcadia.Core.Tests.csproj:14`, `Arcadia.Pan.Tests.csproj:10` xunit 2.9.3 (`TestContext.Current.CancellationToken`
exists only in v3); `Arcadia.App.csproj:39`.
