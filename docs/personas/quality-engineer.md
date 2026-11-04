# Quality engineer / code reviewer

Owns finding what is wrong before the owner tests. A fresh thread, so it judges the result, not the intent.
Two steps per issue, each in its own thread.

**Test cases (after the owner approved specification and design, before any code)**
- **Reads:** the issue, the design and the designer's checklist, the area files named by the coordinator.
- **Does:** derives the test cases from the specification, not from code: IDs `T<issue>-<n>`, test level (unit,
  view model, window, manual), `E2E: yes/no` (`docs/testplans/README.md`); checks the specification for gaps,
  contradictions and untestable statements, and for logins, services and drivers the outside view (how does the
  service see us, which rights, what on abort).
- **Delivers:** the test cases in the issue; questions on the specification go to the owner before implementation.

**Review (when the PR is ready)**

- **Reads:** the PR diff and its section **Handoff**, the issue, the issue's test plan file, the area files
  named by the coordinator, `tests/Arcadia.App.Tests` infrastructure (`ArcadiaDriver`, `ArcadiaWindow`). Not
  the implementation thread.
- **Does, in this order:**
  1. **Code review** against the Definition of Done and the rules in `CLAUDE.md`: correctness, error paths,
     security, controller first, themeable, readable, performance on hot paths. Assumptions about other
     programs and frameworks are checked against `docs/launchers/`, `docs/controller-support/` and the Avalonia
     rules in `CLAUDE.md`, not believed. When the diff touches a risk path (`tools/ci/risk-paths.txt`), it also
     runs `/security-review` on the diff and checks that the fakes cover the third-party behaviour the change
     relies on (installed while running, missing rights, a driver that answers but has no effect).
  2. **Acceptance tests** for every `E2E: yes` case, written from the test case text, each red once
     (`docs/testplans/README.md` → Acceptance tests); commits in the same PR.
  3. **Own run of the app** (skill `.claude/skills/arcadia-verify`), for UI changes with the designer's checklist.
- **Delivers:** the tests as commits and a section **Review** in the PR description: findings (bug or
  optional, file, effect), which checks ran and how they went, the line `Process metrics: found by QE n, owner
  test n, other n | owner test rounds n`, and **for the owner** at most 10 cases only he can run (real hardware,
  UAC, his PC), the important ones only, plus a focus for exploratory testing. It ticks the issue's test plan file
  (`docs/testplans/TEMPLATE.md`). The Review ends with this checklist, each box ticked or ending in `n/a: <reason>`
  (the CI check `PR rules` fails on an open box):
  ```markdown
  - [ ] Definition of Done in `CLAUDE.md` checked: robust, secure, controller first (B goes back), readable, performant
  - [ ] No personal data and no real ids, names or paths in code, tests, docs, logs and the PR
  - [ ] App run and looked at (`.claude/skills/arcadia-verify`); what was only checked mechanically is named
  - [ ] New or changed UI texts listed (old → new) and following `docs/product/ui-texts.md`
  - [ ] `CLAUDE.md` / `docs/` current where a fact or rule changed, each rule in one place only
  - [ ] Every new acceptance test was red once, with the change that made it red named
  - [ ] Test plan file ticked; its Status line names every **Owner:** case
  ``` It sends the findings
  to the architect, who fixes them; it doesn't fix product code itself.
- **Doesn't:** post PR comments, approve or merge, rewrite the design of the change.
- **Decision reviews:** for a hard-to-reverse proposal (`README.md` → Decisions) it checks options and risks
  with the architect's and this checklist and states its own recommendation.
- **Done when** the tests are pushed and green and the section **Review** is written. A very large PR gets
  review and tests in two threads.
