# Independent code review before closing iteration 2

A second, independent review of `main` at `5c95f46` (merge of PR #303, replace SISR), requested by the owner beside
the review running in the project thread. It knows nothing of the iteration's history; it judges the code as it is
against `CLAUDE.md`'s Definition of Done. Focus: robustness, security, performance, modularity, scalability,
architecture, code quality. Meant to be compared with the project thread's findings; every finding carries an ID,
`path:line`, severity, confidence and a concrete scenario for that purpose.

| File | Scope | High | Medium | Low | Info |
|---|---|---|---|---|---|
| [core.md](core.md) | `Arcadia.Core`, `Launchers.Abstractions` (CORE-n) | 0 | 3 | 10 | 2 |
| [launchers.md](launchers.md) | `Arcadia.Launchers` (LNCH-n) | 2 | 4 | 10 | 1 |
| [input.md](input.md) | `Arcadia.Input` (INP-n) | 0 | 3 | 9 | 2 |
| [controller-support.md](controller-support.md) | `Arcadia.ControllerSupport` (CS-n) | 0 | 3 | 11 | 3 |
| [pan.md](pan.md) | `Arcadia.Pan`, `Pan.Core` (PAN-n) | 0 | 4 | 9 | 1 |
| [app.md](app.md), [app-architecture.md](app-architecture.md) | `Arcadia.App` (APP-n) | 0 | 7 | 11 | 2 |
| [cross-cutting.md](cross-cutting.md), [cross-cutting-tests-ci.md](cross-cutting-tests-ci.md) | tests, CI, docs, theme, dependencies, own cross-project findings (XC-n, XR-n) | 0 | 8 | 10 | 1 |
| **Total** | 116 findings, none Critical | **2** | **32** | **70** | **12** |

## Verdict

The code base is in better shape than its age suggests: the dangerous boundaries (elevation into Pan, downloads,
the named pipe, VIIPER's key, process starts, logs) are designed deliberately and mostly match their docs, the
exception nets and `AppErrors` discipline hold almost everywhere, hot paths were measured and written
allocation-aware, and the test suite (1,725 tests, all 372 test-case traits resolving to the test plans) is a real
asset. Nothing found would block a 0.x release on its own. Two High findings (uncatchable stack overflow from two
VDF parsers reading files any local program can write) should be fixed before the iteration closes; the Medium ones
cluster into a handful of themes below. Architecture debt is concentrated in six large classes and in launcher
coupling that contradicts the project's own plugin rule.

## Fix first (ordered)

1. **LNCH-1, LNCH-2** depth limit in `TextVdf`/`BinaryVdf` (the project's own `SteamAppInfo.Reader` shows how); a
   crafted `appmanifest_*.acf` is a crash loop every 2 s.
2. **INP-3 / CS-3, INP-4** an exception from any `Sampled`/`Polled`/`ButtonPressed` consumer, or from the loop body
   outside `Poll`, ends controller input for the session (INP-4: ends the process). For a controller-first product
   this is the largest blast radius found. Guard the event invocations separately from SDL failures.
3. **CS-4** `PanClient` keeper loop without a catch-all: one `IOException` from the log sink and Pan is never
   reconnected, silently.
4. **CORE-1, CORE-3, CORE-2** install poll not isolated per launcher (toast every 2 s), no timeout around
   scanners/monitors, endless 15 s full rescans after a cancelled install.
5. **LNCH-3, LNCH-6, APP-6, CS-6** steady background cost beside a running game: every Steam manifest parsed every
   2 s, Epic install tree walked every 2 s, a file open per log line on the input path, all kernel drivers enumerated
   every 2 s. None measured here; the budgets ask for a measurement when touched.
6. **PAN-1 / CS-1, PAN-2, PAN-3** usbip-win2 unpinned and its hash chosen by the unprivileged client (matters once
   Pan is signed, #271), a non-reading pipe client can starve the console user's Arcadia, a reconnect race lets the
   idle stop tear down a live emulation.
7. **XR-1** the embedded browser runs the sign-in script on any https host; the comment promises a host restriction
   the code doesn't have.
8. **APP-3, LNCH-4, APP-1, APP-4** update download without timeout, sign-in stuck after an unexpected error (B
   throws), the one `async void` outside `AppErrors`, a latent dialog-chaining bug.
9. **XC-1, XC-2, XC-4, LNCH-16, PAN-14, APP-15** notices, README and architecture docs describe the removed SISR
   architecture, omit HidHide and name classes that don't exist.
10. **XC-3, XC-5, XC-6, XC-8** CI job timeouts, Dependabot and SHA-pinned actions, three real-clock test
    assertions, analyzers at defaults.

## Themes across areas

- **Launcher coupling against the plugin rule.** `Arcadia.Launchers` → `Arcadia.Core` (LNCH-5), `KnownLaunchers`
  with a Steam flag in Core (CORE-8), App branching on `KnownLaunchers.Steam.Id` and referencing
  `Arcadia.Launchers.Steam` (APP-9), ControllerSupport bound to `SteamInstallation` (XR-2), settle times tuned to
  Steam/Epic in `LibrarySession` (APP-20). Each has a product reason; together they make Steam reachable from every
  layer. One decision (an `ISteamPresence` seam, ids in Abstractions, or a documented exception) resolves all five.
- **"Never throws" contracts that delegate to callers.** `Feed`, `ForwardingAllowed`, `Fed`, `Lost`/`TornDown`
  handlers, `InstallTracker.Poll` (CS-3, CS-5, CS-11, CORE-1): the hot loops trust callbacks they don't own.
- **Timeouts are thorough at the edges, missing in the middle.** Network, process and Pan requests all have them;
  scanner/monitor calls (CORE-3), the update download (APP-3), shutdown gates (CS-13) and the pipe flush (PAN-2) don't.
- **Docs drifted during the SISR → Pan switch.** README, notices, architecture README/app.md, `pan.md`,
  `pan-setup.md` each carry one or two stale facts; the 10 KB limit means fixes need splits, not appends.
- **Six classes carry the complexity.** `MainViewModel` 1320, `LauncherSettingsViewModel` 1277, `PanEmulation` 1057,
  `TakenOverControllers` 948, `SearchViewModel` 868, `PanClient` 850, plus `Guardian` ≈ 2,300 over four partials
  (APP-11, CS-15, PAN-11, INP-12). All are tested through fakes, so the splits are mechanical.
- **Logging on the hot path.** `AppLog.Write` opens the file per line under one lock from the UI thread, the SDL
  poll thread and Avalonia's trace listener (APP-6, INP-6).

## Method and limits

- Static review only: no .NET SDK in the review environment, nothing was built, run or measured. Performance
  findings are therefore "cost visible in the code", flagged Medium confidence where the budget impact depends on
  a measurement.
- Seven scoped reviewers each read their whole scope; the lead cross-checked project references, async/process-start
  patterns, launcher special-casing and the embedded browser, and re-verified in the source every High and most
  Medium findings named here (LNCH-1/2, INP-3, CS-4, PAN-1, PAN-2's code, APP-4, APP-6, XR-1, XC-1). Line numbers
  refer to `5c95f46`; one set was corrected (LNCH-1), the rest were spot-checked, not all re-read.
- Severity is the reviewer's; where the lead disagreed it says so in the block (XC-1 Medium, not High).
- Duplicates across scopes were merged: INP-3 = CS-3, INP-6 = APP-6 = XR-2's note, LNCH-18 = CORE-1.
- Not reviewed: `.axaml` beyond the theme/focus rules, `tools/dev` scripts, test plans' content, git history.
  Context the review noticed but didn't judge: 529 commits and 100 merged PRs between 2026-10-23 and 2026-11-01,
  which explains the documentation drift better than any single PR.
