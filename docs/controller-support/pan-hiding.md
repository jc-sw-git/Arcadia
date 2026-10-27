# Pan hides the real Steam Controller (#243, part B1)

How Pan ([pan.md](pan.md)) hides the real puck from every program but Arcadia with HidHide, and shows it again in
every teardown. B2 adds HidHide's setup, the settings rows and the notices; #242 is the first caller in Arcadia. Until
then `tools/dev/PanDevClient` stands in for Arcadia. What HidHide does to Steam: [emulated-steam-controller.md](emulated-steam-controller.md).

## Protocol

Still protocol version 1: `Hide` (7) and `Unhide` (8) were part of it, a part A Pan answers them `Unsupported`.
New: `CheckHiding` (9) → `HidingState` with a reason or `None`; `Hide` → `Hidden` (value: how many of the puck's HID
instances are hidden now, 0 = not connected yet) or `Rejected(reason)`; `Unhide` → `Shown` or `Rejected(reason)`.
Reasons: `HidHideMissing` (not installed, or its driver not running), `HidHideUnsupported` (version or an IOCTL it
doesn't know), `HidHideInverseMode`, `HidHideCloakingOffWithForeignEntries`, `HidHideBusy` (its configuration client
has it open; HidHide allows one program at a time), `HidingFailed` (Pan's log says why), `StillHidden`.

- `Hide`/`Unhide` only within the current generation's emulation, so every teardown undoes the hiding too.
  `EmulationStopped` while HidHide holds a change of Pan's → `StillHidden`: the client unhides first (a restart still
  pending doesn't count). `PlannedExit` while hidden → a full
  teardown (reason `PlannedExit`, no notice).
- `Unhide` is trusted to come after Arcadia removed its copy (#242 keeps that order); every unplanned path goes
  through the teardown, which checks that the forwarding is gone.
- A rejected `Hide` may have changed HidHide already (e.g. hidden, then the restart failed): the client sends `Unhide`
  after any rejected `Hide` (#242).
- While the client's `Hide`/`Unhide` runs (up to ~20 s if a device step hangs), its heartbeats wait on the pipe; the hang
  watch pauses and restarts its clock afterwards.

## Hiding

- Driver interface, not the CLI (`HidHideWire`, `HidHideDriver`): `\\.\HidHide`, IOCTLs `CTL_CODE(32769, 2048…2054,
  METHOD_BUFFERED, FILE_READ_DATA)` (get/set allow list, hidden list, cloaking, read inverse), lists as REG_MULTI_SZ.
  The control device is opened per call and closed at once; "in use" is retried 5 × 200 ms.
- Version: `HidHide.sys` from the service's `ImagePath`, file version; accepted `1.2` ≤ v < `2.0` (inverse mode came with
  1.2; another major may change the interface). An IOCTL answered with "invalid function" counts as unsupported.
- Checks before every hide (R13): installed, version, inverse off, and with cloaking off no device of the user's in
  the hidden list (listed puck entries don't count): turning cloaking on would hide those too.
- What: only `HidingList` — the puck `HID\VID_28DE&PID_1304&…`, every instance Pan finds present itself
  (`CM_Get_Device_ID_List`, enumerator `HID`). Other Valve devices stay visible (#248, #249).
- Order: list the devices (may wait on the PnP lock), then read and check HidHide, journal (write-ahead), allow list (the client's image path in device form,
  `QueryFullProcessImageNameW(PROCESS_NAME_NATIVE)` on the pipe client's process), hidden list, cloaking on, then the
  puck restart (SetupAPI `DIF_PROPERTYCHANGE`/`DICS_PROPCHANGE` on `USB\VID_28DE&PID_1304\…`, as devcon and
  `pnputil /restart-device` do), so programs that have it open lose it. A restart Windows can only do by replug
  (`DI_NEEDRESTART`) fails and stays pending.
- Entries that were there before (the user hid the puck, or allow-listed Arcadia) aren't journaled and never removed;
  `CloakingWasOn` keeps what cloaking was before the first hide.
- Arrival: Windows' `CM_Register_Notification` for HID interfaces whose link names `VID_28DE&PID_1304`; while Pan hides,
  new instances are hidden and the puck restarted (its own restart finds nothing new). No polling.

## Showing again

- Teardown steps 3 and 4 (after ports detached and forwarding ended, both `Ok`; otherwise the puck stays hidden and
  journaled for the next trigger): "show the Steam Controller" removes exactly the journaled entries from the current
  lists, turns cloaking off only if it was off, each change journaled once made, with `PuckRestartPending` written
  first; "restart the Steam Controller" restarts it while pending.
- HidHide changes run one at a time (a gate shared by hide, unhide, arrival, check and the teardown's steps); IOCTLs run
  inline (no PnP lock), device listing and restart have a 10 s timeout and may be left behind (they change nothing
  journaled).
- A partial show (HidHide busy, its driver restarting, a failed restart) after the forwarding was gone is retried by
  itself after 5 s, 30 s, then every 2 min while nothing emulates; a restart that only a replug does stops after those.
- Triggers: crash/kill (`ClientGone`), hang, service stop or pre-shutdown, a stop with an incomplete teardown journaled,
  planned exit while hidden, Pan's start after a crash or power loss (`RecoverAsync`), `--restore`, uninstall. The undo
  goes through the driver whatever its version reads as; only with HidHide's service gone and its control device too
  (uninstalled with its lists) the journal drops the entries.
- Last resort for the user, if Pan can't undo: HidHide's Configuration Client (admin) unticks the puck, removes
  Arcadia from the application list and turns hiding off; uninstalling HidHide empties its lists. Pan's later undo
  removes only what is still there.
- Journal: `HiddenDevices`, `AllowListed`, `CloakingWasOn`, `PuckRestartPending`; a journal naming a hidden device that
  isn't on `HidingList` counts as unreadable (an undo must never remove one of the user's entries).

## Steam's focus change (R12)

Spike #239 saw Steam pick up a hidden or shown puck only after a focus change. Pan runs in session 0 and can't cause
one. The check script (project files `spikes/243/`, `Run-Check-243-R12.ps1`) asks on the owner's PC: hide, unhide
without and with a puck restart, kill and freeze of the client, planned exit.

Result (owner's PC, 2026-10-27, R12 and the Steam-button check R12b, `spikes/243/`): **no focus change needed.**
- Steam loses the puck at once when Pan hides it, and gets it back at once when Pan shows it with a restart (unhide,
  killed client): the Steam button stops and works again without any click. Only Steam's desktop layout (trackpad
  pointer) came back after a focus change in R12; any click or Arcadia's own window change gives one.
- While hidden the trackpad still moves the pointer: the controller's own mouse mode, which it switches on when no
  program drives it (Steam doesn't see it then). In #239 SDL3 read the puck and switched it off; #242 must open the
  puck with SDL3 right when it hides, or the pointer moves during the game.
- Every path of Pan's was clean: hide (13 instances, ~2.4 s with the puck restart), unhide, kill, hang (3 s), planned
  exit; HidHide as before and the journal empty each time.

## Tests

`HidingTests` (fake HidHide and devices: order, write-ahead, the HidHide state matrix, the user's entries, every
teardown trigger, failing/hanging steps, arrival, the hang watch during a hide), `HidHideWireTests` (IOCTL codes,
REG_MULTI_SZ, driver path, the device list). On the Windows CI runner `tools/ci/Install-HidHide.ps1` installs the
HidHide 1.5.230 (pinned to the owner's version, hash and signature checked; on about half the runners its updater registration hangs, the step moves on after 90 s); with its control device answering, `HidHideTests` and the service test hide through the real
driver and check HidHide is back as before; otherwise they skip with the reason.
