# Pan: known limits and accepted risks

What Pan (#243, [pan.md](pan.md)) does not guard against, decided with the owner or by the reviews of PR #245. Each
one names the countermeasure that would close it, so a later story can pick it up.

- **A server port reused before the teardown** (#304 closed "a teardown detaches every local VIIPER port"): Pan
  detaches only devices imported from a journaled `127.0.0.1:<port>`. If Arcadia's VIIPER died and another program's
  usbip server got the same port from Windows before the teardown ran, its devices there would be detached too. Ports
  come from Windows' dynamic range and the teardown follows within seconds; a nuisance at most, no privilege gain.
- **Arcadia's VIIPER: check to use** (#304): between a listener check and a connect another process could take a port
  VIIPER just let go. Without the key it can neither read nor forge a frame (session key from key and nonces), so it
  can only make the plug fail. Same-user code could read the key from `viiper.exe`'s memory (or its file in the few
  ms before it is deleted); it could control the user's input anyway.
- **The usbip port takes any local importer** (#304, QE review of PR #318): usbip has no auth, and the fork's
  `handleImport` imports a device for any connection, a second time too. The port is in the TCP table, so any local
  process outside an AppContainer, also of another Windows user, can import the pad or the copy of the puck: read
  the controller's input and send control requests through the copy. The allow list still drops update traffic, but
  each dropped message counts for `FirmwareAttemptWatch`, so it can force the firmware hold (puck stays hidden). The
  LAN case closed with 127.0.0.1. Countermeasure (fork, follow-up): accept an import only from usbip-win2's kernel
  connection (on Windows the TCP table names PID 4), or refuse a second import of a busid.
- **VIIPER reuses nonces** (upstream, #304): both directions use the same session key with counters from 0, so two
  frames share a nonce. Anyone who could read loopback traffic (administrators only) could XOR them. Fix upstream:
  one key per direction.
- **A usbip step left behind after its 10 s timeout** keeps running with a cancelled token; usbip.exe itself is killed
  after 5 s. In theory a late `detach -p N` could hit a port the next emulation got under the same number. Ending
  processes is handle-bound and can't hit anything new.
- **`Pan.exe` runs elevated from the user's folder** (install, update, uninstall, restore): whoever can write to
  `%LOCALAPPDATA%\Arcadia` gets SYSTEM code at the next confirmed UAC prompt. `--expect-hash` only closes the window
  between Arcadia's hash and Pan's copy; the hand-off copy of `--uninstall` (#270) is likewise only checked against the
  file at the path then, not the image UAC started. The countermeasure is code signing of `Pan.exe` (the UAC dialog then names
  the publisher); a release topic (#271), see `docs/releases.md`.
- **`%ProgramFiles%\Arcadia\Pan`** is trusted as Program Files is: only administrators can write there. A junction
  planted by an administrator is not defended against (administrators are trusted anyway).
- **Process name as a hint only:** `viiper.exe` is taken as the client's child by parent id, start time and name, and
  from then on known by its handle; nothing is ever ended by a name lookup alone.
- **`TerminateProcess`** ends a held forwarding process without a graceful shutdown (as SISR did): VIIPER keeps no
  state worth saving, and the usbip ports are detached before it goes.

## Hiding (part B1)

- **HidHide has no atomic add or remove:** Pan reads a list and writes it back right after (the device listing comes
  before the read). A change the user makes in HidHide's configuration client in that gap can be lost (HidHide lets one
  program open it at a time, which narrows it further). Pan only ever removes its journaled entries.
- **A console-session program gains a little:** `Hide` lets the first client of the console session, without
  administrator rights, have Pan allow-list its own image and turn cloaking on; it then sees every device the user
  hid with HidHide (whose configuration is admin-only). Part A's "no privilege gain" no longer holds for this. The
  countermeasure: accept `Hide` only from Arcadia's installed and signed exe (with code signing, `docs/releases.md`).
- **HidHide's IOCTLs have no timeout:** they run inline, because a step left behind could write a stale list after the
  undo. They don't wait on Windows' PnP lock; listing and restarting the devices do and are bounded. If one did hang,
  it would hold the HidHide gate: every later show times out and the puck stays hidden until the service restarts
  (Pan doesn't die, so Windows' recovery doesn't help, nor does `Pan.exe --restore` while the service runs).
- **`Unhide` is trusted:** the client says it removed its copy before it unhides (#242's order); Pan can't check that a
  forwarding inside Arcadia stopped. Every unplanned path goes through the teardown, which does check.
- **A puck restart that needs a replug** (`DI_NEEDRESTART`) leaves it pending: the puck is visible to programs that
  open it anew, but one that kept a handle may not see it until a replug; retried at every trigger.
- **Hiding cloaks for every allow-listed program:** with cloaking off before, the user's allow list (e.g. DS4Windows)
  was meaningless; while Pan hides, those programs see the puck too. They are the user's choice.
- **HidHide's silent setup can hang:** on about half the CI runners (Windows Server 2025) its last MSI action,
  registering `nefarius_HidHide_Updater.exe`, never returned after the driver was in place; killing it risks a rollback.
  Pan waits 2 min, then counts a driver in place as installed and leaves the setup running (B2). Not seen on a real PC
  yet; if it happens there, Windows Installer refuses other installs (1618) while it hangs and probably rolls HidHide
  back at the next restart. The CI starts a no-op in the updater's place instead (`tools/ci/Install-HidHide.ps1`). Cause and a clean fix: #274.
- **HidHide's Configuration Client competes with Pan:** while it is open it holds HidHide (`HidHideBusy`: a hide is
  refused, a show retried until it closes; B2 should tell the user to close it). What the user changes there during
  an emulation applies at once: unticking the puck or turning hiding off shows Steam both controllers (double input),
  removing Arcadia from the list cuts the emulation off from the real controller, inverse mode refuses the next hide.
- **An update over the real puck's COM port runs past Arcadia** (review of PR #294, #242). HidHide hides HID only;
  the puck's interface 0 is a CDC ACM COM port that stays Steam's while Pan hides. If Steam sends the puck to its
  bootloader over it, Arcadia sees no traffic, no firmware dialog and no hold: the puck vanishes mid-copy, is flashed
  as the bootloader `28DE:1007`, which Arcadia, Pan and HidHide never touch, and comes back
  ([emulated-steam-controller.md](emulated-steam-controller.md)). Accepted (owner, 2026-10-28). Rejected
  countermeasure: Pan disabling `USB\VID_28DE&PID_1304&MI_00` while it hides; it protects nothing (the flash runs on
  `1007`) and adds an elevated, persistent device change that could leave the puck without its port or keep Steam
  from ever updating it. Steam's real path is recorded in spike #295.
