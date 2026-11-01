# Pan's puck restart: who may hold the puck (#308)

Pan restarts the puck after every hide and show ([pan-hiding.md](pan-hiding.md)), so programs that have it open lose
it. Windows refuses that restart while a program holds a handle to it and doesn't let go: SetupDi answers
`DI_NEEDRESTART`, Pan logs "Steam Controller not restarted: … needs a replug or a Windows restart" and rejects the hide
with `HidingFailed`. The System log names who held it (Kernel-PnP event 225: process, command line, vetoing devices).

- **The usual holder was Arcadia itself:** SDL's Triton driver opens a puck at once and keeps it open, waiting for a
  controller to connect, without listing a gamepad (`SDL_hidapi_steam_triton.c`), so `arcadia.log` shows no
  "Controller #N verbunden" while it holds it. Every hide then failed ~6 s after it began, each retry ~22 s later
  counted as a failure, and the emulation stopped after the 4th within 5 minutes.
- **Arcadia lets go first.** Around every `Hide` and `Unhide` (`PanEmulation.WithPuckLetGoAsync`) Arcadia switches
  SDL's Steam driver off (`SDL_JOYSTICK_HIDAPI_STEAM=0`, then one `SDL_UpdateGamepads`, which closes the device) and
  on again afterwards (`SdlGamepadSource.ReadSteamControllers`). The copy is attached only after the hide and removed
  before the show, so it holds nothing then. A connected Steam Controller drops out of SDL for the ~2.5 s of the
  restart, which the restart does anyway.
- **The copy is never hidden nor restarted.** VIIPER's copy of the puck arrives as a second `28DE:1304` instance (on
  `ROOT\USB\…`, no serial); Pan lists and restarts only devices whose device tree leads to a hardware host
  (`HidingList.OnSoftwareHost`: past the USB and HID levels to `ROOT\…`/`SWD\…` = software). A tree that can't be read
  counts as hardware: a copy hidden by mistake costs Steam the copy, a puck not hidden would give every input twice.
  Arcadia copies only a puck on a PCI or ACPI host (`PuckDescriptors.IsRealPuck`), so a puck on another host (e.g.
  `VMBUS\`) or with an unreadable tree is hidden but never copied: "Kopie startet nicht", and after 3 failures the
  emulation stops. Safe both ways, just not working there.
- **The warning stays.** An automatic retry after a failed hide keeps the status `Paused`/`Hiding`; one after "Erneut
  versuchen" or "Controller wiederherstellen" shows `SettingUp` (`docs/product/settings.md`).
- **Pan's own restarts** (the teardown's show, its retry, the hide of a new puck instance arriving while hiding; none
  while a bootloader is connected, [steam-controller-firmware.md](steam-controller-firmware.md)): a connected client first gets the notice `ReleasePuck(n)`; Arcadia lets go
  (`PanClient.PuckRestarting`; `PanEmulation` counts who wants the puck let go, so an overlap with its own hide reads
  again only after both) and answers `PuckReleased(n)`. Pan waits at most 2 s (`PuckReleaseTimeout`; a hung client
  can't answer), restarts, and sends `PuckRestarted(n)`, done or not. A late answer for an older number changes nothing.
- Still open: Pan doesn't read the veto itself (event 225 is the quickest way to find a holder). `ReleasePuck` lets
  go of SDL only: an arrival's re-hide while the copy runs (a second puck instance, e.g. another USB port) is still
  vetoed by the copy's own handles on the real puck, as is a restart while another program holds the puck. Such a
  restart stays pending and the teardown retries it.
