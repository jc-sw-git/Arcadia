# Test plans

One test plan per feature iteration, as a folder (`iteration-1/`, `iteration-2/`, …): one file per
issue (`<issue, three digits>-<title>.md`, e.g. `015-settings-page.md`, a copy of [TEMPLATE.md](TEMPLATE.md)) and a `README.md` with the
acceptance review, the release candidate tests and the exploratory test. The folder is the index:
`tools/dev/testplan-index.sh <n>` prints it. A PR touches only its own issue's file (and the README only for release
candidate tests), so parallel PRs don't conflict over the test plan.
It is the only source for all manual tests of that iteration: what is to be tested, who tested what
and when, what went wrong and what came of it. Whoever tests or extends an issue reads only its file,
not the whole folder.

## Rules

- **Test cases come from the specification, before any code:** the quality engineer derives them from the issue
  and its design and writes them into the issue; the PR copies them into the issue's file. Test level per case:
  one acceptance test without a window per acceptance criterion, variants as unit tests; a case only the real window
  can show (layout, pixels, a real click path) is `E2E: no (window)` and checked in the quality engineer's own run of
  the app, recorded with a screenshot or recording in the History. Issues on drivers or installation get manual
  cases for a fresh install and for a driver arriving while Arcadia runs.
- **Every PR that closes an issue extends the test plan** with a file holding that issue's test cases
  (Definition of Done in `CLAUDE.md`; the CI check `Testplan` checks it). If an issue needs no
  new manual tests (e.g. a refactoring of features already covered automatically or by
  existing test cases), it still gets a section with one line saying why,
  and names the existing test cases it touches:
  `# #67 Split MainViewModel` → "No new test cases: pure refactoring; touches T41-3 to T41-6."
- **Layout:** every issue file is a copy of [TEMPLATE.md](TEMPLATE.md). It shows at a glance what is left for the
  owner: a case is ticked (`[x]`) as soon as an automated test or the quality engineer's run passed it; the cases handed
  to him stay open and start with **Owner:**; the **Status:** line names which cases are automated, which the quality
  engineer ran and which are open for the owner.
- **Test case IDs** are `T<issue>-<running number>`, e.g. `T15-2`. What belongs to no issue
  (the groundwork in Iteration 1) is in `grundgeruest.md` with `G-<n>`. They never change; an
  outdated test case is ~~struck through~~ with a reason, not deleted.
- **Every test case is marked:** `E2E: yes` (to become an automated acceptance test in the
  pipeline) or `E2E: no (reason)` (e.g. real Steam Controller, UAC, real launcher installation).
  An `E2E: yes` case that can't be automated within its issue says `E2E: yes, deferred (reason)`; the
  acceptance review picks it up.
  Iteration 1's files stay German and say `ja`/`nein`; new files are English.
- **Every test run is its own row** in the issue's "History" table: date, who,
  build and environment, result, consequence. Tests during implementation (demo data, virtual
  display, mechanical only) go there too, with an honest environment.
- **Failures** name cause and consequence: the new issue and where it went (fixed in this
  iteration, next iteration, backlog).
- **The owner's test before the merge** covers only the cases the quality engineer hands him in the PR's
  **Review** (at most 10, those only he can run, plus a focus for exploratory testing); the rest is covered by
  the automated tests and the quality engineer's own run, recorded in the History. The acceptance review at the
  end of an iteration hands him at most 30 cases. The thread records the ticks and pushes.
- **Restart or installation → release candidate:** a case that needs a Windows restart or an installation,
  update or uninstall of Arcadia is marked `· RC` and never handed over in a PR; the iteration's overview lists it
  under "Release candidate tests", and the owner runs them on the release candidate. They don't count toward the 30.
  A push that changes only `docs/testplans/**` on top of a green commit skips build and tests and runs only the
  `Testplan` check (`tools/ci/detect-testplan-only.sh`). If the acceptance review at the end of the iteration finds
  that a later issue changed the behaviour, the test case is open again and retested on the release candidate.

## Acceptance tests

- They live in `tests/Arcadia.App.Tests` and talk only through `ArcadiaDriver` (keys, time, restart) and what the
  view models show; they open no window. Steam and Epic read recreated files with made-up IDs there
  (`Fixtures/`, `ArcadiaDriver.StartWithLaunchersAsync`); nothing opens a real launcher. Every test carries
  the IDs of its test cases as `[Trait("TestCase", "T15-1")]`;
  the issue's file lists them under "Automated tests" (test cases → test classes).
- **Red once:** every new acceptance test failed once before the PR because the feature was broken
  on purpose. The PR names, per test, the change that made it red (commit or file).
  The quality engineer reads that note and repeats the red run only if: the note is missing or vague; the test
  changed after its red run; the test checks something beside its criterion (e.g. a mock instead of the
  behaviour); the case is safety-critical (HidHide, VIIPER, firmware, crash); or the quality engineer wrote
  the test.
- **Review** per PR (quality engineer): does every test cover the text of its test case IDs, where are the gaps?
- **CI:** a PR runs the Core, Pan and App tests without a window; no CI test asserts a timing budget (budgets are
  measured by hand, `CLAUDE.md` → Performant).

## Window guards

- `tests/Arcadia.App.Tests/Guards/` opens the real headless window through `ArcadiaWindow` (click, real key, wheel,
  layout, pixels, focus ring). It holds only cross-page guards (focus ring, ring geometry, fades, consistent texts,
  embedded font, covers and icons, reused covers and search keys); they run on `main` only (`tools/ci/run-tests.sh`,
  `WINDOW_GUARDS=1`), and `ArcadiaWindow` anywhere else fails the test run.
- No window test per feature. A new window test is written only as a cross-page guard or to pin a layout bug that
  escaped to the owner, and only with the owner's OK in the refinement.
