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
(elevated) to look at HidHide, Steam's button (opens Steam's menu or Big Picture) as the sign whether Steam has the
puck: while hidden the trackpad still moves the pointer by the controller's own mouse mode (R12b). How it works: `docs/controller-support/pan-hiding.md`.

- [ ] **T243-22** R12: `Run-Check-243-R12.ps1` (project files `spikes/243/`) on the owner's PC: Steam loses the puck at
      a hide and gets it back after an unhide without and with restart, after a killed and a frozen client and after a
      planned exit; the script notes "sofort" or "nach Fokuswechsel" per case, and HidHide is as before at the end.
      The result goes into `pan-hiding.md` → R12. · E2E: no (Steam and the real puck)
- [ ] **T243-23** `PanDevClient` → `hide`: status `hidden 5` (or the number of the puck's HID instances); `--dev-list` lists
      them, `--app-list` the client's path, cloaking on; the Steam button no longer opens Steam's menu; `journal.json` lists exactly
      those entries and `CloakingWasOn`. `unhide`: `shown`, HidHide as before, Steam has the puck again. · E2E: yes (CI:
      `HidHideTests` with the real driver, the service test's hide)
- [ ] **T243-24** The user's own entries: a second pad hidden in HidHide by hand, cloaking on, `PanDevClient.exe` already
      allow-listed: `hide` and `unhide` leave the pad's entry, the allow-list entry and cloaking as they were (compare
      the three lists before and after). · E2E: yes (`HidingTests`, fake HidHide)
- [ ] **T243-25** States Pan refuses, each with `check` and `hide`: HidHide's "Inverse" ticked → `HidHideInverseMode`;
      cloaking off with a pad of the user's listed → `HidHideCloakingOffWithForeignEntries`; HidHide's configuration
      client open → `HidHideBusy`; nothing changes in HidHide. · E2E: yes (`HidingTests`, the state matrix)
- [ ] **T243-26** `hide`, then the client ended in the Task Manager: the Steam button opens Steam's menu again without any
      other input (R12b); `pan.log`: "show the Steam Controller: ok", "restart the Steam Controller: ok";
      HidHide as before. · E2E: no (a real kill; the guardian's side: `HidingTests`)
- [ ] **T243-27** `hide`, `freeze`: after 3 s status `torn down Hang`, Steam has the puck; `resume`, `hide`: hidden again
      under the next generation. · E2E: no (a real hang; the guardian's side: `HidingTests`)
- [ ] **T243-28** `hide`, then the PC switched off hard (power button held) or reset: after the boot, before Steam is
      opened, `--dev-list` and cloaking are as before the hide; `pan.log`: "Journal: emulation … left open", the show
      and restart steps ok. · E2E: no (power loss)
- [ ] **T243-29** `hide`, then the puck unplugged and plugged into another USB port: every present instance is hidden
      (`--dev-list`; a puck with a serial number keeps its instance ids on any port, so `pan.log` names newly hidden
      ones only for one without), Steam doesn't get the puck; `unhide` shows all. · E2E: no (USB)
- [ ] **T243-30** `quit` (goodbye without unhide): `pan.log` "exits with the Steam Controller still hidden", HidHide as
      before. `sc stop ArcadiaPan` (elevated) while hidden: shown before the service reports stopped. · E2E: no (the
      guardian's side: `HidingTests`)
- [ ] **T243-31** Budget: `hide` and `unhide` each answer within 5 s (`devclient.log` times; Windows' puck restart alone
      takes 2.4–3.9 s, owner 2026-10-27); Pan idle stays at 0 %
      CPU with the arrival watch (Task Manager), also while hidden. · E2E: no (measurement)

## Part B2: HidHide's setup, the settings rows, the notices, the uninstall

Part B2 (third PR into `feat/replace-sisr`): Arcadia without SISR (the App), Steam running. To make HidHide or every
part missing: uninstall HidHide (and usbip-win2, Pan) in Windows' Apps. How it works: `docs/controller-support/pan-setup.md`.

- [ ] **T243-32** HidHide missing: "Steam Controller Kompatibilität" greyed out, "Aus – es fehlt: HidHide", row "HidHide
      fehlt" with "Installieren"; A → one UAC prompt; then HidHide 1.5.230 in Windows' Apps, the row is gone and
      the switch, still greyed out until #214, reads "Nicht mehr nötig – …"; `pan.log` names the Nefarius signature, "HidHide 1.5.230 installed" (or "setup still runs … left running"
      after 2 min on a PC where its last step hangs) and the puck restart. · E2E: yes (`EmulationSetupTests`; the
      foreign-signed installer refused with exit 6: `ServiceTests` on the Windows runner)
- [ ] **T243-33** Every part missing (usbip-win2, HidHide, Pan uninstalled, SISR's folder removed): no part rows, the
      setting itself offers "Treiber installieren", the info panel names all four parts; A → VIIPER downloads without a
      prompt, then one UAC prompt for the rest and no installer window (usbip-win2's Inno wizard stays hidden); afterwards
      all four are there, `pan.log` names the SHA-256 and signature checks of both installers. UAC declined: the error stands at the
      first part left ("usbip-Treiber fehlt"), VIIPER stays. · E2E: yes (`EmulationSetupTests`)
- [ ] **T243-34** An old HidHide (e.g. 1.1.x from HidHide's releases) installed: row "HidHide-Version wird nicht
      unterstützt" with "Aktualisieren"; A → one UAC prompt, HidHide 1.5.230 afterwards. · E2E: yes (`EmulationSetupTests`)
- [ ] **T243-35** With the settings open: HidHide's Configuration Client opened → within 10 s the row
      "HidHide-Konfiguration ist geöffnet" ("… Bitte schließe sie."); closed → gone within 10 s. "Inverse" ticked in it
      (then closed) → "HidHide: Liste ist umgekehrt"; unticked → gone. · E2E: yes (`EmulationSetupTests`, `PanClientTests`)
- [ ] **T243-36** Pan's notices: `sc stop ArcadiaPan` (elevated) while Arcadia is in front → a toast; again while a game
      is in front (Arcadia in the background) → the overlay notice over the game, readable, gone after ~6 s; no toast
      waiting when Arcadia comes back. A notice during a View hold over the game stands below "Zurück zu Arcadia", whose
      frame keeps filling. · E2E: yes for which of the two (`EmulationSetupTests`, `PanGuardianTests`); no
      for the overlay over a real game
- [ ] **T243-37** Uninstall Arcadia (Windows' Apps): the question "Treiber für die Controller-Emulation auch entfernen?"
      lists usbip-win2 and HidHide, No is the default. No → Pan gone, both drivers stay. Reinstalled, Yes → one UAC
      prompt, Pan gone, then HidHide and usbip-win2 gone from Windows' Apps; the Steam Controller works in Steam.
      · E2E: no (Velopack's uninstall, UAC; the question's text: `ControllerSupportRemovalTests`)
- [ ] **T243-38** Pan removes the drivers only through their own uninstallers: HidHide by its MSI product code through
      `msiexec`, usbip-win2 by its `unins*.exe` under Program Files; anything else runs nothing. · E2E: no (unit:
      `HidHideSetupTests`; the real removal is T243-37)
- [ ] **T243-39** Sleep with the Xbox pad in (a game on "Arcadia" or Steam's Big Picture without a game, Arcadia
      behind): Windows' sleep, wake after ~10 s → the pad works again within ~30 s, no toast "VIIPER fiel …";
      `pan.log` says "The PC woke from sleep" and no "hang". Found in the owner's B2 test (2026-10-27): Pan tore the
      pad down 3.4 s after waking. · E2E: yes for "Pan's teardowns never make Arcadia give up"
      (`SteamControllerXboxPadTests`; the grace: `GuardianTests`); no for the real sleep
- [ ] **T243-40** "Controller wiederherstellen" three times within 5 minutes with the Xbox pad in → no toast "VIIPER
      fiel …", the pad comes back each time. After Arcadia gave up (e.g. viiper.exe ended in Task Manager four times,
      toast "… erst nach einem Neustart oder „Controller wiederherstellen“ wieder") → "Controller wiederherstellen"
      brings the pad back without restarting Arcadia. · E2E: yes (`SteamControllerXboxPadTests`)
- [ ] **T243-41** Uninstall HidHide (Windows' Apps), don't restart, open the controller settings: row "HidHide:
      Neustart ausstehend" ("Windows entfernt HidHide beim nächsten Neustart."), no button, the setting reads "Aus – es
      fehlt: HidHide". After the restart: "HidHide fehlt" with "Installieren". Found in the owner's B2 test
      (2026-10-27): before, "HidHide fehlt" only came after the restart. · E2E: yes (`EmulationSetupTests`; the registry
      signals: `HidHideWireTests`)
- [ ] **T243-42** Update usbip-win2 (or install it), don't restart: row "Windows-Neustart ausstehend" ("Bis zum Neustart
      kann der Steam Controller in Spielen aussetzen."), no "SISR neu starten". Found in the owner's B2 test
      (2026-10-27). · E2E: yes (`EmulationSetupTests`)

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
| T243-1 … T243-21 (owner's first run on their PC, Debug build) | 2026-10-26 | owner | Windows 11, Debug from the IDE: no `Pan\Pan.exe` in the output, so "Installieren" failed with a path error shown at the wrong row; no triangle in the header; two install buttons; exceptions in the debugger at every retry | fail | fixed in this PR (next row) |
| T243-13 (rewritten: header mark, one install button, the error at the Pan row), `ControllerSupportServiceTests` (drivers checked while off), `PanClientTests` (no connect attempt without the service) | 2026-10-26 | Claude (thread) | Debug, Linux cloud container, each new case seen red once; `PublishPanForDebug` and the exception-free service check are Windows-only and verified by reading | pass | the owner's second run |
| T243-1 … T243-21 (owner's second run, whole test plan) | 2026-10-26 | owner | Windows 11, Debug build from the IDE with `Pan\Pan.exe` published by the build, commit 94128e1 | pass | ready to merge into `feat/replace-sisr` |
| Pan core part B1 (`HidingTests`, `HidHideWireTests`; T243-24, T243-25 at the guardian) | 2026-10-26 | Claude (thread) | Debug, Linux cloud container, unit tests; mutations seen red: show without the forwarding check, hang watch during a hide | pass | – |
| T243-23 (real HidHide), Windows adapters (`HidHideTests`, device list, arrival registration, the service test's hide) | 2026-10-26 | Claude (thread) | Windows CI runner, first run on the PR | open | read the CI run |
| After the QE review of PR #252 (`HidingReviewTests`: stop while hidden, check during the hang watch, uninstalled HidHide, listing before the check; `HidingTests`: retry after a passing failure, retry leaves a new emulation alone) | 2026-10-27 | Claude (thread) | Debug, Linux cloud container, QE's cases red before the fix; retry scheduling and its emulation guard seen red by mutation | pass | – |
| T243-23 (real HidHide), Windows adapters (`HidHideTests`, device list, arrival registration, the service test's hide) | 2026-10-27 | Claude (thread) | Windows CI runner (Server 2025, HidHide 1.5.230 installed by `Install-HidHide.ps1`), run 40458024167 on 96f33aa: all real-driver tests ran, none skipped | pass | one fake test (`Cloaking_that_was_off_is_turned_off_again`) failed on the loaded runner: its 300 ms device timeout ran out on an instant fake; seen red locally with a 500 ms fake restart, fixed (10 s, short only where devices hang) |
| T243-7 … T243-12, T243-23 and the Windows adapters after the CI fixes | 2026-10-27 | Claude (thread) | Windows CI runner (Server 2025, HidHide 1.5.230), run 40461544152 on ed9e096: Pan 109/109 with the real driver, none skipped; Core 944, App 319 | pass | – |
| R12 (check script, first run) | 2026-10-27 | owner | Windows 11, Steam running, HidHide 1.5.230, Pan and PanDevClient from the PR package | partly | Pan's paths clean (journal empty, HidHide as before); showing again needs a focus change; pointer moved while hidden (likely the controller's own mouse mode): R12b with the Steam button pending; step 6 failed only on PanDevClient's status (`exited` overwritten by `disconnected`, fixed) |
| R12b (check script, Steam button) | 2026-10-27 | owner | as R12 | pass | Steam loses the puck at once at a hide and gets it back at once after an unhide and after a killed client; the pointer moving while hidden is the controller's own mouse mode; HidHide as before |
| B1 check script (`Run-Check-243-B1.ps1`, steps 1–6; step 7, the hard power-off, declined) | 2026-10-27 | owner | Windows 11, Steam running, HidHide 1.5.230, Pan and PanDevClient from the check package (Pan before the review fixes of 05147a5) | pass | T243-23, -24, -25 (inverse, cloaking off with a foreign device, configuration client open → `HidHideBusy`), -29, -30 pass; Steam button checked hidden and shown. T243-29: the puck keeps its 13 instance ids on another port (serial number), so nothing new to hide and no arrival line; the case's text was wrong, fixed. T243-31: hide 2.4–3.9 s, unhide ~2.5 s (puck restart), over the 2 s budget, within the 5 s the owner set then; Pan CPU 0.03 % hidden, 0.003 % shown. T243-28 still open |
| Full test plan run (T243-22 … T243-31) | 2026-10-27 | owner | Windows 11, Steam running, HidHide 1.5.230, Pan and PanDevClient from the check package built from bfa4040 | pass | – |
| After the security/robustness reviews of PR #252 (`IdleStopTests`: Pan stays without a client while a show retry runs; `HidingTests`: a new emulation waits while a retry changes HidHide, the pending retry is reported) | 2026-10-27 | Claude (thread) | Debug, Linux cloud container; both new guards seen red by mutation (removed), Pan tests green 3 runs in a row | pass | – |
| Part B2 (`EmulationSetupTests` T243-32 … T243-36, `EmulationPartsTests`, `ControllerSupportServiceTests`, `PanClientTests` T243-35, `ControllerSupportRemovalTests` T243-37, `HidHideSetupTests` T243-38) | 2026-10-27 | Claude (thread) | Debug, Linux cloud container, whole suite (Pan 133, Core 964, App 334); each new acceptance test seen red once by mutation (missing HidHide not a part, all-missing not offered on the switch, HidHide row without its fix, busy row dropped, notice always a toast); UAC, the real installers and the overlay over a game only on Windows | pass | T243-32's `ServiceTests` step on the Windows runner |
| usbip-win2 silent switches and hash check (`InstallerSignatureTests`: a signed installer with another SHA-256 is refused, the Inno switches) | 2026-10-27 | Claude (thread) | Debug, Linux cloud container; the installer of 0.9.8.1 inspected (Inno Setup 6.5, so `/S` showed the wizard); the hash check seen red by mutation (removed); the real silent install only on Windows (T243-33) | pass | – |
| T243-32 (`ServiceTests`: a signed installer that isn't HidHide's refused, exit 6, Pan still installed), T243-7 … T243-12, T243-23 after B2 | 2026-10-27 | Claude (thread) | Windows CI runner (Server 2025, HidHide 1.5.230), run 40498651454 on 50b49e3: Pan 138/138 none skipped, Core 969, App 334 | pass | – |
| After the overall review of #243 on PR #264 (`UninstallOrderTests`: no driver removal after an incomplete undo, order undo → drivers → stop file log → delete; `PanLogTests`: no line into a recreated folder) | 2026-10-27 | Claude (thread) | Debug, Linux cloud container; both guards seen red by mutation (early return removed, stop of the file log removed); Pan 141, Core 969 | pass | – |
| QE review of PR #264: `EmulationSetupTests` T243-32 (HidHide fails inside Pan's install, exit 6: Pan set up, the error at the HidHide row) and T243-34 (one prompt) added | 2026-10-27 | Claude (QE thread) | Debug, Linux cloud container, `EmulationSetupTests` 7/7, whole suite before (Pan 133, Core 964, App 334); both seen red by mutation (error shown at no row; a second install call) | pass | – |
| After the QE review of PR #264 (`ControllerSupportServiceTests`: a usbip install whose HidHide step failed records the pending restart; the drivers' outer timeout derived from Pan's budgets) | 2026-10-27 | Claude (thread) | Debug, Linux cloud container; the new case seen red before the fix | pass | – |
| QE review's optional points on PR #264 (`ControllerSupportServiceTests`: Pan's HidHide state goes with the settings; `MouseModeOverlayTests`: a notice leaves a running hold; `HidHideSetupTests`: another publisher's "HidHide…" MSI is never removed; `DriverRemovalTests`: the real HidHide entry, Windows runner only; `PanClientTests`: waits for the loss report) | 2026-10-27 | Claude (thread) | Debug, Linux cloud container; each new guard seen red once (old code or the check removed); the overlay only headless, not over a real game | pass | `DriverRemovalTests` on the Windows runner |
| Second overall review (Claude) on PR #264, point 1 (`PanClientTests`: `SteamControllerBeingShown` is "Pan räumt noch auf" for 10 s, no loss, then the pad goes on) | 2026-10-27 | Claude (thread) | Debug, Linux cloud container; seen red with the new branch disabled; whole suite: Pan 141, Core 970, App 337 | pass | – |
| Real-HidHide tests after the reviews (`DriverRemovalTests` T243-38: the real HidHide entry gives an msiexec `/x` by product code; `HidHideTests`; CI no longer hangs in HidHide's updater registration) | 2026-10-27 | Claude (thread) | Windows CI runner (Server 2025, HidHide 1.5.230), run 40507889490 on 818605a: Pan 147/147, Core 972, App 337, none skipped | pass | – |
| T243-39, T243-40 after the owner's B2 test (sleep: `GuardianTests` resume grace; `SteamControllerXboxPadTests` Pan's teardowns no loss, restore clears the count and a give-up; `GuardedXboxPadsTests` `TakenAway`; `ControllerSupportServiceTests` the restore event) | 2026-10-27 | Claude (thread) | Debug, Linux cloud container; each new check seen red once by mutation (grace removed, loss always counted, restore not clearing the count or the give-up, `TakenAway` false, the event not raised or not wired); Pan 142, Core 972, App 339 | pass | owner's manual run of T243-39/40 open |
| T243-41, T243-42 after the owner's second B2 test (`EmulationSetupTests`: the removal row without a fix and no Pan check, the restart row without SISR; `HidHideWireTests`: `DeleteFlag` and the pending deletion of the driver file) | 2026-10-27 | Claude (thread) | Debug, Linux cloud container; each seen red once by mutation (removal not checked, Pan asked anyway, the SISR row kept, either registry signal ignored); the real registry after an uninstall only on Windows | pass | owner's manual run of T243-41/42 open |
