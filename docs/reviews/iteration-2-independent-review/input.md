# Input (`src/Arcadia.Input`)

SDL3 gamepad source, polls and gestures, mouse mode, `SendInput`, Steam desktop-layout filter, device tree. All
files read; P/Invoke signatures and struct layouts checked.

**Assessment.** Good shape for a native-interop hot path: one lock around every SDL call, `Poll` never throws,
events raised outside the lock with a reused list, hook callbacks swallow everything before returning to Windows,
the whole SDL surface behind `ISdlGamepads` so 29 tests drive hot-plug, let-go, rumble and errors without hardware.
No serial or device path reaches the log. Nothing Critical. To fix: a missing `_open` re-check in the poll's locked
section; let-go pauses polls without releasing anything; any throwing consumer handler ends controller input for the
session; the loop body around `Poll` is unguarded; the "8 ms" poll is really a 15.6 ms `Thread.Sleep` on Windows.

| ID | Sev | Cat | Finding |
|---|---|---|---|
| INP-1 | Medium | Robustness | Poll runs against a quit SDL after a concurrent let-go; stale pad handle possible |
| INP-2 | Medium | Robustness | Let-go pauses polls without releasing buttons, mouse buttons or hold timers |
| INP-3 | Medium | Robustness | A throwing consumer handler ends controller input for the whole session (= CS-3) |
| INP-4 | Low | Robustness | `Run` loop body outside `Poll` is unguarded on a background thread |
| INP-5 | Low | Performance | Normal-mode poll sleeps on the 15.6 ms system tick, not 8 ms |
| INP-6 | Low | Performance | Synchronous file log per button press on the poll thread (= APP-6) |
| INP-7 | Low | Robustness/Perf | Device-tree enumeration under `_sdlLock` at every XInput connect |
| INP-8 | Low | Robustness | `SetScope` can block the UI thread up to 2 s; hooks linger after a timed-out Off |
| INP-9 | Low | Robustness | `SDL_Quit` runs on whichever thread calls `ReadControllers(false)` |
| INP-10 | Info | Performance | Per-poll allocations, ~33 P/Invokes per pad per read |
| INP-11 | Low | Robustness | Expected-failure paths log `ex.Message` only, also for unexpected exceptions |
| INP-12 | Low | CodeQuality | `SdlGamepadSource` mixes five concerns; two `ReadControllers` with different meanings |
| INP-13 | Info | Product/Security | Desktop filter drops all injected input, not only Steam's (accessibility tools) |
| INP-14 | Low | Tests | Gaps: let-go race, releases on let-go, `AnyNow` timeout, loop guard, throwing handler |

**INP-1** `SdlGamepadSource.cs:302-329` (`Poll` checks `_letGo` outside the lock), `:362-409` (no `_open` check
inside the lock), `:163-190`, `:473-489` (`CloseUnderLock` returns on `!_open`). `AnyNow` (`:243`) and
`ReadSteamControllers` (`:215`) do re-check. Window: poll passes `if (_letGo)`, `WhileLetGoAsync` on another thread
takes the lock, closes pads, `SDL_Quit`; the poll then calls `SDL_PollEvent` on a quit SDL and raises `Sampled` with
an empty list. If a device change were returned, `Connect` would `SDL_OpenGamepad` after Quit and the handle would
survive `Reopen` (use after free). Fix: `if (!_open) return;` first inside the lock. Medium/Low.

**INP-2** `SdlGamepadSource.cs:309-312`: during a let-go no `Polled`, no `_polls.Update`, no `MouseMode.Update`;
`Lose()` notifies via `Lost`, `ReadControllers(false)` notifies nobody. Pan hides a new controller model while RT is
held in the mouse mode → `SendInput` left button stays down system-wide for 1-3 s; in Big Picture an A already let go
can complete as a hold (`HoldGesture.cs:45-57`). Fix: synthesise one empty poll on entering let-go. Medium.

**INP-3** `SdlGamepadSource.cs:314-328,411-416`: `Sampled`/`Polled`/`ButtonPressed` run inside `Poll`'s try; any
exception → `Lose` → SDL quit, `Lost` raised, toast "Controller werden nicht erkannt (SDL3 fehlt…)". Consumers:
`routing.Press/Release`, `xboxPad.Feed`, `forGames.Others.Feed` (`App.axaml.cs:216-222`); only `MouseModeHost.OnPolled`
guards itself. Verified by the lead reviewer: `_lost = true`, nothing in the App recreates the source. For a
controller-first product the blast radius of one consumer bug is the whole input stack. The ControllerSupport review
reached the same conclusion from the other side (CS-3: `Feed` evaluates `ForwardingAllowed`, `Combos`, `LayoutOf`,
`Rumble` delegates unguarded, foreground queries among them). Fix: guard each event invocation separately, report via
`AppErrors` with stack, keep polling; keep `Lose` for `ISdlGamepads` failures. Tests for both. High on behaviour.

**INP-4** `SdlGamepadSource.cs:512-553`: `pacing.StartRead` (→ `_log`, `Process.GetCurrentProcess().TotalProcessorTime`
`:558-562`), `wait.Wait` P/Invoke run outside `Poll`'s try; `try/finally` has no catch → an exception on the
"Arcadia SDL gamepads" thread terminates the process. Cheap fix: `catch (Exception ex) { Lose(ex); }`.

**INP-5** `SdlGamepadSource.cs:538-545`: `Thread.Sleep(PollIntervalMs)` (8) in normal mode; `PreciseWait.cs` itself
documents 15.6 ms ticks and that Windows 11 ignores a raised resolution for a hidden window; nothing calls
`timeBeginPeriod`. Up to ~16 ms of the 50 ms press→focus budget; auto-repeat quantised. Use the `PreciseWait` already
in hand or measure and document the real interval. Medium.

**INP-6** `src/Arcadia.App/Input/GamepadRouting.cs:129` → `AppLog.Write` per press from the poll thread (details
APP-6); `Connect`/`Disconnect` log under `_sdlLock` (`:617-629,645`), during which `AnyNow`'s 2 ms `TryEnter` fails.

**INP-7** `SdlGamepadSource.cs:364-376` → `Connect` → `GamepadDevices.WhyIgnored` → `XusbDevices.List`
(`XusbDevices.cs:343-379,409-426`: `CreateFileW` + synchronous `DeviceIoControl` per XUSB device) under `_sdlLock`.
Fast reads gap, the hook's `AnyNow` answers "not in use" exactly when a pad connects; a driver that doesn't answer
freezes all input (no timeout). Classify outside the lock; cache per connect burst.

**INP-8** `SteamDesktopInputFilter.cs:102-140,144-160`, caller `MouseModeHost.cs:611` (UI thread): `_ready.Wait(1 s)`
+ `thread.Join(1 s)`; after a timed-out Off the hooks stay until the 30 s rehook timer. **INP-9**
`SdlGamepadSource.cs:163-190` ← `TakenOverControllers.cs:418-434` ← `SteamControllerForGames.cs:111-117`: `SDL_Quit`
on the caller's thread, possibly the UI thread after an await (not established). Low.

**INP-10** `GamepadPolls.cs:84-86` new `HashSet` per poll, `MouseMode.cs:200,211,259-261` LINQ closures while a
combo is pending, `SendInputOutput.cs:139` `[input]` array per call; `SdlGamepads.cs:91-115` 22 button + 6 axis +
touch + sensor getters per pad per read. Within budgets; listed for the hot path.

**INP-11** `SdlGamepadSource.cs:186-189,228-231`, `EmulatedXboxPads.cs:522-527,542-547`: `catch (Exception ex)` →
`_log(… {ex.Message})`, no stack, no toast. **INP-12** `SdlGamepadSource.cs` (648 lines): `ISdlGamepads` interface,
lifecycle/let-go/reopen, fresh look, read, three `ConcurrentQueue` effect mailboxes, thread loop, failure, devices;
`public ReadControllers(bool)` vs `private ReadControllers(long, bool)`; `IGamepadSource` has 13 members all stubbed
by `NullGamepadSource`. Extract `PadEffects`, `SdlSession`; split the interface into consumer and control parts.

**INP-13** `SteamDesktopInputFilter.cs:251-304`, `InjectedInputGate.cs:231-259`: `foreign = INJECTED && ExtraInfo !=
ArcadiaMarker` drops every other process's `SendInput` while a controller is in use: on-screen keyboards, voice
control, remote tools. Product decision; not stated in `docs/product/mouse-mode.md`. The marker is a public constant
any process can copy (harmless). **INP-14** `tests/Arcadia.Core.Tests/SdlGamepadSourceTests.cs`: add the five cases
named in INP-1..4; `FakeSdlGamepads` already supports them.

**Done well.** One `Lock` for every SDL call incl. the hook thread's 2 ms `TryEnter`; `Lose` runs every `Lost`
handler even if one throws; hardware-free testability (`ISdlGamepads`, pure decision functions, `TimeProvider`,
`IMouseKeyboardOutput`); `PadDevice` has no serial, `Path` never logged; source-generated `LibraryImport` with
correct blittable layouts and `SafeFileHandle`; fast reads measure themselves (gap p50/p99/max, CPU share every
10 min) and use a waitable timer instead of raising the global resolution; `MaxStep` clamps a stalled poll; held
rumble as renewed 2 s bursts so motors stop if Arcadia dies; hot-plug rules centralised in `WhyIgnored` with a logged
reason.
