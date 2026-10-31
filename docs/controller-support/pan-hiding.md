# Pan hides the real Steam Controller (#243)

How Pan hides the real puck from every program but Arcadia with HidHide, and shows it again in every teardown; every
other controller follows the same rules ([pan-hiding-controllers.md](pan-hiding-controllers.md)). What
HidHide does to Steam: [emulated-steam-controller.md](emulated-steam-controller.md); who may hold the puck during the
restart: [pan-puck-restart.md](pan-puck-restart.md).

## Protocol

- `CheckHiding` → `HidingState` with a reason or `None`; `Hide` → `Hidden` (value: hidden HID instances, 0 = puck not
  connected yet) or `Rejected(reason)`; `Unhide` → `Shown` or `Rejected`. Reasons: `HidHideMissing`,
  `HidHideUnsupported` (version or an IOCTL it doesn't know), `HidHideInverseMode`,
  `HidHideCloakingOffWithForeignEntries`, `HidHideBusy` (its configuration client has it open; HidHide allows one
  program at a time), `HidingFailed` (Pan's log says why), `HidingLost` (an answer to `CheckHiding`: cloaking off, the
  client off the allow list, an instance no longer hidden).
- `Hide`/`Unhide` only within the current generation's emulation, so every teardown undoes the hiding too.
  `EmulationStopped` or `PlannedExit` while HidHide holds a change of Pan's → a full teardown with its retries.
- `Unhide` is trusted to come after Arcadia removed its copy; every unplanned path goes through the teardown, which
  checks that the forwarding is gone. A rejected `Hide` may have changed HidHide already: the client sends `Unhide`
  after any rejected `Hide`.
- A hide or show takes ~2.5 s (up to ~4 s, the puck restart) and needs its own reply timeout of ~25 s (the 2 s request
  timeout would drop the link mid-hide); the hang watch pauses meanwhile. The puck restart takes the puck away from
  every program, Arcadia too; Arcadia shows a spinner and doesn't let the user switch again until it is done.

## Hiding

- Driver interface, not the CLI (`HidHideWire`, `HidHideDriver`): `\\.\HidHide`, IOCTLs `CTL_CODE(32769, 2048…2054,
  METHOD_BUFFERED, FILE_READ_DATA)` (get/set allow list, hidden list, cloaking, read inverse), lists as REG_MULTI_SZ.
  The control device is opened per call and closed at once; "in use" is retried 5 × 200 ms.
- Version: `HidHide.sys` from the service's `ImagePath`, file version; accepted `1.4.181` ≤ v < `2.0` (the driver of
  HidHide 1.5.230, which numbers its driver on its own; older ones get "Aktualisieren", another major may change the
  interface). An IOCTL answered with "invalid function" counts as unsupported.
- Checks before every hide: installed, version, inverse off, and with cloaking off no device of the user's in the
  hidden list (listed puck entries don't count): turning cloaking on would hide those too.
- What: only `HidingList` — the puck `HID\VID_28DE&PID_1304&…`, every instance Pan finds present itself
  (`CM_Get_Device_ID_List`, enumerator `HID`) on a hardware host, never Arcadia's copy on VIIPER's software host
  ([pan-puck-restart.md](pan-puck-restart.md)). Other Valve devices stay visible.
- Order: list the devices (may wait on the PnP lock), read and check HidHide, journal (write-ahead), allow list (the
  client's image path in device form), hidden list, cloaking on, then the puck restart (SetupAPI `DIF_PROPERTYCHANGE`
  on `USB\VID_28DE&PID_1304\…`), so programs that have it open lose it. A restart Windows can only do by replug
  (`DI_NEEDRESTART`) fails and stays pending.
- Entries that were there before (the user hid the puck, or allow-listed Arcadia) aren't journaled and never removed;
  `CloakingWasOn` keeps what cloaking was before the first hide.
- Arrival: `CM_Register_Notification` for HID interfaces whose link names `VID_28DE&PID_1304`; while Pan hides, new
  instances are hidden and the puck restarted. No polling. The puck keeps its instance ids on any USB port (serial
  number), so a replug while hidden needs no new entry.
- Steam loses the puck at once when Pan hides it and gets it back at once when Pan shows it with a restart; no focus
  change needed (only Steam's desktop trackpad pointer came back after one). While hidden the trackpad still moves the
  pointer (the controller's own mouse mode when no program drives it), so Arcadia opens the puck with SDL3 right when
  it hides.

## Showing again

- Teardown steps 3 and 4 ("show the hidden controllers", "restart the shown controllers"), only after ports detached
  and forwarding ended (otherwise the puck stays hidden and journaled for the next trigger): remove exactly the
  journaled entries, turn cloaking off only if it was off, each change journaled once made, `PuckRestartPending`
  written first; then restart the puck. `Unhide` removes the allow-list entry and cloaking only when no other
  controller stays hidden.
- HidHide changes run one at a time (a gate shared by hide, unhide, arrival, check and the teardown's steps); IOCTLs
  run inline (no PnP lock); device listing and restart have a 10 s timeout and may be left behind.
- A partial show (HidHide busy, its driver restarting, a failed restart) is retried after 5 s, then every 10 s while
  nothing emulates (an attempt only opens HidHide); a restart that only a replug does stops after two. Pan doesn't
  idle-stop while that retry runs, and a new emulation waits for a retry that is changing HidHide.
- Triggers: `ClientGone`, hang, service stop or pre-shutdown, a stop with an incomplete teardown journaled, planned
  exit while hidden, Pan's start after a crash or power loss (`RecoverAsync`), `--restore`, uninstall. The undo goes
  through the driver whatever its version reads as; only with HidHide's service and control device gone (uninstalled
  with its lists) the journal drops the entries.
- No hide and no puck restart while a Valve bootloader is connected (a firmware update is being flashed):
  [steam-controller-firmware.md](steam-controller-firmware.md).
- Last resort for the user if Pan can't undo (also in `docs/releases.md`): HidHide's Configuration Client (Start menu,
  admin) unticks the puck, removes Arcadia from the application list and turns hiding off; uninstalling HidHide
  empties its lists. Pan's later undo removes only what is still there.
- Journal fields: `HiddenDevices`, `AllowListed`, `CloakingWasOn`, `PuckRestartPending`, `FirmwareHold`; the other
  controllers' in [pan-hiding-controllers.md](pan-hiding-controllers.md).

## Tests

`HidingTests` (fake HidHide and devices), `HidHideWireTests`. On the Windows CI runner `tools/ci/Install-HidHide.ps1`
installs HidHide 1.5.230 (hash and signature checked; on about half the runners its updater registration hangs, the
step moves on after 90 s); with its control device answering, `HidHideTests` and the service test hide through the
real driver and check HidHide is back as before; otherwise they skip with the reason.
