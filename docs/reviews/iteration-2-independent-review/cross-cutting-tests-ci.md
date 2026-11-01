# Cross-cutting A: tests and CI (XC-3, XC-5 to XC-10, XC-12 to XC-15)

Part of the cross-cutting review; the overview, findings table, docs/theme items and the lead reviewer's own
findings (XR-1, XR-2) are in [cross-cutting.md](cross-cutting.md).

**XC-3** `.github/workflows/ci.yml:31-61`, `release.yml:16-41`: only the HidHide step has a timeout;
`Install-HidHide.ps1:26` records a six-hour hang; `ServiceTests` waits up to 3 min. `timeout-minutes` per job and
`--blame-hang-timeout 10m` on `dotnet test`. **XC-5** no `.github/dependabot.yml`; `actions/*@v4`, `github-script@v7`
by tag. Versions pinned: Avalonia 12.1.4, WebView 12.1.1, CommunityToolkit.Mvvm 8.4.2, Svg.Skia 5.1.1, Velopack
1.2.161, Viiper.Client 0.9.0, ProtectedData/ServiceController 10.0.12, xunit 2.9.3 + xunit.v3 3.2.2, Test.Sdk
17.14.1, coverlet 6.0.4, SDL3 3.4.18, HidHide 1.5.230 (advisories not checkable offline). Add Dependabot for `nuget`
and `github-actions`, SHA-pin actions, `dotnet list package --vulnerable` as a non-blocking step.

**XC-6** `tests/Arcadia.App.Tests/Window/HoldButtonFillTests.cs:33-52` (`Thread.Sleep(500)` → `ScaleX` in
`0.2..0.95`; the fill animates on the headless render clock = real time, `ArcadiaWindow.cs:28-31` says so),
`LibrarySpeedTests.cs:72-94` (`TotalOf(lines[^1]) <= 299` real wall time), `InstallFolderTests.cs:49-54`
(`ElapsedMilliseconds <= 1000`). On a loaded Windows runner that also installs a driver these go red for unrelated
PRs. Drive the hold on the fake clock, assert monotonic, widen to the budget actually meant. **XC-7**
`PanClientTests.cs:266-268,284-286,634-636`, `TakenOverControllersTests.cs:471-480`, `SteamControllerCopyBridgeTests.cs:287`,
`PanEmulationTests.cs:501`: `await Task.Delay(50); Assert.Equal(0, …)` flakes green when the work is slower than the
delay; advance the fake clock past the next retry instead. **XC-12** `src/Arcadia.App/Input/InputHints.cs:10-33`
static `UsesController`/`Symbols` mutated by `MainViewModel` and reset by `ArcadiaWindow.cs:62-64` ("a test before
may have left it"); no `xunit.runner.json`, App test classes run in parallel. **XC-13** `docs/testplans/iteration-1/
023-…md:13` T23-5, `iteration-2/243-…md:44` T243-12 (exercised by `ServiceTests` but without a `Trait`). **XC-14**
`ArcadiaWindow.cs:31,115,432` 400 ms real sleeps × (35 `WaitForTransitions` + 19 `CentreOf` + 25 `AssertRingAround`
+ 14 captures) on one UI thread; `ServiceTests.cs:350-376` runs `dotnet publish` in-test.

**XC-8** `Directory.Build.props`: no `AnalysisLevel`/`EnforceCodeStyleInBuild`, no `.editorconfig`; "warning-free"
means warning-free at `AnalysisMode=Default` (CA2007/CA2016 token forwarding, CA1849 sync-over-async, CA3xxx/CA5xxx
off); one justified `#pragma` (`InstallerSignature.cs:158`). `latest-recommended` + a one-time cleanup PR.

**XC-9** `ci.yml:31-37`, `doc-sizes.yml`, `testplan.yml` run downloaded (hash-pinned) binaries with the default
token; `permissions: contents: read` is one line. **XC-10** `release.yml:18-19` job-level `GH_TOKEN` with
`contents: write` reaches `dotnet test` (`:41`), which runs `Pan.exe` and child processes; move to the three `gh`/`vpk`
steps; read the tag from `$env:GITHUB_REF_NAME` (`:35,45,69`). **XC-15** `ci.yml:49-54` HidHide step lacks the
`skip_build` condition; no `concurrency` on `ci.yml`.

**Done well.** Every native download pinned by SHA-256 and verified before use (`Sdl3.targets`, `Viiper.targets`,
`HidHideInstallation`, `Install-HidHide.ps1` adds Authenticode + publisher); `vpk` version pinned identically in
csproj and tool manifest; no `pull_request_target`, inputs through env with a comment why, the tag workflow validates
SemVer and refuses existing tags; tests speak the user's language with `FakeTimeProvider` everywhere, GUID temp
folders, the log moved out of the user profile, every test `HttpClient` faked, made-up fixture IDs; error paths are
first-class (`GameLaunchTests`, `SteamScannerTests`, `SettingsServiceTests`, Pan `FakeCleanup` hangs/fails/throws
every step); machine-changing tests skip with a reason and share a `DisableParallelization` collection;
`XamlBindingTests` turns an Avalonia pitfall into a mechanical check; `check-doc-sizes.sh` keeps every doc under its
limit; the testplan-only skip refuses unless the previous head was green.
