# Steam's desktop layout and the mouse mode (#64)

- With Steam running, Steam's desktop layout turns the controller into keys and mouse itself, beside Arcadia's mouse
  mode (holding Back + Start opened Windows' Alt+Tab window). It sends them as injected input (`LLKHF_INJECTED` /
  `LLMHF_INJECTED`, about 220 mouse events a second); a forced game layout's trackpad mouse did not carry the flag.
- `SteamDesktopInputFilter` drops simulated (`SendInput`) mouse and key input while a controller is in use (or was
  less than 150 ms ago); Arcadia's own input carries a marker in `dwExtraInfo` and passes. `arcadia.log` names how
  many inputs were dropped. The option is its own box and works with the mouse mode "Aus" too: Steam's layout doubles
  Arcadia's own navigation as well.
- Beside a library game without the mouse mode it drops input only while ⧉ + ≡ are held
  (`DesktopFilterScope.ComboHeld`), so a game's own Steam layout (e.g. keys for a game without controller support)
  still reaches it. In a Steam game with an Xbox pad Steam's desktop layout still made Alt+Tab of Back + Start; so a
  hook stays in beside games too, but only the keyboard one (Alt+Tab is keys; a gaming mouse would send up to 8,000
  events a second through Arcadia), and none while no controller is connected.
- The hook thread runs at the highest priority, because every input waits for it. Windows removes a low-level hook
  that once took too long without telling (Microsoft's docs for `LowLevelKeyboardProc`), e.g. during a GC pause, so
  the thread sets its hooks anew every 30 s and on every change of scope, the new one before the old one goes.
- Not yet seen: whether the filter also drops the Windows on-screen keyboard (osk.exe), which sends its keys with
  `SendInput` too (T64-18).
- The Steam Controller is read raw, also while a game on "Steam-Eingabe" runs; Steam's trackpad pointer glides on for
  up to a second after release, so a trackpad touch counts as use for 1 s. Back + Start also switches the action set
  of Steam's desktop configuration (Desktop ↔ Gamepad) inside Steam, with nothing to filter (#93). The active set is
  not observable: `controller.txt` writes nothing on a switch; over CEF, `SteamClient.Input` has no getter, none of
  its `RegisterFor…` callbacks fires, and `GetConfigForAppAndController(413080, n)` returns the same
  `desktop_neptune.vdf` in both sets.
- "Arcadia in front" is decided by the foreground window (`ProcessWatch.ForegroundProcessId`), not by the window's
  Activated/Deactivated, which got out of step beside games (Arcadia took a game's controller input as its own, and
  View held didn't bring it back). "In front" also needs that no other program's window covers Arcadia whole (a game
  in borderless full screen is a topmost window), and View held keeps Arcadia topmost until it loses the focus when
  `BringToFront` alone left it covered (`ForegroundGuard`). The owner has three monitors, so Arcadia can really get the
  foreground on another monitor (Steam's Alt+Tab, or a click in the mouse mode with the pointer the game hides lying
  over Arcadia); the mouse mode, switched on with a game in front, therefore puts the pointer in the middle of the
  game's window. `arcadia.log` names every foreground change (Arcadia, game, no game or no window, and whether
  Avalonia's activation disagrees; never the program) and where the foreground is after View held.
