# #335 Pan hides every controller Arcadia takes over

Pan's hiding grows from the puck to every controller the client takes over, one model at a time
(`HideController`/`ShowController`), with the puck's guarantees. Findings and rules:
[pan-hiding-controllers.md](../../controller-support/pan-hiding-controllers.md). Arcadia itself sends these commands
only with #238: until then T335-1 to T335-3 can't be run through Arcadia and run with #238's build.

Check script A1–A5 (owner's PC, Windows 11, HidHide 1.5.230): results in #238. A1 passed for a DualSense by USB and an
8BitDo Ultimate 2 (XInput) by dongle and cable once its XInput interface is hidden too; A3 failed for XInput
interfaces (restart only on reconnect), which Pan reports as `ControllerNeedsReplug`. Not tested: DualSense by
Bluetooth, GameInput.

- [ ] **T335-1** DualSense (USB) and 8BitDo (dongle) connected, Arcadia starts and takes both over: a probe outside the
      allow list sees neither through XInput, Windows.Gaming.Input, raw HID and SDL3, Steam's controller settings list
      neither; `pan.log` says "Controller 054C:0CE6 hidden" and "Controller 2DC8:310B hidden … only fully taken over
      once reconnected". After a reconnect of the 8BitDo Steam doesn't list it either. Then plug the DualSense into
      another USB port (a new instance, hidden at its arrival without a restart): Steam doesn't list it; if it does,
      note it (Steam opened it first). · E2E: no (real HidHide and pads)
- [ ] **T335-2** As T335-1, then end `Arcadia.exe` in Task Manager: both pads are visible to the probe and Steam again
      within 10 s; HidHide's configuration client lists none of Pan's entries, the user's own entries are still there.
      · E2E: no (Pan, HidHide)
- [ ] **T335-3** As T335-1, then a hard reset: after login both pads are visible to games; `pan.log` shows the
      recovery showing them. · E2E: no (reboot)
- [ ] **T335-4** Two models and the puck hidden, one model shown again: the other model and the puck stay hidden,
      Arcadia stays on the allow list, cloaking stays on; showing the last one takes both back; an arrival of the shown
      model is left alone. · E2E: yes (`ControllerHidingTests`, `PanClientTests`, fake HidHide)
- [ ] **T335-5** The DualSense firmware updater (or Xbox Accessories) runs: hiding a pad of that vendor is refused with
      `FirmwareToolRuns`, which reaches Arcadia, while another vendor's pad is hidden; closed, the pad is hidden. With
      the real tool open, `Get-Process` names the process; it must be one of `ControllerList.FirmwareTools`.
      · E2E: yes (`ControllerHidingTests`, `PanClientTests`, fake process list); the process name by hand

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| A1–A5 | 2026-10-30 | owner | check script v3 and probe, Windows 11, HidHide 1.5.230 | ✅ A1, A4, A5 (DS4, DualSense) · ❌ A2, A3 for XInput pads, adaptive triggers, Switch 2 Pro unseen | Results in #238; XInput restart reported as replug (this PR), slot and kinds go to #238 |
| T335-4, T335-5 | 2026-10-30 | Claude | branch `feat/pan-hides-every-controller-335`, Linux container, fakes | ✅ | – |
| real driver | 2026-10-30 | Claude | Windows CI runner, HidHide 1.5.230, made-up instance ids | see the PR's CI run | – |
