# Pan: known limits and accepted risks

What Pan ([pan.md](pan.md)) does not guard against, decided with the owner. Each one names the countermeasure that
would close it, so a later story can pick it up. Don't "fix" one of these in passing; it was weighed.

- **`Pan.exe` runs elevated from the user's folder** (install, update, uninstall, restore): whoever can write to
  `%LOCALAPPDATA%\Arcadia` gets SYSTEM code at the next confirmed UAC prompt. `--expect-hash` only closes the window
  between Arcadia's hash and Pan's copy; the hand-off copy of `--uninstall` is likewise only checked against the file
  at the path then. Countermeasure: code signing of `Pan.exe` (#271, `docs/releases.md`).
- **`%ProgramFiles%\Arcadia\Pan`** is trusted as Program Files is: only administrators can write there; a junction
  planted by an administrator is not defended against.
- **A usbip step left behind after its 10 s timeout** keeps running with a cancelled token; `usbip.exe` itself is
  killed after 5 s. In theory a late `detach -p N` could hit a port the next emulation got under the same number.
  Ending processes is handle-bound and can't hit anything new.
- **A server port reused before the teardown (#304):** Pan detaches only devices imported from a journaled
  `127.0.0.1:<port>`. Had another program's usbip server got that port from Windows after Arcadia's VIIPER died and
  before the teardown, its devices there would go too: seconds, a nuisance at most.
- **Arcadia's VIIPER, check to use (#304):** between a listener check and a connect another process could take a port
  VIIPER just let go; without the key it can't read or forge a frame, only make the plug fail. Same-user code could
  read the key from `viiper.exe`'s memory, but it could read the user's input anyway.
- **The usbip port takes any local importer:** usbip has no auth and VIIPER imports a device for any connection, a
  second time too. A local process, also another Windows user's, could import
  the pad or the puck's copy: read the input, and through dropped firmware messages raise Arcadia's firmware notice. No flash,
  no privilege gain. Residual risk; a fix would be in VIIPER (import only from usbip-win2's kernel
  connection, or no second import).
- **VIIPER reuses nonces:** both directions use one session key with counters from 0. Only someone reading loopback
  traffic (administrators) could use it. Fix upstream: one key per direction.

## Hiding

- **HidHide has no atomic add or remove:** Pan reads a list and writes it back right after. A change the user makes
  in HidHide's configuration client in that gap can be lost (HidHide lets one program open it at a time, which narrows
  it). Pan only ever removes its journaled entries.
- **A console-session program gains a little:** `Hide` lets the first client of the console session, without
  administrator rights, have Pan allow-list its own image and turn cloaking on; it then sees every device the user hid
  with HidHide (whose configuration is admin-only). Countermeasure: accept `Hide` only from Arcadia's installed and
  signed exe (code signing).
- **HidHide's IOCTLs have no timeout:** they run inline, because a step left behind could write a stale list after the
  undo. If one did hang, it would hold the HidHide gate: every later show times out and the puck stays hidden until
  the service restarts (Pan doesn't die, so Windows' recovery doesn't help, nor does `--restore` while the service
  runs).
- **`Unhide` is trusted:** the client says it removed its copy before it unhides; Pan can't check that a forwarding
  inside Arcadia stopped. Every unplanned path goes through the teardown, which does check.
- **A puck restart that needs a replug** (`DI_NEEDRESTART`) leaves it pending: the puck is visible to programs that
  open it anew, but one that kept a handle may not see it until a replug; retried at every trigger.
- **Hiding cloaks for every allow-listed program:** with cloaking off before, the user's allow list (e.g. DS4Windows)
  was meaningless; while Pan hides, those programs see the puck too. They are the user's choice.
- **HidHide's silent setup can hang** at its last MSI action (registering its updater) after the driver is in place;
  killing it risks a rollback, so Pan waits 2 min and counts a driver in place as installed. Seen on about half the
  CI runners (Windows Server 2025), not on a real PC yet; if it happens there, Windows Installer refuses other
  installs (1618) while it hangs and probably rolls HidHide back at the next restart. Cause and a clean fix: #274.
- **HidHide's Configuration Client competes with Pan:** while it is open it holds HidHide (`HidHideBusy`: a hide is
  refused, a show retried until it closes). What the user changes there during an emulation applies at once:
  unticking the puck or turning hiding off shows Steam both controllers (double input), removing Arcadia from the list
  cuts the emulation off from the real controller, inverse mode refuses the next hide.
- **An update over the real puck's COM port runs past Arcadia.** HidHide hides HID only; the puck's interface 0 is a
  CDC ACM COM port that stays Steam's while Pan hides. If Steam sends the puck to its bootloader over it, Arcadia sees
  no traffic, no firmware dialog and no hold: the puck vanishes mid-copy, is flashed as the bootloader `28DE:1007`,
  which Arcadia, Pan and HidHide never touch, and comes back. Rejected countermeasure: Pan disabling
  `USB\VID_28DE&PID_1304&MI_00` while it hides; it protects nothing (the flash runs on `1007`) and adds an elevated,
  persistent device change that could leave the puck without its port or keep Steam from ever updating it. Steam's
  real path: spike #295.
