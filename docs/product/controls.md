# Product decisions: Controls, button hints and dialogs

Controllers in games (Pan, the Steam Controller, per-game mode): [controllers-in-games.md](controllers-in-games.md).

## Buttons and keys

- One "Ansicht" modal (L3) for sort + page size; Y = the header's status messages in a modal (`settings.md`). X = search (all launchers, on-screen keyboard, `/steam-epic term`
  prefix, keyword per launcher), R3 = the library's filter (#44), on the detail page too (#530), ⧉ there a renamed game's original title (#496), keyboard layout switch where a keyboard is open, View/⧉ or right click = context menu, Menu/☰ = settings.
  The context menu stands beside the cover after a right click on it, over the preview's "Optionen" button after ⧉, R
  or a click on that button. No "all launchers" tab. No dedicated favourite button.
- The keyboard plays like a game, one hand on WASD: WASD move, Space = A (held: details), Esc = B, F = X, V = Y, C = L3, G = R3,
  Q/E = LT/RT, Shift+Q/E = LB/RB, Shift+W/S = RS, 1 … 0 = launcher tab 1–10, Enter = ≡, R = ⧉. Arrows, Page Up/Down, Ctrl+Tab, F2,
  the menu key and Shift+F10 stay as unadvertised alternatives. In the search a real keyboard types, so only keys
  without text act there (arrows, Enter = ≡ starts the hit, Esc, Page Up/Down).
- Every list goes round at its ends (#476): wherever the focus steps through items, a step past the last goes to the
  first and back, left/right in a row, up/down in a column; greyed-out items are passed over. That holds for the
  library and the search (`library.md`, `search.md`), the tabs, the settings' rows and the first start's, menus
  (context menu, keyboard layouts), "Ansicht", the filter, the status messages, the detail page's buttons, areas and rows (its greyed rows keep the
  focus, `detail-page/`), every
  dialog's buttons and the on-screen keyboard (`on-screen-keyboard.md`). Where a step past an edge leads to another
  area, it still does (the filter: left out of the values back to the categories). A lifted launcher being moved,
  the mouse wheel and the scroll bars stop at the ends.
- RS (Shift+W/S, the wheel over it) scrolls an info box that doesn't fit, on the settings page and the detail page
  (#506), the text of "Infos" while it has the focus in the detail page's rail (#530), and a metadata hit's facts on
  "Warten auf deine Auswahl" (#560); the focus stays, a box that fits ignores it, and a box whose row, value or greying changes starts at its top
  again. The footer names RS only while the box overflows.
- Held buttons repeat (#476): the D-pad and both sticks after 400 ms, then every 90 ms; LT/RT and LB/RB, wherever
  they turn (library pages, search, detail page, launcher pages in the settings, tabs), after 400 ms, then every
  150 ms; keys repeat as Windows repeats them. On the on-screen keyboard LB/RB are shift and AltGr and don't repeat.
  A hold stops at the end once: where a step past the end goes round to the other end, a held button or key stops
  at the end, and only a fresh press goes round; a switch flips once per press. Two exceptions: the detail page's
  carousel shows no first or last game, so held LT/RT turn it round without a stop; in the library a held stick or
  D-pad also stops at the page's edge, so it never turns the page unnoticed (`library.md`). A hold acts only where it was
  pressed: once the page, a dialog or the page's layout changes (B closes a page, the keyboard slides in or out, a
  dialog opens), it does nothing more until let go. Pan hiding or showing a controller releases what is held (#367).
- A stick direction or a trigger presses past half way and lets go only below 0.3 (stick) or a quarter (trigger), so
  one held near half way that wobbles gives no fresh press and doesn't go round at a list's end (#476).
- Every common controller works the same, with Steam running or not and with Steam-Eingabe on or off for it (#72):
  Xbox, PlayStation, Nintendo and pads modelled on them, read through SDL3. Buttons count by position: the bottom face
  button is A on every controller (Cross, Nintendo's B), the right one B. Guide/PS/Home, Capture and the touchpad click
  do nothing in Arcadia (Steam opens its own menu with Guide/PS). Only what SDL knows as a gamepad counts: two Joy-Cons
  together are one pad; a single Joy-Con, wheels and flight sticks are ignored. No gyro or touchpad yet (#111).
- The Steam Controller too, read directly (#212): its Steam button and "…" belong to Steam, the back grips do nothing
  (assignable later), each press counts once.

## Button hints and texts

- UI texts name a controller button by the symbol on it, not by its name: ⧉ and ≡, not View/Back and Menu/Start
  (written with Xbox's names; drawn with the symbols of the controller in use), framed like in the legend also inside
  a text (`ButtonTextBlock`; in prose other buttons are written in brackets, e.g. "[L3]", "[LS]"). Never a bare "A" or
  "B" in a text: `ButtonGlyphTextTests` fails on one in the App's strings and `AppTexts.resx` (tooltips, plain text for
  the mouse, and the mouse mode's button lists aside). While the keyboard is in use a button in prose shows the key that
  does the same (`KeyNames`: [A] Leertaste, [B] Esc, [Y] V, …; #539); a text about one controller only, a text about
  something only a controller does ([R3] on the on-screen keyboard; in the printing of the controller last used, Xbox
  without one) and the sticks and Guide keep the button.
- Every button hint follows the input in use (#127): after a controller button the controller's buttons, after a key
  or a click the keyboard's keys and the mouse (`KeyHint`, `KeyText`; the mouse merely moving changes nothing).
  Controller buttons are round or a pill, keys square with rounded corners, both with a darker lower edge. An element
  no key reaches shows only its icon. Search and settings are chips in the header (key + symbol), so the footer doesn't
  list them.
- Button symbols of the controller in use (#215): the controller with the last press or stick push past the dead zone
  decides (stick noise doesn't); before any input the first connected one, with none the keyboard's keys; a controller
  plugged in changes nothing until used; unplugging the one in use shows the next one's, then the keyboard's. Nothing
  is stored. By position (A = bottom): Xbox (also generic, unknown and GameCube pads) A B X Y LB RB LT RT L3 R3 ⧉ ≡;
  PlayStation ✕ ○ □ △ L1 R1 L2 R2 L3 R3 with PS5 Create/Options icons, PS4 "Share"/"Options", PS3 "Select"/"Start";
  Nintendo (Switch Pro, Joy-Con pair) B A Y X (bottom button prints B) L R ZL ZR, LS RS for the stick clicks, − +;
  Steam Controller A B X Y L1 R1 L2 R2 L3 R3 ⧉ ≡. Monochrome, face buttons round, the rest pills, PlayStation's
  shapes and ⧉ ≡ (no embedded font has them; ≡ is the same icon as PS5's Options) as vector icons from the theme,
  as high as a capital letter. Only buttons Arcadia uses have a symbol; back grips and trackpads come
  with their first feature. Guide ("[Guide]", #315) is a pill with the button's name, "Guide", "PS", "Home" or
  "Steam"; the controller's own logo only once brand logos are cleared as a trademark question. A text about one
  controller only (e.g. the Steam Controller's action set in "Steams Desktop-Steuerung zulassen") always shows that
  controller's symbols (`InfoText.Symbols`). Texts keep the neutral names; a toast already shown stays.

## Dialogs

Dialogs (Ansicht, settings' prompts) show an ✕ in the top right corner while mouse or keyboard is in use
(`InputInUseViewModel.ShowsCloseButtons`); the controller closes them with B. A dialog that only informs has no "Schließen"
button besides; the exception and the other design rules: `design.md`.
