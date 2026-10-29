# Pan's puck restart: who may hold the puck, and the copy (#308)

Pan restarts the puck after every hide and show ([pan-hiding.md](pan-hiding.md)), so programs that have it open lose it.
Windows refuses that restart while a program holds a handle to it and doesn't let go: SetupDi answers
`DI_NEEDRESTART`, Pan logs "Steam Controller not restarted: … needs a replug or a Windows restart" and rejects the hide
with `HidingFailed`. The System log names who held it (Kernel-PnP event 225: process, command line, vetoing devices).

## What happened on the owner's PC (2026-10-29)

Only the puck plugged in, Steam Controller off, Steam closed, Pan on:
- Every hide failed ~6 s after it began; each time event 225 named Arcadia (`dotnet.exe … Arcadia.dll --demo`) and the
  four vendor collections `HID\VID_28DE&PID_1304&MI_02…05&Col03\7&…`. Pan's later restarts for the show worked.
- Arcadia retried every ~22 s (10 s wait plus ~11 s of hide, undo and show), counted each as a failure and stopped the
  emulation after the 4th within 5 minutes ("Emulation angehalten"). The settings row flickered with every retry.
- The holder is SDL: its Triton driver opens a puck (`Proteus` dongle) at once and keeps it open, waiting for a
  controller to connect, without listing a gamepad (`SDL_hidapi_steam_triton.c`, `InitDevice` for the dongle). So
  `arcadia.log` shows no "Controller #N verbunden" while it holds it.
- With the controller switched on the hide worked once. 5 s later VIIPER's copy of the puck arrived
  (`USB\VID_28DE&PID_1304\2&387d3fea&0&1` on `USB\ROOT_HUB30\1&…` on `ROOT\USB\0000`, no serial, its HID instances
  `4&…`). Pan took it for a new puck instance: it hid the copy from Steam and restarted the real puck (vetoed again,
  now also by the copy's own handles), and the copy's reader lost the puck.

## Since #308

- **Arcadia lets go first.** Around every `Hide` and `Unhide` (`PanEmulation.WithPuckLetGoAsync`) Arcadia switches
  SDL's Steam driver off (`SDL_JOYSTICK_HIDAPI_STEAM=0`, then one `SDL_UpdateGamepads`, which closes the device) and
  on again afterwards (`SdlGamepadSource.ReadSteamControllers`). The copy is attached only after the hide and removed
  before the show, so it holds nothing then. A connected Steam Controller drops out of SDL for the ~2.5 s of the
  restart, which the restart does anyway.
- **The copy is never hidden nor restarted.** Pan lists and restarts only `28DE:1304` devices whose device tree leads
  to a hardware host (`HidingList.OnSoftwareHost`: past the USB and HID levels to `ROOT\…`/`SWD\…` = software). A tree
  that can't be read counts as hardware: a copy hidden by mistake costs Steam the copy, a puck not hidden would give
  every input twice.
- **The warning stays.** An automatic retry after a failed hide keeps the status `Paused`/`Hiding` (no `SettingUp` in
  between); one after "Erneut versuchen" or "Controller wiederherstellen" shows `SettingUp`, so the press shows.
- **Two defaults on purpose.** Pan hides a puck whose host is unknown; Arcadia copies only a puck on a PCI or ACPI
  host (`PuckDescriptors.IsRealPuck`). A puck on another host (e.g. `VMBUS\`) or with an unreadable tree is therefore
  hidden but never copied: "Kopie startet nicht", and after 3 failures the emulation stops. Safe both ways (no
  double input, no copy reading itself), just not working there.

## Still open (#313)

Pan's own restarts, which Arcadia isn't asked about, can still be vetoed by Arcadia's SDL (QE review of PR #310):
- the teardown after `EmulationStopped` when the client's `Unhide` was rejected (HidHide busy), or after `EndAsync`'s
  "nicht alles entfernt" branch (`ShowAfterTeardownAsync`, `RetryShowAsync`);
- the show when a firmware hold ends while Arcadia keeps running;
- a device arrival while hiding (only for new puck instance ids), and a teardown after Arcadia hung (it can't let go).

The restart then stays pending (`PuckRestartPending`) and the teardown retries it; a running Steam may only see the
controller again after a replug (not checked on hardware). Event 225 is the quickest way to find a holder; Pan doesn't
read the veto itself yet.
