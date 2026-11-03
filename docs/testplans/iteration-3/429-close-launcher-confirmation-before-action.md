# #429 Close the launcher settings' confirmation before its action runs

No new manual test cases: the order of closing and confirming changes, nothing a player sees. It touches T83-7
(sign-out question). `SteamAccountTests.Confirming_the_sign_out_closes_the_question_before_signing_out_…` checks that the
question is closed when the sign-out starts, after A and after a click on "Abmelden". B stays covered by the existing
T83-7 test with its assertions unchanged; `SteamAccountTests.Abbrechen_in_the_sign_out_question_closes_it_and_keeps_the_sign_in`
covers A and a click on "Abbrechen".

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| New test against `main` | 2026-11-03 | Claude | Debug, Linux cloud container, Avalonia headless | ❌ 2 failed: the question was still open when the sign-out ran (A and click) | Fix |
| All App tests | 2026-11-03 | Claude | PR branch, Debug, same environment | ✅ 549 passed | – |
| QE: A on "Abbrechen" runs the action (mutation) | 2026-11-03 | Claude (QE) | PR branch, Debug, same environment | ❌ new "Abbrechen" test red (A) | Test kept |
| QE: all App tests | 2026-11-03 | Claude (QE) | PR branch, Debug, same environment | ✅ 551 passed | – |
