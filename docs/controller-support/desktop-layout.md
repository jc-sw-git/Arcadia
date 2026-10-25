# Steam's desktop layout and the mouse mode (#64, 2026-10-20)

- With Steam running, holding Back + Start on the owner's PC opened Windows' Alt+Tab window: Steam's desktop
  layout turns the controller into keys and mouse itself, beside Arcadia's mouse mode.
- `SteamDesktopInputFilter` drops simulated (`SendInput`) mouse and key input while a controller is in use (or was
  less than 150 ms ago); Arcadia's own input carries a marker in `dwExtraInfo` and passes. Seen on the owner's
  PC (2026-10-25, spike #112): Steam's desktop layout sends keys and mouse as injected input; a forced game layout's
  trackpad mouse did not carry the flag ([steam-input-helper.md](steam-input-helper.md)). `arcadia.log` names how many inputs were dropped.
- Beside a library game without the mouse mode it drops input only while ⧉ + ≡ are held (`DesktopFilterScope.ComboHeld`),
  so a game's own Steam layout (e.g. keys for a game without controller support) still reaches it. Seen on the
  owner's PC (2026-10-20): in a Steam game with an Xbox pad (Steam Input presumably off for it) Steam's desktop layout
  still made Alt+Tab of Back + Start; with the Steam Controller (Steam Input on) it didn't. So a hook stays in beside
  games too, but only the keyboard one (Alt+Tab is keys; a gaming mouse would send up to 8,000 events a second through
  Arcadia), and none while no controller is connected. The hook thread runs at the highest priority, because
  every input waits for it. Windows removes a low-level hook that once took too long without telling (Microsoft's docs
  for `LowLevelKeyboardProc`), e.g. during a GC pause, so the thread sets its hooks anew every 30 s and on every change
  of scope, the new one before the old one goes. The option is its own box and works with the mouse mode "Aus" too
  (owner, 2026-10-20): Steam's layout doubles Arcadia's own navigation as well. Not yet seen: whether the filter also drops the Windows on-screen
  keyboard (osk.exe), which sends its keys with `SendInput` too (T64-18).
- The Steam Controller reaches Arcadia only through SISR. While SISR is paused for a game on "Steam Input", Arcadia
  doesn't see it at all: no Back + Start, no View held (named under the Steam Controller Support's "Nachteile", owner 2026-10-20; a fix needs #112 or #72).
- "Arcadia in front" is decided by the foreground window (`ProcessWatch.ForegroundProcessId`), not by the window's
  Activated/Deactivated: on the owner's PC Arcadia sometimes took a game's controller input as its own after
  switching between the game and Arcadia, and View held didn't bring it back (2026-10-20, cause not found).
  A game in borderless full screen is a topmost window and can stay above Arcadia while Arcadia has the foreground
  (found in review, not seen): "in front" also needs that no other program's window covers Arcadia whole, and View
  held keeps Arcadia topmost until it loses the focus when `BringToFront` alone left it covered (`ForegroundGuard`).
  The owner has three monitors and saw no difference between borderless and exclusive full screen, so the likelier
  way is that Arcadia really got the foreground on another monitor: Steam's Alt+Tab, or a click in the mouse mode
  (RT or A) with the pointer the game hides lying over Arcadia. So the mouse mode, switched on with a game in front,
  puts the pointer in the middle of the game's window, and `arcadia.log` names every foreground change (Arcadia,
  game, no game or no window, and whether Avalonia's activation disagrees; never the program, owner 2026-10-23) and where the foreground is after View held.
