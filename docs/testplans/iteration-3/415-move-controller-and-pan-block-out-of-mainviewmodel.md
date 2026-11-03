# #415 Move the controller and Pan block out of MainViewModel

No new test cases: refactoring without a visible change. The existing automated cases of the moved parts run unchanged
(only the path to the property changed, e.g. `Main.ControllerMode.ShowControllerSupport`, no assertion): T15-10,
T92-2, T127-2, T178-1 to T178-9, T212-2/3/5/6/7/10, T213-8 to T213-13, T214-10, T215-1 to T215-6,
T242-2/3/10 to T242-16/20, T243-2 to T243-6/13, T295-2/3/5/6, T297-4, T307-1/2, T308-2/3, T315-1, T326-1 to T326-8,
T340-1, T354-1, T357-2, T396-1.

The two fixes taken over from #377 have unit tests without a manual case: a confirmation closes before its action runs
(`RoutingTests.A_on_a_confirmation_closes_it_before_its_action_…`), and a disposed window leaves the static error event
(`WindowLifetimeTests`).

**Measurement** (budget: gamepad input to visible focus move < 50 ms): Release, Avalonia headless, 5,000 games, 400
D-pad presses per run, three runs each, alternating. The view model's share of a press (`Bild:` "Verarbeitung"): before
mean 1.6–1.8 ms, p99 6.2–6.6 ms; after mean 1.3–1.6 ms, p99 5.3–7.0 ms. No difference beyond noise. The owner's PC is
not measured for this PR.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| All App, Core and Pan tests | 2026-11-03 | Claude | PR branch, Linux cloud container, Avalonia headless | ✅ | – |
| Focus measurement before/after | 2026-11-03 | Claude | `main` and PR branch, Release, Linux cloud container, Avalonia headless | ✅ within budget | – |
