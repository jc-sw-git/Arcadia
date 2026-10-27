# #243 Pan, the guardian service (part A)

Part A (PR into `feat/replace-sisr`): the service, its setup with one UAC prompt, the update dialog, the uninstall,
`--restore`, and the VIIPER/usbip teardown. Part B (HidHide) gets its own cases. Needed: the Steam Controller with its
puck, a game of another launcher, `services.msc`, "Game controllers" = `joy.cpl`, Process Explorer or `pssuspend`
(Sysinternals) for a hang. Logs: `%APPDATA%\Arcadia\logs\arcadia.log` ("Pan: …"), `%ProgramData%\Arcadia\Pan\pan.log`
(administrators only), journal beside it. How it works: `docs/controller-support/pan.md`.

Automated: the Windows CI runner installs the real service (`tests/Arcadia.Pan.Tests/ServiceTests`, T243-7 … T243-12);
the player's side runs in the acceptance tests with a faked Pan (`PanGuardianTests`).

- [ ] **T243-1** Setup on a PC without usbip-win2 and without Pan: Einstellungen → Controller → "Treiber installieren"
      (or the row "Wächterdienst Pan fehlt" → "Installieren") asks for administrator rights exactly once; afterwards
      `services.msc` lists "Pan - Arcadias Guardian" (running, automatic, description in German on a German Windows),
      `%ProgramFiles%\Arcadia\Pan\Pan.exe` exists, `sc sdshow ArcadiaPan` gives users (IU) only LC RP LO RC, the warning
      row is gone and "Controller wiederherstellen" appears. `pan.log`: "Copied to … (SHA-256 …)", "Service started".
      · E2E: no (UAC and the real driver; the service's install: T243-7)
- [ ] **T243-2** Without Pan (service uninstalled by hand: `Pan.exe --uninstall` elevated): no Xbox controller of
      Arcadia's appears, the per-game mode is greyed out with "„Arcadia“ braucht den usbip-Treiber, VIIPER und den
      Wächterdienst Pan – …"; Pan installed again, the controller comes back by itself within a few seconds. · E2E: yes
- [ ] **T243-3** `arcadia.log` after start: "Pan: verbunden (…, Protokoll 1, Generation n)"; in a game with the pad,
      `pan.log` shows the emulation started with its generation and the held `viiper.exe`; the game plays as before (no
      added latency: Pan only receives heartbeats). · E2E: yes (registration and heartbeat)
- [ ] **T243-4** A teardown by Pan (e.g. `sc stop ArcadiaPan` elevated while a game runs with the pad): the Xbox
      controller leaves "Game controllers" at once, `viiper.exe` is gone, Arcadia shows the toast "… Pan wurde beendet und
      hat den Xbox Controller entfernt – er kommt zurück, sobald Pan wieder läuft"; Pan started again (Arcadia starts
      it itself), the controller is back within about 2 s with the next generation. · E2E: yes
- [ ] **T243-5** Pan's process killed in the Task Manager while emulating: the SCM restarts it, the restarted Pan undoes
      its journal (`pan.log`: "Recover…", `viiper.exe` ended), Arcadia shows "… Verbindung zu Pan verloren …" and the
      controller returns. · E2E: yes (the lost connection and the return)
- [ ] **T243-6** Arcadia closed normally: `viiper.exe` and the controller are gone, `pan.log` logs the planned exit, no
      teardown runs, Pan stops itself after 30 s without a client. · E2E: yes
- [ ] **T243-7** `Pan.exe --install --expect-hash <sha256>` (CI): exit 0, the copy in Program Files, the service running;
      a wrong hash installs nothing and exits 3; a wrong hash on an installed Pan leaves it running and unchanged, no
      `Pan.exe.new` left. · E2E: yes (Windows CI, `ServiceTests`, all three)
- [ ] **T243-8** The service's DACL gives users start, query and interrogate only (`sc sdshow`: the IU entry has only LC RP LO RC, no
      `WD`/`BU`/`AU` entry); `ImagePath` in the registry is quoted; the description is the system language's. · E2E: yes (CI)
- [ ] **T243-9** The pipe `ArcadiaPan` takes SYSTEM and the interactive user, not Everyone or Users; hello answers
      `HelloOk` with the generation. · E2E: yes (CI)
- [ ] **T243-10** Service stop while a child `viiper.exe` of the client is registered: the child is ended before the
      service reports stopped. · E2E: yes (CI)
- [ ] **T243-11** A client that sends no heartbeat for 3 s after `EmulationStarted`: Pan ends the forwarding process and
      sends `TornDown(Hang)` with the generation; the client acknowledges. · E2E: yes (CI)
- [ ] **T243-12** `Pan.exe --uninstall`: service gone (`sc query` → 1060), `%ProgramFiles%\Arcadia\Pan` gone. · E2E: yes (CI)
- [ ] **T243-13** Einstellungen → Controller with Pan missing and the switch off: the category shows the warning
      triangle, the library's header too (tooltip "Steam Controller: Wächterdienst Pan fehlt"); the row "Wächterdienst
      Pan fehlt" offers "Installieren", while "Steam Controller Kompatibilität" stays a switch whose status points to
      that row (no second install button). "Installieren" asks for UAC once; afterwards the row, the triangle and the
      header mark are gone and "Controller wiederherstellen" appears. A failed install (e.g. the prompt declined)
      shows its message in place of the row's subtitle with the error icon and how to go on, and in the row's panel;
      the switch's panel shows no error. · E2E: yes (acceptance tests; the real UAC by hand)
- [ ] **T243-14** An Arcadia update that brings a newer Pan: at start one dialog "Pan aktualisieren"; B closes it and
      the next start asks again; A asks for administrator rights once, `services.msc` shows the new version afterwards
      (`Pan.exe --version`). While the first-run dialog or the settings are open, no question. · E2E: yes (the dialog;
      the real update is manual)
- [ ] **T243-15** Arcadia ended in the Task Manager while a game runs with the pad: the Xbox controller leaves "Game
      controllers" within about a second, `viiper.exe` is gone, `pan.log`: "Client … gone while emulating", the usbip
      port detached, "torn down". After the next start the controller is back and nothing is listed twice. · E2E: no
      (a real crash; the fake Pan's side: T243-4)
- [ ] **T243-16** Arcadia suspended for 5 s (Process Explorer → Suspend, or `pssuspend Arcadia`) while a game runs with
      the pad: the controller goes after 3 s; resumed, the toast "… Arcadia hat 3 Sekunden nicht reagiert, Pan hat den
      Xbox Controller entfernt – er kommt gleich zurück" and the controller returns within about 2 s. Three such losses
      within 5 minutes: Arcadia gives up until the next start (toast). · E2E: no (a real hang)
- [ ] **T243-17** Sleep: PC put to sleep for over a minute while a game runs with the pad; after waking, no teardown
      (`pan.log` has no "hang"), the controller still works. Shutdown with a game running: Pan's pre-shutdown tears the
      emulation down (log after the next boot). · E2E: no
- [ ] **T243-18** Uninstalling Arcadia (Windows → Apps): after the SISR and data questions one UAC prompt for Pan; the
      service, `%ProgramFiles%\Arcadia\Pan` and `%ProgramData%\Arcadia` are gone. Declined: Arcadia is removed, Pan
      stays idle and `Pan.exe --uninstall` (an elevated prompt, exe from the old install folder or the repo) removes it.
      With Pan never installed, no UAC prompt at all. · E2E: no (uninstall hook and UAC)
- [ ] **T243-19** "Controller wiederherstellen" with nothing to restore: one UAC prompt, no error, `pan.log`: "Nothing to
      restore". With the service stopped by hand (`sc stop`, elevated) after `viiper.exe` was left running from a killed
      Arcadia: the action ends `viiper.exe` and detaches the port, "Restore complete". · E2E: no (UAC)
- [ ] **T243-20** A standard user (second Windows account, not administrator): `sc stop ArcadiaPan` and
      `sc config` are denied, `sc start ArcadiaPan` works; Arcadia in that account connects and emulates. · E2E: no
      (second account)
- [ ] **T243-21** Budget (Release build): Pan idle 0 % CPU and under 20 MB in the Task Manager; while a game runs with
      the pad, Pan stays under 1 % CPU; `arcadia.log`'s latency line ("Verzögerung im Mittel …") is as without Pan.
      · E2E: no (measurement)

## Part B1: Pan hides the Steam Controller (HidHide)

Part B1 (second PR into `feat/replace-sisr`): `Hide`, `Unhide`, `CheckHiding`, the teardown's show and restart, boot
clean-up, arrivals. No caller in Arcadia yet (#242): `tools/dev/PanDevClient` plays Arcadia (close Arcadia first).
Needed besides the above: HidHide (1.5.230 on the owner's PC), `HidHideCLI.exe --dev-list / --app-list / --cloak-state`
(elevated) to look at HidHide, Steam with its desktop layout (right trackpad = mouse) as the sign whether Steam has the
puck. How it works: `docs/controller-support/pan-hiding.md`.

- [ ] **T243-22** R12: `Run-Check-243-R12.ps1` (project files `spikes/243/`) on the owner's PC: Steam loses the puck at
      a hide and gets it back after an unhide without and with restart, after a killed and a frozen client and after a
      planned exit; the script notes "sofort" or "nach Fokuswechsel" per case, and HidHide is as before at the end.
      The result goes into `pan-hiding.md` → R12. · E2E: no (Steam and the real puck)
- [ ] **T243-23** `PanDevClient` → `hide`: status `hidden 5` (or the number of the puck's HID instances); `--dev-list` lists
      them, `--app-list` the client's path, cloaking on; Steam's trackpad mouse stops; `journal.json` lists exactly
      those entries and `CloakingWasOn`. `unhide`: `shown`, HidHide as before, Steam has the puck again. · E2E: yes (CI:
      `HidHideTests` with the real driver, the service test's hide)
- [ ] **T243-24** The user's own entries: a second pad hidden in HidHide by hand, cloaking on, `PanDevClient.exe` already
      allow-listed: `hide` and `unhide` leave the pad's entry, the allow-list entry and cloaking as they were (compare
      the three lists before and after). · E2E: yes (`HidingTests`, fake HidHide)
- [ ] **T243-25** States Pan refuses, each with `check` and `hide`: HidHide's "Inverse" ticked → `HidHideInverseMode`;
      cloaking off with a pad of the user's listed → `HidHideCloakingOffWithForeignEntries`; HidHide's configuration
      client open → `HidHideBusy`; nothing changes in HidHide. · E2E: yes (`HidingTests`, the state matrix)
- [ ] **T243-26** `hide`, then the client ended in the Task Manager: without any input Steam's mouse works again (or after
      a focus change, per T243-22); `pan.log`: "show the Steam Controller: ok", "restart the Steam Controller: ok";
      HidHide as before. · E2E: no (a real kill; the guardian's side: `HidingTests`)
- [ ] **T243-27** `hide`, `freeze`: after 3 s status `torn down Hang`, Steam has the puck; `resume`, `hide`: hidden again
      under the next generation. · E2E: no (a real hang; the guardian's side: `HidingTests`)
- [ ] **T243-28** `hide`, then the PC switched off hard (power button held) or reset: after the boot, before Steam is
      opened, `--dev-list` and cloaking are as before the hide; `pan.log`: "Journal: emulation … left open", the show
      and restart steps ok. · E2E: no (power loss)
- [ ] **T243-29** `hide`, then the puck unplugged and plugged into another USB port: its new instances are hidden too
      (`--dev-list`, `pan.log` "HidHide: n device(s) hidden"), Steam doesn't get the puck; `unhide` shows all. · E2E: no (USB)
- [ ] **T243-30** `quit` (goodbye without unhide): `pan.log` "exits with the Steam Controller still hidden", HidHide as
      before. `sc stop ArcadiaPan` (elevated) while hidden: shown before the service reports stopped. · E2E: no (the
      guardian's side: `HidingTests`)
- [ ] **T243-31** Budget: `hide` and `unhide` each answer within about 2 s (`devclient.log` times); Pan idle stays at 0 %
      CPU with the arrival watch (Task Manager), also while hidden. · E2E: no (measurement)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T243-2 … T243-6, T243-13, T243-14 | 2026-10-26 | Claude (thread) | Debug, Linux cloud container, acceptance tests with a faked Pan (`PanGuardianTests`), mechanical only | pass | – |
| T243-7 … T243-12 | 2026-10-26 | Claude (thread) | skipped on Linux (needs the Windows CI runner with administrator rights); first run on the PR's CI | open | read the CI run |
| Pan core, client, journal, teardown (`Arcadia.Pan.Tests`, `PanClientTests`, `GuardedXboxPadsTests`, service driver warnings) | 2026-10-26 | Claude (thread) | Debug, Linux cloud container, unit tests | pass | – |
| T243-4, T243-14 (rewritten after the QE review: the service stop's toast; the update question through the driver), T243-2 … T243-6, T243-13 | 2026-10-26 | Claude (thread) | Debug, Linux cloud container, acceptance tests with a faked Pan, each new case seen red once | pass | – |
| T243-7 … T243-12 | 2026-10-26 | Claude (thread) | Windows CI runner (elevated): the DACL check compared as a set, wrong-hash step added | fail (SACL audit entry in the DACL check, `IsConnected` race in `PanClientTests`) | fixed in 24b6b32 |
| T243-7 … T243-12 | 2026-10-26 | Claude (thread) | Windows CI runner (elevated), run 40380137687 on 24b6b32: the whole `ServiceTests` case passed | pass | – |
| Pan core after the QE re-check (`GuardianTests`: reused leftover listed once, planned stop keeps leftovers; `PanLogTests`) | 2026-10-26 | Claude (thread) | Debug, Linux cloud container, each new case seen red once | pass | – |
| After the code review comment on the PR (`PanClientTests`: a running teardown is "Pan räumt noch auf"; `SteamControllerXboxPadTests`: no loss counted for it) | 2026-10-26 | Claude (thread) | Debug, Linux cloud container, each new case seen red once | pass | – |
| After the third review (`JournalTests`: fallback written back, nonsense journals unreadable; `GuardianTests`: recovery result, restore after a torn journal; `PanClientTests`: cancelled request drops the link) | 2026-10-26 | Claude (thread) | Debug, Linux cloud container, each new case seen red once | pass | – |
| T243-7 (update step: a rejected update keeps the installed Pan running) | 2026-10-26 | Claude (thread) | added to `ServiceTests`; red only by reasoning (the old code deleted the service before the check); Windows CI run 40403971802 on 6776613 passed | pass | – |
| After the fourth review (`JournalTests`: a fallback that can't be written back is found by the next load; `GuardianTests`: recovery from the handed snapshot) | 2026-10-26 | Claude (thread) | Debug, Linux cloud container, each new case seen red once; the dispatch lock and the install rollback are verified by reading only | pass | – |
| T243-1 … T243-21 (owner's first run on his PC, Debug build) | 2026-10-26 | Juri | Windows 11, Debug from the IDE: no `Pan\Pan.exe` in the output, so "Installieren" failed with a path error shown at the wrong row; no triangle in the header; two install buttons; exceptions in the debugger at every retry | fail | fixed in this PR (next row) |
| T243-13 (rewritten: header mark, one install button, the error at the Pan row), `ControllerSupportServiceTests` (drivers checked while off), `PanClientTests` (no connect attempt without the service) | 2026-10-26 | Claude (thread) | Debug, Linux cloud container, each new case seen red once; `PublishPanForDebug` and the exception-free service check are Windows-only and verified by reading | pass | Juri's second run |
| T243-1 … T243-21 (owner's second run, whole test plan) | 2026-10-26 | Juri | Windows 11, Debug build from the IDE with `Pan\Pan.exe` published by the build, commit 94128e1 | pass | ready to merge into `feat/replace-sisr` |
| Pan core part B1 (`HidingTests`, `HidHideWireTests`; T243-24, T243-25 at the guardian) | 2026-10-26 | Claude (thread) | Debug, Linux cloud container, unit tests; mutations seen red: show without the forwarding check, hang watch during a hide | pass | – |
| T243-23 (real HidHide), Windows adapters (`HidHideTests`, device list, arrival registration, the service test's hide) | 2026-10-26 | Claude (thread) | Windows CI runner, first run on the PR | open | read the CI run |
| After the QE review of PR #252 (`HidingReviewTests`: stop while hidden, check during the hang watch, uninstalled HidHide, listing before the check; `HidingTests`: retry after a passing failure, retry leaves a new emulation alone) | 2026-10-27 | Claude (thread) | Debug, Linux cloud container, QE's cases red before the fix; retry scheduling and its emulation guard seen red by mutation | pass | – |
| T243-23 (real HidHide), Windows adapters (`HidHideTests`, device list, arrival registration, the service test's hide) | 2026-10-27 | Claude (thread) | Windows CI runner (Server 2025, HidHide 1.5.230 installed by `Install-HidHide.ps1`), run 40458024167 on 96f33aa: all real-driver tests ran, none skipped | pass | one fake test (`Cloaking_that_was_off_is_turned_off_again`) failed on the loaded runner: its 300 ms device timeout ran out on an instant fake; seen red locally with a 500 ms fake restart, fixed (10 s, short only where devices hang) |
