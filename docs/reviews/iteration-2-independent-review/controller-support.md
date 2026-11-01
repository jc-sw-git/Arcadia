# Controller Support (`src/Arcadia.ControllerSupport`)

Driver install, GitHub downloads, Pan client, VIIPER pads, the copy to Steam, `TakenOverControllers`, `PanEmulation`.
All files read; `docs/controller-support/` read for the intended design.

**Assessment.** Carefully designed against its own threat model and the code mostly matches the docs: downloads are
hash-checked before anything elevated touches them, the elevated step is one `Pan.exe` call with `ArgumentList`, the
pipe is trusted only after a server-PID check against the SCM, VIIPER starts on loopback with a fresh key and is
positively verified to refuse a wrong key, every background tick is wrapped. Error-path tests are unusually good. No
Critical. Weak spots: usbip-win2 is fetched as unpinned "latest" (HidHide is pinned); a few "never throws" hot-path
methods can throw into the SDL loop (INP-3); the `PanClient` keeper loop has no catch-all; avoidable per-millisecond
work on the reading thread and a full driver enumeration every 2 s beside a game; four classes of 780-1060 lines.

| ID | Sev | Cat | Finding |
|---|---|---|---|
| CS-1 | Medium | Security | usbip-win2 installer unpinned "latest", no version/URL gate before the elevated run |
| CS-2 | Low | Security | GitHub asset URL/name used unchecked (scheme, host, path), no size cap |
| CS-3 | Medium | Robustness | `Feed` on the SDL reading thread not exception-safe → all controllers lost (see INP-3) |
| CS-4 | Medium | Robustness | `PanClient` keeper loop has no catch-all; reconnection can die silently |
| CS-5 | Low | Robustness | `ViiperReportPad` swallows feedback/handler exceptions unlogged, counts them as VIIPER losses |
| CS-6 | Low | Performance | `UsbipDriver.IsRunning()` enumerates every kernel driver every 2 s while a game runs |
| CS-7 | Low | Performance | Per-poll allocations in `TakenOverControllers.Feed` at 1 kHz |
| CS-8 | Info | Performance | Per-state `byte[]` allocations on the raw-pad send path |
| CS-9 | Low | Robustness | `ControllerSupportRemoval.Remove` kills every `viiper.exe` of the user, not by path |
| CS-10 | Low | Robustness | `RunBusyAsync` catch list misses `JsonException`/`UriFormatException`; `finally` refresh can mask the error |
| CS-11 | Low | Robustness | `Lost`/`TornDown` subscriber exceptions propagate into unrelated request paths |
| CS-12 | Low | Robustness | `UsbipDriver.InstalledVersion` unguarded `FileVersionInfo.GetVersionInfo` |
| CS-13 | Low | Robustness | Shutdown waits on gates without a bound (up to ~1 min on exit) |
| CS-14 | Info | CodeQuality | `VerifiedDownload` cleanup in `catch` can replace the original exception |
| CS-15 | Low | Modularity | Big classes with mixed responsibilities; doc names `GuardedXboxPads`, which is now private |
| CS-16 | Info | Robustness | `IsPanUpdating` stays true if `UpdatePanAsync` throws and the caller forgets `SwitchPanOffForUpdate` |
| CS-17 | Low | Tests | No tests: keeper survives a throwing transport/log; malformed release JSON/URL; throwing `ForwardingAllowed` |

**CS-1** `UsbipDriver.cs:200-212`, `GitHubReleases.cs:18-40`: `GetLatestAsync("vadimgrn", "usbip-win2")`, asset by
name pattern, hash taken from the same API response, then `Pan.exe --install --usbip-installer … --usbip-sha256 …`
runs it as SYSTEM. `HidHideRelease` pins version, URL, SHA-256, size. `release.Version` is parsed but never compared
with `MinimumVersion`. Any signed upstream release, newer or broken for Arcadia, installs automatically with admin
rights; the digest gives integrity against the CDN, not against the API. Pan has no floor either (PAN-1). Fix: pin
like HidHide, or at least `Version >= MinimumVersion`, https + host allowlist, size check. High.

**CS-2** `GitHubReleases.cs:37,136-139`, `UsbipDriver.cs:209`: `new Uri(a.BrowserDownloadUrl!)` any scheme/host;
`Path.Combine(TempPath, "Arcadia", asset.Name)` escapes the folder for a rooted or `..\` name, and
`ControllerSupportPlatform.cs:154-159` later `File.Delete`s that path; `CopyToAsync` without a byte cap. Needs a
tampered API response, so Low; the DoD rule ("no file-supplied string unchecked into a path or URI") still applies.

**CS-3** `SteamControllerXboxPad.cs:88-129` ("Never throws"), `Pan/TakenOverControllers.cs:160-226`: `Combos()`,
`ForwardingAllowed()` (foreground queries from the App, `SteamControllerForGames.cs:95-151`), `LayoutOf`, `Rumble`,
`Light` are raw delegate calls; an exception leaves `Sampled` into `Poll`'s catch → `Lose` (INP-3). Wrap the callback
evaluations, fall back to "not forwarding"/neutral; test with a throwing `ForwardingAllowed`. Medium.

**CS-4** `Pan/PanClient.cs:106` (`_keeper ??= Task.Run(KeepConnectedAsync)`), `:473-524`: only `ConnectAsync` is in
a try; `SetUnavailable` → `_log`, `Drop(null)` → `link.Dispose()`, `WhyLeftAlone()`, `_transport.Unavailable()`
(SCM P/Invoke) are unguarded; nothing observes a faulted keeper until `DisposeAsync`, which catches only
`OperationCanceledException`. Disk full → `IOException` from the log sink ends reconnection for the session with no
toast; exit may throw. DoD: no unguarded fire-and-forget. Same for `SendHeartbeatsAsync`. Verified by the lead
reviewer. Fix: catch-all per iteration with stack trace, continue after `Retry`; faulted-task continuation in `Start()`.

**CS-5** `ViiperReportPad.cs:37-42,61-65`: `catch (Exception) { _broken = true; }` without log; `KeepPadsAsync`
then blames VIIPER after three losses and the real cause never reaches `arcadia.log`.

**CS-6** `UsbipDriver.cs:169-192` `ServiceController.GetDevices()` (all kernel drivers) from `ViiperXboxPads.cs:33`
`Unavailable()`, evaluated every `PanEmulation.StepAsync` tick (`:362`). Query the one service
(`OpenServiceW` + `QueryServiceStatusEx`, the pattern `PanPipeTransport` uses) or cache. Not measured.

**CS-7** `Pan/TakenOverControllers.cs:215` `feedbackAgain ?? []` allocates a list per 1 ms poll; `:332-343` `KeyOf`
formats `$"{model}#{index}"` per path-less controller per poll and `Find` compares strings. **CS-8**
`VirtualPadReports.cs:34`, `ViiperConnection.cs:216`: a report array and an encrypted packet array per state (two per
millisecond per DualSense with sensors on). Reuse buffers under the existing `_writeGate`.

**CS-9** `ControllerSupportRemoval.cs:79,117-134`: comment says "by path", `StopProcesses("viiper")` has no path
check (`SisrInstallation.StopForRemoval` and `WindowsViiperHost.EndLeftovers` do); `sisr-removal.md` promises "one
elsewhere stays". `Kill(entireProcessTree)` on a user's VIIPER for another program.

**CS-10** `ControllerSupportService.cs:719-724,733`: `GitHubReleaseClient` can throw `JsonException`/`UriFormatException`
(not caught → generic toast, no row error); `RefreshAsync()` in `finally` is not cancellable and can replace the
propagating exception (CS-12). **CS-11** `Pan/PanClient.cs:729-770`, `Dispatch`: a throwing `Lost` handler escapes
from `Drop` inside `RequestAsync`'s catch; a throwing `TornDown` handler costs the link ("Verbindung zu Pan nach einem
Fehler geschlossen"). Raise through a guarded `Raise(Action)` as `LetGoOfPuckAsync` already does.

**CS-12** `UsbipDriver.cs:22-39`: `FileVersionInfo.GetVersionInfo(driver)` without the guard `HidHideInstallation`
has; registry reads in `UninstallEntries()` likewise. **CS-13** `Pan/PanEmulation.cs:320-322,530`,
`SteamControllerXboxPad.cs:199-201`, `ViiperXboxPads.ShutdownAsync`: `await _gate.WaitAsync()` without timeout while
a tick may hold it for `HidingRequestTimeout` 25 s / `ControllerRequestTimeout` 45 s; exit (also for a Velopack
update) can stall ~1 min. **CS-14** `GitHubReleases.cs:153-157` `catch { File.Delete(targetFile); throw; }`.

**CS-15** `Pan/PanEmulation.cs` 1057 (generation machine + copy-follow + puck let-go counting + `Session`/
`GuardedDevice` decorators), `Pan/TakenOverControllers.cs` 948 (1 kHz feed + per-model hide state + layouts +
settings + inner `Controller`), `Pan/PanClient.cs` 850 (keeper + framing + heartbeat + puck-restart choreography),
`ControllerSupportService.cs` 782 (driver install + Pan update + Steam closing + SISR removal + health rows).
`pan.md` documents `GuardedXboxPads` as a separate decorator; it is a private class inside `PanEmulation`. Extract
`GuardedXboxPads.cs`, `PuckLetGo`, `ModelHiding`, `PanRequestChannel`, `SisrRemovalFlow`/`PanUpdateFlow`.
**CS-16** `ControllerSupportService.cs:528-545`: invariant relies on every caller; make it local. **CS-17**
`tests/Arcadia.Core.Tests/PanClientTests.cs`, `VerifiedDownloadTests.cs`, `ControllerSupportTests.cs`.

**Done well.** Narrow, typed elevation boundary (`PanSetup.InstallAsync`: `ArgumentList`, `--expect-hash`); pipe
server PID verified against `QueryServiceStatusEx` with `SERVICE_QUERY_STATUS` only; VIIPER on `127.0.0.1:0` with a
fresh 190-bit key, scrubbed environment, kill-on-close job object, negative key checks before any device; no key or
payload in logs (tested), refusals counted after the first line; fail-closed copy filter; `PadForwarder` with a
one-slot newest-wins queue on the reading thread; `PanTimeouts`/`AttachTimeout`/`SendTimeout`/`CommandTimeout`
everywhere; idempotent, ordered `Dispose` paths; realistic fakes (anonymous pipe with the real `Guardian`, hanging
attach, VIIPER that stops reading, declined UAC, Steam that won't close).
