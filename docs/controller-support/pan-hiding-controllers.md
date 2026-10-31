# Pan hides every other controller (#335)

How Pan hides the controllers Arcadia takes over besides the Steam Controller, with the same guarantees as for the
puck ([pan-hiding.md](pan-hiding.md): driver interface, checks, write-ahead journal, the user's entries untouched,
every teardown trigger). What the client emulates for them: [every-controller.md](every-controller.md) (#238).

## Protocol

- `HideController(model)` → `ControllerHidden(reason)` or `Rejected(reason)`; `ShowController(model)` →
  `ControllerShown(reason)` or `Rejected`. The value is the model's USB ids (`vendor << 16 | product`, `ControllerModel`),
  never an instance: Pan finds the devices itself. One model covers every connected controller of it (#238 D3b).
- `ControllerHidden`/`ControllerShown` carry `None` or `ControllerNeedsReplug`: done, but programs that opened the
  controller before keep it (hide), or don't notice it is back (show), until it is reconnected.
- Rejected with `ControllerNotConnected`, `ControllerNotHideable` (no gamepad collection or XInput interface, a software
  host, a Valve device), `FirmwareToolRuns`, or the puck's reasons (HidHide missing, busy, …).
- Only within the current generation's emulation, as `Hide`. The client lets go of the model before it sends either
  (Pan restarts it). A rejected `HideController` with `HidingFailed` may have hidden it already: the client sends
  `ShowController` (`PanClient.HideControllerAsync` does).
- The client's reply timeout is 45 s: a request may wait behind an arrival's scan, then scan, restart and reconnect
  (each step up to 10 s in Pan).
- A hide that was still listing devices when a teardown or stop began hides nothing: Pan checks the emulation again
  right before it journals (the puck's hide too).
- `Unhide` (the puck) and `ShowController` leave everything else hidden; the client's allow-list entry and cloaking go
  back only with the last hidden device.

## What is hidden

- `ControllerDevices.Scan`: the model's present devices on a hardware host (`HidingList.OnSoftwareHost`, never VIIPER's
  pads): every HID collection but those of class `Keyboard`/`Mouse` (HidHide doesn't filter them, games don't read
  them), and every USB device of class `XnaComposite` or `XboxComposite` (the XInput interface, driver `xusb22`).
  Only if one of them is a gamepad: a HID collection with `HID_DEVICE_UP:0001_U:0004`/`0005` among its hardware ids (its compatible ids are empty), or an XInput interface.
- Model ids are read from USB (`VID_054C&PID_0CE6`), Bluetooth (`VID&0002054C_PID&0CE6`) and Bluetooth LE
  (`VID&02045E_PID&0B13`) ids.
- The XInput interface must be hidden too: XInput, Windows.Gaming.Input and SDL read an XInput pad through it, not its
  HID collections (owner's PC, 8BitDo Ultimate 2).
- Never Valve's devices (`ControllerList.MayTakeOver`): the puck has its own path, the rest stays visible.

## Restart

- After a hide or show, Pan restarts the device each hidden HID collection belongs to (its USB interface `…&MI_xx`, or
  its Bluetooth device), not the composite device: Windows refuses that (`DI_NEEDRESTART`) while any interface is held.
- An XInput interface is never restarted in place: Windows refuses it (`DI_NEEDRESTART`) while anyone holds it, and
  Steam opens a freshly plugged pad within a second. A newly hidden one is reconnected instead: Pan cycles the hub port
  of its USB device (`IOCTL_USB_HUB_CYCLE_PORT` on the hub's `GUID_DEVINTERFACE_USB_HUB`, port = `CM_DRP_ADDRESS`,
  device back after about 4 s; verified with the 8BitDo's dongle) and answers once the device is gone. Only a USB device
  of the controller's own model right below a hub is cycled (`ControllerList.OnHubPort`), never a Bluetooth radio's
  port. Not journaled; where it fails the hide answers `ControllerNeedsReplug`. A HID restart that is refused gets the
  same port cycle. A restart that needs a reconnect isn't tried again. A show doesn't cycle.
- Pending restarts are journaled (`ControllerRestarts`) and retried by the teardown and its retry.

## Arrival

- `CM_Register_Notification` for HID and XInput interfaces; a link naming a model the client took over in this
  emulation hides the model's new instances at once. No restart: Arcadia, allow-listed, opens the controller on the
  same notification and Windows would refuse it.
- What the client took over is forgotten at every teardown and stop; after a show of the model its arrivals are left alone.

## Firmware tools (#238 D8)

- While Xbox Accessories (Microsoft's pads) or Sony's DualSense firmware updater (Sony's pads) runs, Pan hides nothing
  of that vendor, also at an arrival, and answers `FirmwareToolRuns`. A tool started while a pad is hidden doesn't
  show it; the client shows it by `ShowController` (excluded) or Pan off.
- Process names in `ControllerList.FirmwareTools` (`XboxDevices`, `XboxAccessories`; `FWupdater`,
  `FWupdaterDualSense`), not yet confirmed on a real install (T335-5).

## Showing again

- Every teardown shows the controllers with the puck (steps "show the hidden controllers", "restart the shown
  controllers"). A firmware hold keeps only the puck hidden ([pan-firmware-hold.md](pan-firmware-hold.md)); the
  controllers are shown anyway, the retry waits for Steam only for the puck.
- Journal fields: `HiddenControllers` (HID and XInput instances Pan added), `ControllerRestarts` (HID instances still to
  restart). A Pan before #335 reads the journal ignoring them: after such a downgrade it doesn't show the controllers;
  the last resort in `docs/releases.md` covers them.

## Tests

`ControllerHidingTests` (fake HidHide and controllers: several models, partial show, arrival, firmware tool, teardown,
recovery), `ControllerModelTests`, `PanClientTests` (T335-4, T335-5). On the Windows runner `HidHideTests` hide and
show a HID collection and an XInput interface through the real driver.
