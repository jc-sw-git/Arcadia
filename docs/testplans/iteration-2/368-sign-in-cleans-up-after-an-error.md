# #368 B after an unexpected sign-in error leaves the launcher page stuck

A Steam sign-in that ends with an unexpected exception now lets go of its run like every other exit: the launcher page
goes back to signed out, B goes back, and a new sign-in starts normally. The error still lands in `arcadia.log` and as a
toast.

| ID | Case | Test | E2E |
|---|---|---|---|
| T368-1 | The sign-in page fails with an exception: the account is signed out again, B (cancel) throws nothing, a second sign-in runs and ends the same way | `SteamOnlineAccountTests.An_unexpected_error_in_the_sign_in_ends_it_so_B_goes_back_and_a_new_sign_in_works` | no |
| T368-2 | After the exception the page is told: StateChanged goes from signing in to signed out, so "Anmeldung läuft" leaves the screen | `SteamOnlineAccountTests.An_unexpected_error_in_the_sign_in_tells_the_page_it_is_signed_out_again` | no |
| T368-3 | An older sign-in that fails after a newer one began leaves the newer one signing in, and B on it still works | `SteamOnlineAccountTests.An_older_sign_in_that_fails_after_a_newer_one_began_leaves_the_newer_one_running` | no |
| T368-4 | A cancellation nobody asked for (e.g. a timeout inside the browser) also ends the sign-in, and a new one starts | `SteamOnlineAccountTests.A_cancellation_nobody_asked_for_ends_the_sign_in_too` | no |

No manual case: the owner can't provoke an unexpected exception in the sign-in from the UI (a cancel or a network error
are results, not exceptions), so only the unit test with a browser that throws reaches it. E2E no for the same reason.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T368-1 | 2026-11-01 | Claude | `main` without the fix, Linux cloud container | ❌ as wanted | – |
| T368-1, all tests | 2026-11-01 | Claude | PR branch, Debug, Linux cloud container | ✅ | – |
| T368-1, -2, -4 | 2026-11-01 | Claude (QE) | `main` without the fix, Linux cloud container | ❌ as wanted | – |
| T368-3 | 2026-11-01 | Claude (QE) | PR branch with the current-run check removed, Linux cloud container | ❌ as wanted | – |
| T368-1 to -4, all tests | 2026-11-01 | Claude (QE) | PR branch, Debug, Linux cloud container | ✅ | – |
