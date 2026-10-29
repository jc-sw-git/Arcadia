# Test plans

One test plan per feature iteration, as a folder (`iteration-1/`, `iteration-2/`, …): one file per
issue (`<issue, three digits>-<title>.md`, e.g. `015-settings-page.md`) and a `README.md` as the
overview with the list of files, the acceptance review, the exploratory test and the acceptance tests.
It is the only source for all manual tests of that iteration: what is to be tested, who tested what
and when, what went wrong and what came of it. Whoever tests or extends an issue reads only its file,
not the whole folder. How a PR and an iteration run: `docs/process/iteration.md`.

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
- **The ticks** are set by the owner's full test before the merge (`docs/process/iteration.md` → Before the merge). If the
  acceptance review at the end of the iteration finds that a later issue changed the behaviour, the
  test case is open again and retested on the release candidate.

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
