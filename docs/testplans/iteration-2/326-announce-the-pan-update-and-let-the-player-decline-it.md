# #326 Announce the Pan update before the Windows prompt opens, and let the player decline it

An installed Pan that isn't the bundled one is announced at start ("Neue Pan-Version") and updated only on [A];
declined or failed, Pan is switched off, Arcadia stops talking to the old Pan and waits until it has ended, with the
restore and the steps by hand if it doesn't. Design: artboards T326F01–F09, T326S01–S04 in the design artifact
(section "#326"). How it works: `docs/controller-support/pan-update.md`. Needed for the manual cases: Windows, a build
of this PR (Debug from the IDE publishes `Pan\Pan.exe`), Pan installed with "Treiber installieren" from another build
(e.g. `main`, or this branch before a change to `src/Arcadia.Pan`), `pan.log` (`%ProgramData%\Arcadia\Pan`), the
Steam Controller with its puck.

Automated: `PanUpdateAtStartTests` (T326-1 to T326-8; Pan's `--install`, `--restore`, the service and the UAC prompt
faked), `PanClientTests` (an unwanted Pan is let go silently and not reached again), `ControllerSupportServiceTests`
(switched off for the update, `MayTalkToPan`, the restore), `JournalTests` (a journal of a Pan before the firmware hold
is read with all it has to undo).

- [ ] **T326-1** Another Pan installed, Pan on: Arcadia starts with "Neue Pan-Version", hint "Danach fragt Windows, ob
      Pan installiert werden darf.", [A] Aktualisieren, [B] Ohne Pan weiter; no UAC prompt opens until [A]. [A]: "Pan
      wird aktualisiert" and the prompt; confirmed, the library with the toast "Pan: aktualisiert"; `Pan.exe --version`
      in Program Files is the bundled one. · E2E: yes (the real prompt by hand)
- [ ] **T326-2** [B] on the notice: "Pan wird beendet" with the countdown from 45; the old Pan's service stops (Services
      or `pan.log`) and the library shows with "Pan: nicht aktualisiert, Controller-Wächter ausgeschaltet"; the
      settings say Pan is off; the Steam Controller works in Steam as without Arcadia. The next start shows no notice.
      · E2E: yes (the real service in T326-10)
- [ ] **T326-3** [A], the UAC prompt declined: as T326-2 with the same toast. Update failed (e.g. `Pan.exe` locked):
      toast "Pan: Aktualisierung fehlgeschlagen, …", the error in the info panel of "Controller-Wächter (Pan)". · E2E: yes
- [ ] **T326-4** No Pan installed, or exactly the bundled one: straight into the library. Another Pan, but switched off:
      no notice, no prompt; Einstellungen → Controller: "Controller-Wächter (Pan)" reads "Neue Pan-Version verfügbar"
      with the blue i, its info panel starts with the blue box "Schaltest du den Controller-Wächter ein, …". · E2E: yes
- [ ] **T326-5** On that switch, [A]: "Pan wird aktualisiert …", the row can't be switched, the prompt opens;
      declined: stays off, toast "Pan: nicht aktualisiert, Controller-Wächter bleibt aus"; confirmed: on, toast "Pan:
      aktualisiert", the emulation starts. · E2E: yes
- [ ] **T326-6** A firmware hold from before the start (Arcadia ended during "Steam will die Firmware aktualisieren"),
      [A] pressed at once: the page shows "Pan wird aktualisiert" without a prompt, the firmware dialog comes first,
      the prompt only after it is closed. · E2E: yes
- [ ] **T326-7** The old Pan doesn't end (e.g. HidHide's Configuration Client open while declining, so its undo can't
      run): after 45 s "Controller freigeben", [A] Controller wiederherstellen, [B] Arcadia beenden. [A] confirmed:
      the library, "Pan: Controller wiederhergestellt, …". [A] declined: "Controller von Hand freigeben" with five
      steps and the error; following the steps, [A] Erneut prüfen counts down again and opens the library once the
      service ended. [B] ends Arcadia at both stages. · E2E: yes (the real stuck Pan in T326-11)
- [ ] **T326-8** While a Windows prompt is up or the countdown runs, no button ends Arcadia, starts a second prompt or
      reaches the library, settings or search. · E2E: yes
- [ ] **T326-9** Controller only: notice, countdown, both emergency stages and the settings switch work with the
      gamepad alone; the UAC prompt and HidHide's Configuration Client need mouse or keyboard (exceptions until #247).
      · E2E: no (real Windows prompt and Steam Controller)
- [ ] **T326-10** Real old Pan declined: within 45 s its service has stopped, `pan.log` shows its teardown, the puck and
      the Steam Controller are visible to Steam again. · E2E: no (real service, HidHide and puck)
- [ ] **T326-11** HidHide's labels: the steps of stage 2 ("Devices", "Applications", "Enable device hiding", how the
      Steam Controller and the puck are named) match the real Configuration Client. · E2E: no (real HidHide)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T326-1 to T326-8 | 2026-10-30 | Claude | PR branch, Debug, Linux cloud container, acceptance tests with a faked Pan; each seen red once by a mutation of the code it covers (update started without [A]; Arcadia still talking to the old Pan; Pan not switched off on a decline; notice with Pan off; switch on without the update; update during the firmware dialog; library before the old Pan ended; [B] quitting while busy) | ✅ | owner's PC open |
| T326 (unit) | 2026-10-30 | Claude | Same, `PanClientTests`, `ControllerSupportServiceTests`, `JournalTests` (an older Pan's journal; green at once, no mutation) | ✅ | – |
| T326-1, T326-2, T326-7 (look) | 2026-10-30 | Claude | PR branch, Debug, real app under Xvfb with `--demo`, keys; Pan faked by a local, uncommitted switch (due update, service never ending, 2 s countdown for the last image), as Linux has no Pan. Looked at: notice, countdown, stage 1, stage 2 with the error. Not looked at: the settings row (Linux shows Pan as unavailable), the real prompt | ✅ | owner's PC open |
