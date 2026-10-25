# #72 Support every common controller independently of Steam via SDL3

Controllers at hand: Xbox pad, 8BitDo Ultimate 2, DualSense, Switch Pro (owner); DualShock 4 only automated.
"Fully control" means: D-pad and left stick move the focus, right stick the second focus, A (bottom face button)
starts, held A opens the details, B (right) goes back, X (left) search, Y (top) view, LB/RB tabs, LT/RT pages,
⧉ context menu, ≡ settings, R3 keyboard layout. `arcadia.log`: `%APPDATA%\Arcadia\logs\arcadia.log`.

- [ ] **T72-1** Steam closed. With each of the four controllers alone: it fully controls Arcadia, each press acts
      once; on the Switch Pro the bottom button (labelled B) starts and the right one (A) goes back. · E2E: no (real
      devices; the mapping per type is automated)
- [ ] **T72-2** Steam running, Steam-Eingabe off for Xbox, PlayStation and Switch controllers: same as T72-1, no
      double input, no mouse pointer moving or keys from Steam. · E2E: no (real devices and Steam)
- [ ] **T72-3** Steam running, Steam-Eingabe on for all three types (Steam → Einstellungen → Controller): each
      controller behaves exactly as in T72-2; "Steams Desktop-Belegung ignorieren" on: no mouse or keys from Steam's
      desktop layout. · E2E: no (real devices and Steam)
- [ ] **T72-4** DualShock 4 (mechanical only): Cross, Circle, Square, Triangle, Share, Options map to A, B, X, Y, ⧉, ≡;
      PS and the touchpad click do nothing. · E2E: yes
- [ ] **T72-5** "Steam Controller Kompatibilität" switched on, then off: nothing changes for the four controllers.
      With it on, the Steam Controller works through SISR and each of its presses acts once. · E2E: no (SISR, real devices)
- [ ] **T72-6** DualSense and Switch Pro connected at once: both control Arcadia. Unplug one and plug it in again
      while Arcadia runs: it works again at once. `arcadia.log` has a line "Controller #n verbunden" / "getrennt" for
      each, with name, type and USB id (e.g. `054C:0CE6`), no serial number. · E2E: yes
- [ ] **T72-7** Guide/Xbox, PS, Home, Capture and the touchpad click do nothing in Arcadia (Steam may open its own
      menu with Guide/PS while it runs). · E2E: yes
- [ ] **T72-8** Back + Start held switches the mouse mode and rumbles the controller that pressed it (Xbox, 8BitDo,
      DualSense, Switch Pro), not the others. · E2E: no (rumble needs the device; which pad is unit-tested)
- [ ] **T72-9** `SDL3.dll` next to `Arcadia.exe` renamed: Arcadia starts, a toast says the controllers aren't
      recognised, `arcadia.log` names the error, keyboard (WASD, Space, Esc) and mouse work. Rename it back. · E2E: yes
- [ ] **T72-10** A game in front (one from Steam, one from another launcher), with each of the four controllers:
      Back + Start switches the mouse mode, View held brings Arcadia back. · E2E: no (real games)
- [ ] **T72-11** Budgets (Release build): idle CPU of Arcadia < 1 % over 60 s with a controller connected and
      untouched (Task Manager → Details, or `Get-Counter '\Process(Arcadia)\% Processor Time'`); D-pad press to visible
      focus move < 50 ms. · E2E: no (measurement on the owner's PC)
- [ ] **T72-12** Steam's virtual pad (Steam-Eingabe on), the physical Steam Controller and a single Joy-Con add no
      input of their own; `arcadia.log` says "ignoriert" with the reason. · E2E: yes

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T72-4, T72-6, T72-7, T72-9, T72-12 (and T72-1's mapping) | 2026-10-25 | Claude | Branch `feat/support-every-controller-via-sdl3-72`, Linux, `ControllerTypesTests` (headless, SDL faked) and `SdlGamepadSourceTests` | ✅ green; each acceptance test was red once with its feature broken on purpose (named in the PR) | Owner's manual test |
| SDL3.dll | 2026-10-25 | Claude | same, Linux | ✅ build downloads the pinned zip, checks its SHA-256 (a wrong hash fails the build), copies `SDL3.dll` to App and tests. The real DLL only loads on Windows: `SdlGamepadsTests` run in CI on Windows, skipped on Linux | CI on Windows |
| T72-1 to T72-3, T72-5, T72-8, T72-10, T72-11 | 2026-10-25 | Claude | – | ➖ not runnable in the cloud (no Windows, no controllers) | Owner's PC |
