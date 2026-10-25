# Product decisions: Controls, button hints and dialogs

Agreed with the owner; don't change without asking. Read this before changing this area
(index in `CLAUDE.md` → Product decisions).

- One "Ansicht" modal (Y) for sort + page size. X = search (all launchers, on-screen keyboard,
  `/steam-epic term` prefix, keyword per launcher), R3 = keyboard layout switch, View/⧉ or
  right click = context menu, Menu/☰ = settings (swapped by the owner, 2026-10-17). The context menu stands beside the cover
  after a right click on it, over the preview's "Optionen" button after ⧉, R or a click on that button (owner, 2026-10-21). No "all launchers" tab. No dedicated favourite button.
- UI texts name a controller button by the symbol on it, not by its name: ⧉ and ≡, not View/Back and Menu/Start
  (Xbox symbols for now; later those of the controller in use, #72), framed like in the legend also inside a text
  (`ButtonTextBlock`; in prose other buttons are written in brackets, e.g. "[L3]", "[LS]") (owner, 2026-10-20). Never a
  bare "A" or "B" in a text: `ButtonGlyphTextTests` fails on one in the App's strings (owner, 2026-10-24).
- Every button hint follows the input in use (#127, owner 2026-10-21): after a controller button the controller's
  buttons, after a key or a click the keyboard's keys and the mouse (`KeyHint`, `KeyText`; the mouse merely moving
  changes nothing). Controller buttons are round or a pill, keys square with rounded corners, both with a darker
  lower edge. An element no key reaches shows only its icon. Search and settings are chips in the header (key +
  symbol), so the footer doesn't list them.
- The keyboard plays like a game, one hand on WASD (owner, 2026-10-21): WASD move, Space = A (held: details),
  Esc = B, F = X, V = Y, Q/E = LT/RT, Shift+Q/E = LB/RB, 1 … 0 = launcher tab 1–10, Enter = ≡, R = ⧉. Arrows,
  Page Up/Down, Ctrl+Tab, F2, the menu key and Shift+F10 stay as unadvertised alternatives. In the search a real
  keyboard types, so only keys without text act there (arrows, Enter = ≡ starts the hit, Esc, Page Up/Down).
- Every common controller works the same, with Steam running or not and with Steam-Eingabe on or off for it (#72,
  owner 2026-10-25): Xbox, PlayStation, Nintendo and pads modelled on them, read through SDL3. Buttons count by
  position: the bottom face button is A on every controller (Cross, Nintendo's B), the right one B. Guide/PS/Home,
  Capture and the touchpad click do nothing in Arcadia (Steam opens its own menu with Guide/PS). Only what SDL knows
  as a gamepad counts: two Joy-Cons together are one pad; a single Joy-Con, wheels and flight sticks are ignored. No
  gyro or touchpad yet (#111); the button symbols of the controller in use come with #215.
- Dialogs (Ansicht, settings' prompts) show an ✕ in the top right corner while mouse or keyboard is in use
  (`MainViewModel.ShowsCloseButtons`); the controller closes them with B. A dialog that only informs has
  no "Schließen" button besides (owner, 2026-10-20).
