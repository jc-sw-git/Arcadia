# #399 Show a repeated unexpected error again after a quiet period

"Source" = the place that reports, keyed as in the issue: per handler in `GuardedHandlers` (toast context
"Controller-Eingabe"), per name in `TakenOverControllers` ("Controller für Spiele"), per context in `PanClient`
("Verbindung zu Pan halten", "Heartbeat an Pan", "Pan-Ereignis"). "Toast" = "Unerwarteter Fehler (<context>) – Details
stehen in arcadia.log." (text unchanged). "Summary line" = a log line of a repeat without stack trace, with the latest
message and the count since the source's previous line. All time is fake (`FakeTimeProvider`); acceptance tests use
`ArcadiaDriver` with a fake Pan whose heartbeat or connection throws. No new UI, so no controller-only or B case.

**Assumptions the cases make** (implicit in the spec, stated so the implementation can't miss them):
- A source quiet for exactly 10:00 toasts again; 9:59 doesn't (the quiet period is "at least 10 minutes").
- The first error's line (with stack trace) counts as the source's line of that minute: the first summary line comes
  at the earliest one minute after it.
- The old lines "weitere Fehler … werden nicht gemeldet" / "weitere Fehler von … werden nicht gemeldet" are gone: they
  would be wrong now.
- An `OperationCanceledException` (shutdown) is no error of the source, as in `AppErrors.Report` today: no toast, no
  count, it doesn't end or start a streak.

**Quiet period and steady error**

- [ ] **T399-1** (S1) "Heartbeat an Pan" throws once: one toast, `arcadia.log` has the error with stack trace. 10 minutes
      without an error, then it throws again: a second toast, and the log has this error with stack trace again.
      · E2E: yes
- [ ] **T399-2** (S6) A source throws at 0:00, then again at 9:59 after a quiet stretch: no toast, the repeat is counted.
      From 9:59 it is quiet until 19:59 (exactly 10:00): it toasts again. The quiet period starts at the source's last
      error, not its first. · E2E: no (variant of T399-1, unit test with fake time)
- [ ] **T399-3** (S2) "Heartbeat an Pan" fails every 5 s for one hour of fake time: exactly one toast (the first);
      `arcadia.log` has the first error with stack trace and at most one summary line per minute for "Heartbeat an Pan"
      (≤ 60 lines), each naming the latest message and the count since the previous line; the first line plus the counts
      add up to all 720 failures. · E2E: yes
- [ ] **T399-4** (S3) A handler of the SDL poll event throws on every poll, 1,000 times a second, for 3 minutes of fake
      time: one toast "Unerwarteter Fehler (Controller-Eingabe)", the log has the first error with stack trace and at
      most 3 summary lines; the other handlers of the same event get every poll, the button events still reach their
      handlers. · E2E: no (variant of T399-3; a throwing handler can't be planted through `ArcadiaDriver`; unit test in
      the Input tests)
- [ ] **T399-5** A pending count is written with the source's next line, never by a timer: a source throws 30 times in
      40 s and then stops; for the next hour of fake time no summary line appears. When it throws again after the quiet
      period, the log gets the 30 not yet logged repeats and the new error with stack trace, and a toast. A source that
      stops with a count of 0 writes no line about it. · E2E: no (unit test with fake time)

**Sources**

- [ ] **T399-6** (S4) "Heartbeat an Pan" fails with a `TimeoutException` and is toasted; 3 minutes later it fails with an
      `IOException`: no new toast; the next summary line names the `IOException`'s message. · E2E: no (variant of T399-3,
      unit test)
- [ ] **T399-7** (S5) "Heartbeat an Pan" is in a steady error (toasted, failing every 5 s); "Verbindung zu Pan halten"
      fails for the first time: its own toast with its own context; both keep their own summary lines and counts.
      · E2E: yes
- [ ] **T399-8** Keys as today: two different handlers of the same SDL event each toast once (both "Controller-Eingabe");
      in `TakenOverControllers` "Rumble/Light", `LayoutOf`, `Forwarding` and `Changed` are four sources; in `PanClient`
      "Pan-Ereignis" is its own source. A repeat of one source never counts for another. · E2E: no (unit tests per place)
- [ ] **T399-9** Scope: an error reported through `AppErrors` elsewhere (e.g. a timer handler via `AppErrors.Guard`)
      twice within a minute still toasts and logs with stack trace both times. · E2E: no (unit test)
- [ ] **T399-10** After Arcadia starts, every source has no history: its first error toasts. A second instance of the
      helper (Arcadia started again) toasts the same error again; nothing is written to disk for it. · E2E: no (unit
      test; the restart is only a new instance)

**Robustness**

- [ ] **T399-11** Clock: a `TimeProvider` whose wall clock jumps 1 h back and 1 h ahead while its monotonic timestamp
      goes on normally: the quiet period and the per-minute lines follow the timestamp only (no early toast, no
      prolonged silence, no burst of lines). · E2E: no (unit test with a custom `TimeProvider`)
- [ ] **T399-12** Reporting throws: the toast sink throws, then the log throws: both swallowed; the SDL poll thread goes
      on and the next handler gets the event, Pan's heartbeat loop runs its next beat, the controller reads go on. A
      failed toast doesn't lose the count of the next summary line. · E2E: no (unit tests per place)
- [ ] **T399-13** Concurrent: 8 threads report the same source 10,000 times each at once: exactly one toast; the first
      line plus all counts written add up to 80,000 (no repeat lost or counted twice); two sources reporting from
      different threads at once each toast once. · E2E: no (unit test)
- [ ] **T399-14** An `OperationCanceledException` at a source (Pan's loops at shutdown): no toast, no count, no log line
      as an error; a real error right after it is the streak's first and toasts. · E2E: no (unit test)

**Performance**

- [ ] **T399-15** Good path: 100,000 raises of an SDL event with three handlers and no error allocate 0 bytes
      (`GC.GetAllocatedBytesForCurrentThread`); the error path allocates nothing per repeat between two lines (format
      only when a line is written). · E2E: no (unit measurement)
- [ ] **T399-16** 1 kHz raise path within the pad-latency budget (`docs/controller-support/pad-latency.md`): on the
      owner's PC, a game with a taken-over controller for 10 minutes; `arcadia.log`'s "Verzögerung Lesen → VIIPER" and
      "Abstand der Lesevorgänge" are no worse than before the change. Handed to the owner only if the PR changes the
      good path of `Raise` or `Forwarding`; otherwise covered by T399-15. · E2E: no (real controller and game)

**Shared with #433**

- [ ] **T399-17** The throttle keeps the live repeat count of a source's current streak where the toast's entry can read
      it (for #433's "×N", T433-19): 37 repeats within the quiet period read as 37; after the quiet period the new toast
      starts at 0. Reading it allocates nothing. · E2E: yes, deferred to #433 if #399 merges first (no consumer before)

No case needs a Windows restart or an installation; no manual case for the owner except T399-16 under its condition.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
