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
      triangle, the row "Wächterdienst Pan fehlt" (warning, "Installieren") stands below "Steam Controller
      Kompatibilität"; after the install the row goes and "Controller wiederherstellen" appears. · E2E: yes
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
