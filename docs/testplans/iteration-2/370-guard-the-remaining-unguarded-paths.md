# #370 Guard the remaining unguarded paths

An error nobody expected in a consumer of the controller input, in Pan's connection keeper or heartbeat, in a Pan event
subscriber, in the library refresh on window activation or in the toast's timer is logged with its stack trace and
shown once as a toast; the controllers, the keeper and the request go on.

No manual case: every path is reachable only through a consumer that throws, and every real consumer is written
"never throws"; the unit tests below throw on purpose.

| ID | Case | Test | E2E |
|---|---|---|---|
| T370-1 | A throwing `Sampled` or button handler is reported once; the controllers, `Polled` and the other handlers go on | `A_throwing_Sampled_or_button_handler_is_reported_once_and_the_controllers_keep_working` | no |
| T370-2 | A removed handler is no longer called (the guarded event list) | `A_removed_handler_is_no_longer_called` | no |
| T370-3 | A throwing setting of the taken-over controllers falls back to its safe value, is reported once, the controller is still read | `A_throwing_setting_is_reported_once_and_Feed_goes_on_with_its_safe_value` | no |
| T370-4 | A throwing `Lost` subscriber doesn't fault the request that dropped the link; the other subscribers hear it | `A_throwing_Lost_handler_does_not_fault_the_request_and_the_other_handlers_still_hear_it` | no |
| T370-5 | A transport that throws once is reported and the keeper connects on the next attempt | `A_throwing_transport_is_reported_once_and_the_keeper_keeps_trying` | no |
| T370-6 | The toast hides after its duration through the guarded task; a newer message keeps its own time; sticky stays | `ToastTests` | no |
| T370-7 | A connect that times out (the pipe's own timeout, not Arcadia closing) costs one attempt; the keeper connects on the next | `A_connect_that_times_out_costs_one_attempt_and_the_keeper_connects_on_the_next` | no |

Only checked by review: the `catch` in the SDL poll thread's run loop (pacing and the wait can't be made to fail from a
test) and the guarded library refresh on window activation (same `Forget` as the other window events).

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T370-1 to T370-6, all App and Core tests | 2026-11-01 | Claude | PR branch, Linux cloud container, Avalonia headless | ✅ (T370-1, -3, -4, -5 also ❌ once with the guards broken on purpose) | – |
| T370-7 (QE review) | 2026-11-01 | Claude | PR branch 066b9f1, Linux cloud container | ❌ `FirstAttempt` never completes: the keeper ends on the timeout | fix in the keeper's catch filters |
| T370-7 after the fix, all Core and App tests | 2026-11-01 | Claude | PR branch d29bdf0, Linux cloud container, Avalonia headless | ✅ | – |
