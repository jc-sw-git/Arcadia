# Test plans

One test plan per feature iteration, as a folder (`iteration-1/`, `iteration-2/`, …): one file per
issue (`<issue, three digits>-<title>.md`, e.g. `015-settings-page.md`) and a `README.md` as the
overview with the list of files, the test plan review, the exploratory test and the acceptance tests.
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
  That keeps the history complete, and the test plan review knows what the refactoring touched.
- **Test case IDs** are `T<issue>-<running number>`, e.g. `T15-2`. What belongs to no issue
  (the groundwork in Iteration 1) is in `grundgeruest.md` with `G-<n>`. They never change; an
  outdated test case is ~~struck through~~ with a reason, not deleted.
- **Every test case is marked:** `E2E: yes` (to become an automated acceptance test in the
  pipeline) or `E2E: no (reason)` (e.g. real Steam Controller, UAC, real launcher installation).
  Files written before 2026-10-25 (Iteration 1) stay German and say `ja`/`nein`; new files are English.
- **Every test run is its own row** in the issue's "History" table: date, who,
  build and environment, result, consequence. Tests during implementation (demo data, virtual
  display, mechanical only) go there too, with an honest environment.
- **Failures** name cause and consequence: the new issue and where it went (fixed in this
  iteration, next iteration, backlog).
- **The ticks** are set by the owner's full test before the merge (see "Before the merge"). If the
  test plan review at the end of the iteration finds that a later issue changed the behaviour, the
  test case is open again and retested on the release candidate.

## Before the merge (owner 2026-10-25)

1. **Acceptance tests first:** the issue's test cases with `E2E: yes` are written as acceptance tests in
   the implementation PR (rules under acceptance iteration) before the owner tests. If a case can't be
   automated yet (e.g. headless UI before #191/#192), the PR says so per case.
2. **Codex review** comes as before as a comment in the PR.
3. **Manual test:** the owner runs the issue's test plan file completely, on the PR build, and sends
   feedback in one batch.
4. **Rework:** the implementation thread fixes the owner's feedback and the Codex comments together in
   one pass. A new thread takes over the PR only if the implementation thread is already very long; it
   then reads PR, issue and test plan file instead of the old thread.
5. The owner tests only what the rework changed, and merges.

## Course of an iteration

1. **Feature iteration N** (milestone "Iteration N"): implement issues, every PR extends the test plan.
2. **Final review** (code): checklist in the review issue, fix findings or plan them as issues.
   The checklist also asks whether `CLAUDE.md`, `docs/` and skills are still right.
3. **Test plan review** (Claude): all test plan files of the iteration against the state of `main`. Per file:
   does it still cover what later PRs changed (gap), and does it still describe the current behaviour
   (drift)? Result: test cases added or adjusted, and the list of what has to be retested manually on
   the release candidate, with reason; "nothing" is a valid result. On the release candidate the owner
   tests this list, then does an exploratory test (charter and time box are in the overview beforehand).
   - **Measurement:** the budgets from `CLAUDE.md` once on the owner's PC (Release build), as a row in the history.
   - **Update cases** are tested with a second release candidate (e.g. rc.1 → rc.2 by self-update),
     not only with the next release.
   - **Findings:** before the release only what breaks data, security or a core function is fixed;
     everything else becomes an issue for iteration N+1. New ideas from the test are only filed as issues.
4. **Retrospective** (own issue, only after review and test plan review): for each process
   the owner decides whether it stays, changes or is dropped.
5. **Release.**
6. **Acceptance iteration N** (milestone "Acceptance Iteration N"; Iteration 1's is still named
   "Abnahme Iteration 1"): the core flows (start, navigation, search, game start, settings and what N
   changes about them) become automated acceptance tests in the pipeline; acceptance iteration 1 first
   builds the infrastructure for it. The other test cases with `E2E: yes` become one issue each and run
   along in iteration N+1 (retro Iteration 1, owner 2026-10-24).
   Feature iteration N+1 starts only when the acceptance iteration is closed, so that changes
   in N+1 show breaks in features of N at once.
   Rules for the acceptance tests (owner 2026-10-24, #184):
   - They live in `tests/Arcadia.App.Tests` and talk only through `ArcadiaDriver` (keys, time, restart)
     or, for the real window, through `ArcadiaWindow` (click, real key, mouse wheel, layout, pixels, focus ring; #191)
     and what view models and window show. Steam and Epic read recreated files with made-up IDs there
     (`Fixtures/`, `ArcadiaDriver.StartWithLaunchersAsync`, #193); nothing opens a real launcher. Every test carries
     the IDs of its test cases as `[Trait("Testfall", "T15-1")]`;
     the iteration's overview lists them under "Acceptance tests".
   - **Red once:** every new acceptance test failed once before the PR because the feature was broken
     on purpose. The PR names, per test, the change that made it red.
   - **Codex review** per PR: does every test cover the text of its test case IDs, where are the gaps?
   - **Mutation tests** (Stryker.NET) once at the end of every feature iteration; surviving mutants become tests
     or `type:tech-debt` issues.
7. **Feature iteration N+1** starts with the large `type:tech-debt` findings from the review,
   now secured by the acceptance tests.

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
| T<issue>-2 | YYYY-MM-DD | Juri | v0.x.y-rc1, real PC | ❌ <cause> | #… (fixed in this iteration / iteration N+1 / backlog) |
```
