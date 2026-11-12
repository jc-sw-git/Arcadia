# #<issue> <title>

<!-- Copy to iteration-<n>/<issue, three digits>-<title>.md (the folder is the index, no list to update). Rules:
     README.md in this folder. For the owner's PRs the CI check "Testplan" checks this layout
     (tools/ci/check-testplan-file.sh). A file without new cases holds only the line "No new test cases: <reason>;
     touches T…". -->

**Status:** Automated: T<issue>-1 · QE: T<issue>-2 · Open for the owner: T<issue>-3

- [x] **T<issue>-1** <What to do, what must happen; controller only, B goes back> · E2E: yes
- [x] **T<issue>-2** <…> · E2E: no (<reason>)
- [ ] **T<issue>-3** **Owner:** <a case only the owner can run> · E2E: no (<reason>)
- [ ] **T<issue>-4** <needs a restart or an installation> · E2E: no (<reason>) · RC

Automated tests:

| Test cases | Tests |
|---|---|
| T<issue>-1 | `<AcceptanceTestClass>`; the variants in `<UnitTestClass>` (Core) |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T<issue>-1 | YYYY-MM-DD | Claude | PR #…, demo data | ✅ | – |
| T<issue>-3 | YYYY-MM-DD | owner | v0.x.y-rc1, real PC | ❌ <cause> | #… (fixed in this iteration / iteration N+1 / backlog) |
