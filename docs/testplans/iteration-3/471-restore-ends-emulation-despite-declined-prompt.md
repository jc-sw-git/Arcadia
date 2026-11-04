# #471 "Controller wiederherstellen" ends the emulation even when the Windows prompt is declined

**Status:** Automated: T471-1, T471-2, T471-3 · QE: – · Open for the owner: T471-4

- [ ] **T471-1** The Steam Controller's pad runs in a game, "Controller wiederherstellen", decline the Windows prompt:
      the pad stays plugged in, Arcadia never let go of Pan (no "von Pan beendet"), the row shows "Die
      Wiederherstellung wurde in der Windows-Abfrage abgelehnt." · E2E: yes (fake platform:
      `SteamControllerXboxPadTests.A_declined_restore_prompt_keeps_the_emulation_running`,
      `ControllerSupportServiceTests.A_declined_restore_prompt_never_lets_go_of_Pan`)
- [ ] **T471-2** Same, accept the prompt: while it is up the pad stays; after it Arcadia lets go, the restore runs,
      the pad comes back; a let-go that fails after the prompt restores nothing. · E2E: yes (fake platform:
      `SteamControllerXboxPadTests.Restoring_the_controllers_while_the_pad_runs_ends_the_emulation_after_the_prompt_and_brings_it_back`,
      `ControllerSupportServiceTests.An_accepted_restore_prompt_lets_go_of_Pan_only_after_the_prompt`,
      `A_failed_let_go_after_the_prompt_restores_nothing_and_takes_Pan_up_again`)
- [ ] **T471-3** The elevated Pan gets no `go` (Arcadia ended, `abort`, its 5 min timeout) or another message: it ends
      with exit 9 (exit 3 for another message) and restores nothing; without `--run` (by hand) it restores at once;
      Arcadia's steps up to the `go` fit into Pan's wait. · E2E: no (elevated Pan; the wait and the message are unit
      tests: `RestoreHandOverTests`, `Arcadia_s_steps_from_the_restore_prompt_to_the_go_fit_into_Pan_s_wait`)
- [ ] **T471-4** **Owner:** real PC, Steam Controller in a game (Xbox pad active): "Controller wiederherstellen",
      decline the Windows prompt: the controller keeps working in the game without a drop-out. Again, accept it: the
      controller drops out briefly and comes back by itself; `pan.log` shows "Go received" before "Restore complete"
      (or "Nothing to restore"). · E2E: no (UAC, real Steam Controller)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T471-1, T471-2, T471-3 and all other tests (`dotnet test Arcadia.slnx`; Windows-only pipe tests skipped there) | 2026-11-04 | Claude | PR branch e541664, Debug, Linux cloud container | ✅ Pan 433, Core 2448, App 624 passed, 0 warnings | – |
