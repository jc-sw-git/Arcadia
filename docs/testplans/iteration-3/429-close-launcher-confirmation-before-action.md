# #429 Close the launcher settings' confirmation before its action runs

No new manual test cases: the order of closing and confirming changes, nothing a player sees. It touches T83-7
(sign-out question). `SteamAccountTests.Confirming_the_sign_out_closes_the_question_before_signing_out_…` checks that the
question is closed when the sign-out starts, after A and after a click on "Abmelden"; B and A on "Abbrechen" stay
covered by the existing T83-7 test with its assertions unchanged.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| New test against `main` | 2026-11-03 | Claude | Debug, Linux cloud container, Avalonia headless | ❌ 2 failed: the question was still open when the sign-out ran (A and click) | Fix |
| All App tests | 2026-11-03 | Claude | PR branch, Debug, same environment | ✅ 549 passed | – |
