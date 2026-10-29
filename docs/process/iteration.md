# Course of a PR and of an iteration

Who does each step: `docs/personas/README.md`. Test plan format and acceptance tests: `docs/testplans/README.md`.

## Before the merge (owner 2026-10-25)

1. **All tests in the issue's PR:** unit and integration tests for new logic, and an acceptance test for
   every `E2E: yes` case (`docs/testplans/README.md` → Acceptance tests). A case that can't be automated
   within the issue is marked deferred (`docs/testplans/README.md` → Rules) and named in the PR with its reason.
2. **Review:** a fresh quality engineer thread reviews and adds the acceptance tests before the owner tests
   (`docs/personas/quality-engineer.md`); its findings are in the PR description, section **Review**.
3. **Manual test:** the owner runs the issue's test plan file completely, on the PR build, and sends
   feedback in one batch.
4. **Rework:** the implementation thread fixes the owner's feedback and open review findings together in
   one pass (one thread per PR, `docs/personas/README.md` → Cost rules).
5. The owner tests only what the rework changed, and merges.

**Marking tests as passed costs no CI run** (owner 2026-10-29): the thread records the owner's results in the
test plan file and pushes. A push that changes only `docs/testplans/**` skips build and tests (the `build` jobs
run with all steps skipped, so the required checks still report success) and only the `Testplan` check runs;
the PR is green again within a minute. That holds only if the commit before was green; anything else in the
push, or a red or still running predecessor, builds fully (`tools/ci/detect-testplan-only.sh`).

## Course of an iteration

Milestone "Iteration N" (from Iteration 2 with a theme prefix, e.g. `I3`; the milestone description is the scope).

1. **Issues:** every PR brings all its tests and extends the test plan (above).
2. **Acceptance review** (one issue for the whole iteration, owner 2026-10-25):
   - **Code review** by Codex across the whole iteration (`AGENTS.md`); its checklist also asks whether `CLAUDE.md`,
     `docs/` and the skills are still right. Findings are fixed or planned as issues.
   - **Drift and gaps** (Claude): all test plan files of the iteration against `main`. Does each file still cover what
     later PRs changed, and still describe the current behaviour? Test cases are added or adjusted.
   - **Deferred cases:** each `E2E: yes, deferred` case is automated now or tested manually.
   - **Measurement:** the budgets from `CLAUDE.md` once on the owner's PC (Release build), as a row in the history.
   - **Update cases** are tested with a second release candidate (e.g. rc.1 → rc.2 by self-update),
     not only with the next release.
   - **Mutation tests** (Stryker.NET): surviving mutants become tests or `type:tech-debt` issues.
   - **CI health check** (owner 2026-10-25): from the iteration's workflow runs on GitHub, flag crossed
     thresholds and other anomalies (new flaky tests, failing scheduled jobs, artifact storage running out).
     Thresholds: a PR's CI run over ~10 min, the test step over ~8 min, more than ~150 window tests
     (`ArcadiaWindow`), runs queueing while PRs are open. Each finding becomes an issue for iteration N+1.

   Result: the list of what the owner tests manually on the release candidate, with reason ("nothing" is
   valid), then an exploratory test (charter and time box are in the test plan overview beforehand).
   Before the release only what breaks data, security or a core function is fixed; everything else becomes an
   issue for iteration N+1. New ideas from the test are only filed as issues.
3. **Retrospective** (own issue, after the acceptance review): for each process the owner decides whether it
   stays, changes or is dropped. A conflict between DoD criteria decided on purpose is named there too.
4. **Release.**
5. **Iteration N+1** starts with the large `type:tech-debt` findings of the review. There is no separate
   acceptance iteration (owner 2026-10-25): the tests come with each issue.

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
5. **Once, in the Iteration 3 retro:** how many of Iteration 2's Codex findings were real (drop this line after it).
