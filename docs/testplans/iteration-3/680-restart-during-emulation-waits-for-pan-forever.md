# #680 Arcadia restarted during an emulation waits for Pan's cleanup forever; its spinner keeps the window drawing

**Status:** Automated: T680-1, T680-3 · QE: – · Open for the owner: –

- [x] **T680-1** A new Arcadia connects while Pan tears down its predecessor's emulation (`ClientGone`, no notice): once the teardown ends, Arcadia acknowledges it by itself and starts its own emulation · E2E: no (Pan's protocol, client against the real guardian)
- [x] **T680-2** **Owner:** real PC, Pan on, a Steam Controller connected and "Läuft" in the controller settings; Settings → Lokalisierung → change the language → "Sprache anwenden". After the restart the header shows no "Controller wird eingerichtet …" after a few seconds, the controller settings show Pan running, and `arcadia.log` of the old instance ends with `Arcadia beendet.` (`pan.log`: "exits on purpose", no `ClientGone`) · E2E: no (real Pan, drivers, restart) · RC
- [x] **T680-3** A spinner turns only while its window is in front; it stands still while another program is in front and turns again when Arcadia comes back · E2E: no (control, no page flow)
- [x] **T680-4** **Owner:** real PC, Release, the real library: Arcadia in front, then 5 minutes in the background with nothing pressed: private memory stays flat (T636-6 again) · E2E: no (real library, Windows memory) · RC

Automated tests:

| Test cases | Tests |
|---|---|
| T680-1 | `PanClientTests.An_Arcadia_that_connects_while_Pan_tears_down_its_predecessors_emulation_starts_its_own_once_Pan_is_done` |
| T680-3 | `LoadingSpinnerTests.A_spinner_stands_still_while_its_window_is_not_in_front` |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T636-6 (memory and CPU profile at idle) | 2026-11-18 | owner + Claude | v0.3.0-rc.2, Release, installed, Windows 11, real library (905 games) | ❌ "Pan räumt noch auf" hung since a language restart; its spinner kept the window drawing: ~0.2 cores, 1.5 MB/s garbage; private memory 177 → 694 MB with Arcadia in the background, ~435 MB flat in front (trim every minute); managed heap not growing (two dumps 2 min apart: −49 KB) | this issue |
| T680-1 | 2026-11-18 | Claude | PR branch without the fix, Debug, Linux | ❌ times out waiting for the acknowledgement | the check goes red on the bug |
| T680-1, all `PanClientTests` | 2026-11-18 | Claude | PR branch, Debug, Linux | ✅ | – |
| T680-3 | 2026-11-18 | Claude | PR branch without / with the fix, Debug, Linux headless | ❌ / ✅ | – |
| Exit teardown | 2026-11-18 | Claude | Avalonia 12.1.5, decompiled `ClassicDesktopStyleApplicationLifetime` | `Shutdown()` passes `force: true` and skips `ShutdownRequested`; only `Exit` comes. Checked mechanically, not run | T680-2 for the owner |
| QE: T680-1, T680-3 | 2026-11-18 | Claude (QE) | PR branch without / with the fix (re-acknowledgement call, `IsActive` condition removed), Debug, Linux | ❌ / ✅ | – |
| QE: exit teardown on "Apply language" | 2026-11-18 | Claude (QE) | PR branch without / with `desktop.Exit` handler, Debug, Linux Xvfb, `--demo`, Settings → Localization → Deutsch → Restart | without: old instance's log ends at `Arcadia startet neu` (no teardown); with: `Arcadia beendet.` 0.25 s later, new instance starts | runs the exit teardown for real; Pan's side stays T680-2 |
| T680-2, T680-4 | 2026-11-18 | owner + Claude | PR branch 20a9590, Release (`dotnet run`), Windows 11, real library, Pan installed | ✅ old instance stops emulation 6, shows the controller, ends with `Arcadia beendet.`; `pan.log`: "Client exits on purpose", no `ClientGone`; the new instance runs emulation 7 1.5 s later. In the background 5 min: private bytes 523.6 → 521.4 MB (−0.4 MB/min), private working set 417 MB flat, CPU 1.6 % of one core | – |
