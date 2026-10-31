# Pan removes the stale device entries of Arcadia's emulated devices (#347)

Every device Arcadia plugs in through VIIPER and usbip-win2 (the copy of the puck, the copy of the controller by cable,
every Xbox pad, probes) stays in Windows as a non-present device entry once it is unplugged. A bus id is never reused
within a run, so every plug makes new instance ids and the entries add up: one session with emulation leaves about 22
(puck copy 19, Xbox pad 3). Pan removes them; Windows makes them anew on the next plug.

## Findings (owner's PC, check script `Run-Check-347.ps1`)

- A1: a non-present entry keeps its last known parent (`DEVPKEY_Device_LastKnownParent`), so every stale entry leads up
  to usbip-win2's host. Real Valve devices lead to their PCI/ACPI host and are told apart; none was unknown.
- A2: `CM_Uninstall_DevNode` also removes a **present** devnode (it removed a present HID collection of the copy). Hence
  Pan removes only while no emulation runs and checks presence right before each entry. Residual risk, accepted: a
  device another program plugs in through usbip-win2 during the ~2 s of a cleanup may lose a collection until it is
  replugged.
- A3: after removing 132 entries (1.8 s, no failure) Steam takes the copy once, keeps its controller settings, and the
  controller works in Steam and Arcadia.
- A4: listing every devnode with its parent through cfgmgr32 takes 30–75 ms of 577 devices. PowerShell's
  `Get-PnpDeviceProperty` is what took minutes (#295), not Windows.
- usbip-win2's host is `ROOT\USB\0000` with the hardware id `ROOT\USBIP_WIN2\UDE` (its `usbip2_ude.inf`); between host and
  device sits the root hub `USB\ROOT_HUB30\…`.

## What is removed

`StaleDeviceList` (Pan.Core) selects, `StaleDeviceCleanup` removes, `WindowsStaleDevices` (Pan) reads and removes through
cfgmgr32:

- Only entries that are not present and whose parent chain (past USB and HID levels, `HidingList.HostOf`, the same
  level limit as `OnSoftwareHost`) ends at a **present** host whose hardware ids contain exactly `ROOT\USBIP_WIN2\UDE`,
  from any vendor. Real devices, other software hosts and other usbip drivers' hosts are never touched. A chain that
  can't be read, or ends at a missing host (usbip-win2 uninstalled), keeps the entry.
- Deepest first. An entry goes only when none of its children is still known: a child that failed would otherwise point
  to a missing parent and never be recognised again.
- Each entry is checked for presence right before its removal. A failure is logged with model and CONFIGRET, never
  the instance id (the copy's ids can carry the real puck's serial), and the entry is tried again next time.

## When

`Guardian.StaleDevices` (partial `Guardian.StaleDevices.cs`) runs a cleanup on a worker, only while nothing emulates,
the journal has nothing to undo (no usbip port, held process, hidden controller or pending restart) and Pan isn't
stopping:

- after a teardown that completed (`Hang`, `ClientGone`, a stop with the controller hidden), after `EmulationStopped`,
  and after a show retry that worked;
- once after Pan starts, after `RecoverAsync`, not awaited: the pipe takes Arcadia at once (its connect gives up after
  2 s);
- at `--uninstall`, after the restore and before the drivers go (`UninstallOrder`), while usbip-win2's host still tells
  the entries apart; none on `RestoreIncomplete` or a data folder that isn't Pan's.

Not while a Valve bootloader is connected (`FlashingAsync`, #295), never on service stop or pre-shutdown. A cleanup
stops only between two entries, because a removal can't be cancelled: on stop (`StopAsync` waits for the entry it is
on), when its budget (`StaleCleanupBudget`, 5 s) is spent, or when an `EmulationStarted` comes. That start is answered
`Rejected(SteamControllerBeingShown)` until the cleanup returned; the client says "Pan räumt noch auf", waits
`PanClient.BeingShownWait` and counts no pad loss. What is left goes next time. Silent: one line in `pan.log`
("Stale device entries (trigger): n found, n removed, n failed, n kept").
