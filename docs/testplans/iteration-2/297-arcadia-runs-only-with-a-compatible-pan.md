# #297 Arcadia runs only with a compatible Pan

An installed Pan that isn't byte for byte the bundled one is replaced at start. #326 changed how: the update is
announced and runs on [A], declining switches Pan off (`326-announce-the-pan-update-and-let-the-player-decline-it.md`);
the cases struck through below are replaced there. How it works: `docs/controller-support/pan-update.md`. Needed for the
manual cases: Windows, a build of this PR (Debug from the IDE publishes `Pan\Pan.exe`), Pan installed with "Treiber
installieren", `pan.log` (`%ProgramData%\Arcadia\Pan`). A "different" Pan is made by installing the Pan of another
build (e.g. the release before, or this branch rebuilt after a change to `src/Arcadia.Pan`).

**Owner's manual run deferred to the acceptance review** (owner, 2026-10-28): all cases below are tested on the
release candidate, installed with the Velopack installer, instead of before the merge.

Automated: `PanUpdateAtStartTests` (T297-4; T297-2 within T326-4; the rest moved to T326-x), unit tests
`PanCompatibilityTests` (comparison, unreadable exe) and `ControllerSupportServiceTests` (due flag, update).

- [ ] ~~**T297-1** Another Pan installed (older): Arcadia starts with the page "Pan wird aktualisiert" (spinner, "Der
      Start dauert deshalb etwas länger"), no question; the UAC prompt comes, confirmed, the library shows with the
      toast "Pan: aktualisiert"; `Pan.exe --version` in Program Files is the bundled one. · E2E: yes (acceptance test;
      the real prompt and exe in T297-9)~~ Replaced by T326-1 (#326).
- [ ] **T297-2** No Pan installed, or exactly the bundled one: Arcadia starts straight into the library, no page, no
      prompt. · E2E: yes
- [ ] ~~**T297-3** Pan switched off in the settings ("Controller-Wächter (Pan)" Aus) and another Pan installed: the update
      runs all the same; Pan stays switched off. · E2E: yes~~ Replaced by T326-4 (switched off: no update at start, the settings offer it) (#326).
- [ ] **T297-4** The update installs no driver, needs no network, and neither closes Steam nor waits for a running
      Steam game; a missing usbip-win2 or HidHide stays the "Treiber installieren" row. · E2E: yes (fakes; offline on
      the real PC in T297-10)
- [ ] ~~**T297-5** A firmware hold from before the start (Arcadia ended during "Steam will die Firmware aktualisieren"):
      the firmware dialog comes first, the update only after it is closed. · E2E: yes~~ Replaced by T326-6 (#326).
- [ ] ~~**T297-6** UAC prompt declined: "Pan muss aktualisiert werden" with "… in der Windows-Abfrage abgelehnt", [A]
      Erneut versuchen, [X] Pan entfernen, [B] Arcadia beenden; no button reaches the library, settings or search;
      [A] asks again and, confirmed, the library shows. · E2E: yes (the real prompt by hand)~~ Replaced by T326-3 (#326).
- [ ] ~~**T297-7** Update failed (Pan's exit code): the same page with the error naming `pan.log`; [X] removes Pan with
      one prompt, then the library shows, toast "Pan: entfernt und ausgeschaltet", Pan switched off; that prompt
      declined, the page stays. · E2E: yes~~ Replaced by T326-3 and T326-7 (no failed page, no "Pan entfernen") (#326).
- [ ] ~~**T297-8** On the failed page [B] ends Arcadia; while the prompt is up, [B] does nothing. · E2E: yes~~ Replaced by T326-7 and T326-8 (#326).
- [ ] **T297-9** Downgrade: a Pan *newer* than the bundled one (installed by a later build) is replaced by the older
      bundled one; `pan.log` shows the install, the service runs, the Xbox pad works in a game. · E2E: no (real UAC,
      service, two builds)
- [ ] **T297-10** Network unplugged, another Pan installed: the update works the same (no download). · E2E: no (real
      PC offline)
- [ ] **T297-11** Arcadia closed on the page (window ✕ or Task Manager) before the prompt is answered: nothing changed,
      the next start shows the page again. · E2E: no (real process end during UAC)
- [ ] ~~**T297-12** Controller only: the failed page's [A], [X] and [B] work with the gamepad alone; the UAC prompt
      itself needs mouse or keyboard (exception until #247). · E2E: no (real Windows prompt and Steam Controller)~~ Replaced by T326-9 (#326).

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T297-1 … T297-8 | 2026-10-28 | Claude | PR branch, Debug, Linux cloud container, acceptance tests with a faked Pan; each seen red once by a mutation of the code it covers | ✅ | Windows CI and owner's PC open |
| T297-8 (prompt up) | 2026-10-28 | Claude (QE review) | PR branch, Debug, Linux cloud container, new acceptance test with the Windows prompt held open; red once each by dropping the busy guard of [B] and of [X] | ✅ | – |
