# #367 Letting go of the controllers leaves a held button pressed

- [ ] **T367-1** Mouse mode on (Back + Start), a non-Steam controller (e.g. DualSense) not taken over yet. Hold the
      left shoulder button (left mouse button) on the desktop, e.g. dragging a window, and start a game in Arcadia's
      emulation so Pan hides the controller while it is held: the drag ends at once, the mouse button doesn't stay
      down while the controllers are away (1–3 s). `arcadia.log` has "alle Controller losgelassen". · E2E: no (needs
      Pan hiding a real controller; the release itself is unit-tested in `SdlGamepadSourceTests`)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T367-1 (mechanical) | 2026-11-01 | Claude | PR branch, Linux cloud container, `FakeSdlGamepads` unit tests | ✅ both new tests red without the fix, green with it | real run open: owner's PC |
