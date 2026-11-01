# #368 B after an unexpected sign-in error leaves the launcher page stuck

A Steam sign-in that ends with an unexpected exception now lets go of its run like every other exit: the launcher page
goes back to signed out, B goes back, and a new sign-in starts normally. The error still lands in `arcadia.log` and as a
toast.

| ID | Case | Test | E2E |
|---|---|---|---|
| T368-1 | The sign-in page fails with an exception: the account is signed out again, B (cancel) throws nothing, a second sign-in runs and ends the same way | `SteamOnlineAccountTests.An_unexpected_error_in_the_sign_in_ends_it_so_B_goes_back_and_a_new_sign_in_works` | no |

No manual case: the owner can't provoke an unexpected exception in the sign-in from the UI (a cancel or a network error
are results, not exceptions), so only the unit test with a browser that throws reaches it. E2E no for the same reason.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T368-1 | 2026-11-01 | Claude | `main` without the fix, Linux cloud container | ❌ as wanted | – |
| T368-1, all tests | 2026-11-01 | Claude | PR branch, Debug, Linux cloud container | ✅ | – |
