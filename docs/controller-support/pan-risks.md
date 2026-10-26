# Pan: known limits and accepted risks

What Pan (#243, [pan.md](pan.md)) does not guard against, decided with the owner or by the reviews of PR #245. Each
one names the countermeasure that would close it, so a later story can pick it up.

- **A teardown detaches every local VIIPER port**, another program's too (QE review point 11). usbip gives no
  mapping from a port to the process that attached it, so with "Arcadia nutzt ihn mit" (another program's VIIPER)
  Pan can't tell its ports from the other's. Any process of the console session can trigger a teardown (connect,
  `EmulationStarted`, drop the pipe): a nuisance for that program, no privilege gain. Pan logs an emulation without a
  held `viiper.exe`. Accepted as a residual risk (owner, 2026-10-26); it closes with #214: Arcadia then uses only
  its own `viiper.exe`, never another program's running one, ideally on own ports so Pan knows its ports by number.
- **A usbip step left behind after its 10 s timeout** keeps running with a cancelled token; usbip.exe itself is killed
  after 5 s. In theory a late `detach -p N` could hit a port the next emulation got under the same number. Ending
  processes is handle-bound and can't hit anything new.
- **`Pan.exe` runs elevated from the user's folder** (install, update, uninstall, restore): whoever can write to
  `%LOCALAPPDATA%\Arcadia` gets SYSTEM code at the next confirmed UAC prompt. `--expect-hash` only closes the window
  between Arcadia's hash and Pan's copy. The countermeasure is code signing of `Pan.exe` (the UAC dialog then names
  the publisher); a release topic, see `docs/releases.md`.
- **`%ProgramFiles%\Arcadia\Pan`** is trusted as Program Files is: only administrators can write there. A junction
  planted by an administrator is not defended against (administrators are trusted anyway).
- **Process name as a hint only:** `viiper.exe` is taken as the client's child by parent id, start time and name, and
  from then on known by its handle; nothing is ever ended by a name lookup alone.
- **`TerminateProcess`** ends a held forwarding process without a graceful shutdown (as SISR did): VIIPER keeps no
  state worth saving, and the usbip ports are detached before it goes.

## Hiding (part B1)

- **HidHide has no atomic add or remove:** Pan reads a list and writes it back. A change the user makes in HidHide's
  configuration client in the same moment can be lost (HidHide lets one program open it at a time, which narrows
  the window to Pan's read-to-write gap). Pan only ever removes its journaled entries.
- **HidHide's IOCTLs have no timeout:** they run inline, because a step left behind could write a stale list after the
  undo. They don't wait on Windows' PnP lock; listing and restarting the devices do and are bounded.
- **`Unhide` is trusted:** the client says it removed its copy before it unhides (#242's order); Pan can't check that a
  forwarding inside Arcadia stopped. Every unplanned path goes through the teardown, which does check.
- **A puck restart that needs a replug** (`DI_NEEDRESTART`) leaves it pending: the puck is visible to programs that
  open it anew, but one that kept a handle may not see it until a replug; retried at every trigger.
- **Hiding cloaks for every allow-listed program:** with cloaking off before, the user's allow list (e.g. DS4Windows)
  was meaningless; while Pan hides, those programs see the puck too. They are the user's choice.
