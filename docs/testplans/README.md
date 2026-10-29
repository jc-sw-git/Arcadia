# Test plans

One test plan per feature iteration, as a folder (`iteration-1/`, `iteration-2/`, …): one file per
issue (`<issue, three digits>-<title>.md`, e.g. `015-settings-page.md`) and a `README.md` as the
overview with the list of files, the acceptance review, the exploratory test and the acceptance tests.
It is the only source for all manual tests of that iteration: what is to be tested, who tested what
and when, what went wrong and what came of it. Whoever tests or extends an issue reads only its file,
not the whole folder.

## Rules

- **Every PR that closes an issue extends the test plan** with a file holding that issue's test cases
  and a line in the overview
  (Definition of Done in `CLAUDE.md`; the CI check `Testplan` checks it). If an issue needs no
  new manual tests (e.g. a refactoring of features already covered automatically or by
  existing test cases), it still gets a section with one line saying why,
  and names the existing test cases it touches:
  `# #67 Split MainViewModel` → "No new test cases: pure refactoring; touches T41-3 to T41-6."
  That keeps the history complete, and the acceptance review knows what the refactoring touched.
- **Test case IDs** are `T<issue>-<running number>`, e.g. `T15-2`. What belongs to no issue
  (the groundwork in Iteration 1) is in `grundgeruest.md` with `G-<n>`. They never change; an
  outdated test case is ~~struck through~~ with a reason, not deleted.
- **Every test case is marked:** `E2E: yes` (to become an automated acceptance test in the
  pipeline) or `E2E: no (reason)` (e.g. real Steam Controller, UAC, real launcher installation).
  An `E2E: yes` case that can't be automated within its issue says `E2E: yes, deferred (reason)`; the
  acceptance review picks it up.
  Files written before 2026-10-25 (Iteration 1) stay German and say `ja`/`nein`; new files are English.
- **Every test run is its own row** in the issue's "History" table: date, who,
  build and environment, result, consequence. Tests during implementation (demo data, virtual
  display, mechanical only) go there too, with an honest environment.
- **Failures** name cause and consequence: the new issue and where it went (fixed in this
  iteration, next iteration, backlog).
- **The ticks** are set by the owner's full test before the merge (see "Before the merge"). If the
  acceptance review at the end of the iteration finds that a later issue changed the behaviour, the
  test case is open again and retested on the release candidate.

## Before the merge (owner 2026-10-25)

1. **All tests in the issue's PR:** unit and integration tests for new logic, and an acceptance test for
   every `E2E: yes` case (see "Acceptance tests"), before the owner tests. A case that can't be automated
   within the issue is marked deferred (see Rules) and named in the PR with its reason.
2. **Manual test:** the owner runs the issue's test plan file completely, on the PR build, and sends
   feedback in one batch.
3. **Review:** the quality engineer thread reviews and adds the acceptance tests before the owner tests
   (`docs/personas/quality-engineer.md`); its findings are in the PR description, section **Review**.
4. **Rework:** the implementation thread fixes the owner's feedback and open review findings together in
   one pass. A new thread takes over the PR only if the implementation thread is already very long; it
   then reads PR, issue and test plan file instead of the old thread.
5. The owner tests only what the rework changed, and merges.

## Process metrics (owner 2026-10-27, from Iteration 3)

Few numbers, captured where the work happens, evaluated in each iteration's retro (table in the retro issue).
Without them the AI-assisted QA process is a story, not evidence.

1. **Bugs before the merge, by source:** every code PR description has a line `Process metrics: found by QE n,
   Codex n, owner test n, other n | owner test rounds n`. The QE thread fills in its findings, the implementation
   thread the rest after the owner's test.
2. **Bugs after the merge:** issue label `escaped-bug`, the issue names the PR that introduced it. Counted at
   iteration end, no other effort.
3. **Rework after the owner's test:** the `owner test rounds` value above (fix batches after the first test).
4. **Cost per issue:** threads per issue and how many ended over ~150k tokens; counted by the coordinator from
   the thread list at iteration end. Token numbers only where available without manual work.
5. **Once, in the Iteration 3 retro:** how many of Iteration 2's Codex findings were real.

## Course of an iteration

1. **Feature iteration N** (milestone "Iteration N"): implement issues, every PR extends the test plan.
2. **Final review** (code, by Codex across the whole iteration): checklist in the review issue, fix findings or plan them as issues.
   The checklist also asks whether `CLAUDE.md`, `docs/` and skills are still right.
3. **Acceptance review** (Claude, owner 2026-10-25): all test plan files of the iteration against `main`:
   - **Drift and gaps:** does each file still cover what later PRs changed, and still describe the current
     behaviour? Test cases are added or adjusted.
   - **Deferred cases:** each `E2E: yes, deferred` case is automated now or tested manually.
   - **End-only and agreed cases:** the measurement, the update cases and the mutation tests below.

   Result: the list of what the owner tests manually on the release candidate, with reason ("nothing" is
   valid), then an exploratory test (charter and time box are in the overview beforehand).
   - **Measurement:** the budgets from `CLAUDE.md` once on the owner's PC (Release build), as a row in the history.
   - **Update cases** are tested with a second release candidate (e.g. rc.1 → rc.2 by self-update),
     not only with the next release.
   - **Mutation tests** (Stryker.NET): surviving mutants become tests or `type:tech-debt` issues.
   - **CI health check** (owner 2026-10-25): from the iteration's workflow runs on GitHub, flag crossed
     thresholds and other anomalies (new flaky tests, failing scheduled jobs, artifact storage running out).
     Thresholds: a PR's CI run over ~10 min, the test step over ~8 min, more than ~150 window tests
     (`ArcadiaWindow`), runs queueing while PRs are open. Each finding becomes an issue for iteration N+1.
   - **Findings:** before the release only what breaks data, security or a core function is fixed;
     everything else becomes an issue for iteration N+1. New ideas from the test are only filed as issues.
4. **Retrospective** (own issue, only after code review and acceptance review): for each process
   the owner decides whether it stays, changes or is dropped.
5. **Release.**
6. **Iteration N+1** starts with the large `type:tech-debt` findings from the code review. There is no
   separate acceptance iteration any more (owner 2026-10-25): the tests come with each issue, so a change
   that breaks an earlier feature shows at once. "Abnahme Iteration 1" was the last one; it built the
   infrastructure and automated Iteration 1's cases.

## Acceptance tests (owner 2026-10-24, #184)

- They live in `tests/Arcadia.App.Tests` and talk only through `ArcadiaDriver` (keys, time, restart)
  or, for the real window, through `ArcadiaWindow` (click, real key, mouse wheel, layout, pixels, focus ring; #191)
  and what view models and window show. Steam and Epic read recreated files with made-up IDs there
  (`Fixtures/`, `ArcadiaDriver.StartWithLaunchersAsync`, #193); nothing opens a real launcher. Every test carries
  the IDs of its test cases as `[Trait("TestCase", "T15-1")]`;
  the iteration's overview lists them under "Acceptance tests".
- **Red once:** every new acceptance test failed once before the PR because the feature was broken
  on purpose. The PR names, per test, the change that made it red (commit or file).
  The quality engineer reads that note and repeats the red run only if: the note is missing or vague; the test
  changed after its red run; the test checks something beside its criterion (e.g. a mock instead of the
  behaviour); the case is safety-critical (HidHide, VIIPER, firmware, crash); or the quality engineer wrote
  the test.
- **Review** per PR (quality engineer): does every test cover the text of its test case IDs, where are the gaps?

## Template for an issue

File `iteration-<n>/<issue, three digits>-<title>.md`:

```markdown
# #<issue> <title>

- [ ] **T<issue>-1** <What to do, what must happen; controller only, B goes back> · E2E: yes
- [ ] **T<issue>-2** <…> · E2E: no (<reason>)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T<issue>-1 | YYYY-MM-DD | Claude | PR #…, demo data | ✅ | – |
| T<issue>-2 | YYYY-MM-DD | owner | v0.x.y-rc1, real PC | ❌ <cause> | #… (fixed in this iteration / iteration N+1 / backlog) |
```
