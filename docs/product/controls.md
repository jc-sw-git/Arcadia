# Product decisions: Controls, button hints and dialogs

## Buttons and keys

- One "Ansicht" modal (Y) for sort + page size. X = search (all launchers, on-screen keyboard, `/steam-epic term`
  prefix, keyword per launcher), R3 = keyboard layout switch, View/⧉ or right click = context menu, Menu/☰ = settings.
  The context menu stands beside the cover after a right click on it, over the preview's "Optionen" button after ⧉, R
  or a click on that button. No "all launchers" tab. No dedicated favourite button.
- The keyboard plays like a game, one hand on WASD: WASD move, Space = A (held: details), Esc = B, F = X, V = Y,
  Q/E = LT/RT, Shift+Q/E = LB/RB, 1 … 0 = launcher tab 1–10, Enter = ≡, R = ⧉. Arrows, Page Up/Down, Ctrl+Tab, F2,
  the menu key and Shift+F10 stay as unadvertised alternatives. In the search a real keyboard types, so only keys
  without text act there (arrows, Enter = ≡ starts the hit, Esc, Page Up/Down).
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
  "B" in a text: `ButtonGlyphTextTests` fails on one in the App's strings.
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
  shapes as vector icons from the theme. Only buttons Arcadia uses have a symbol; back grips and trackpads come
  with their first feature. Guide ("[Guide]", #315) is a pill with the button's name, "Guide", "PS", "Home" or
  "Steam"; the controller's own logo only once brand logos are cleared as a trademark question. A text about one
  controller only (e.g. the Steam Controller's action set in "Steams Desktop-Steuerung zulassen") always shows that
  controller's symbols (`InfoText.Symbols`). Choosing the symbols by hand: #291. Texts keep the neutral names; a toast already shown stays.

## Every controller in games (#238)

- While "Controller-Wächter (Pan)" is on, Pan hides every controller Arcadia reads from games and Steam, and each one
  reaches games as an emulated pad of Arcadia's, of its own kind where Arcadia has it (DualShock 4, DualSense,
  DualSense Edge, Switch 2 Pro; every other controller an Xbox 360 pad), or the layout the player chose per model in
  "Controller zuordnen" (`settings-controller.md`); a model set to "Nicht übernehmen" reaches games directly, as with Pan off,
  one per controller, in the order they connected. The same rules as for the Steam Controller
  below: Back + Start never reaches the game, ⧉ and ≡ alone a little later (held, as held), rest while Arcadia is in
  front or the mouse mode is on. Unlike the Steam Controller's pad, they also pass while Steam itself is in front: Steam sees
  only them. With "Steams Desktop-Steuerung zulassen" on they pass in front of Arcadia and in the mouse mode too, ⧉ + ≡
  included, as the copy does (`mouse-mode.md`). Guide/PS/Home passes on (Steam's overlay, the Game Bar); a game's rumble reaches the real controller.
- Never both: a controller Pan can't hide reaches games directly and isn't emulated (the settings list it, see
  `settings-controller.md`). An XInput pad (an Xbox pad, 8BitDo in XInput mode) that Steam or a game
  already opened is reconnected by Pan when it is hidden (gone for a few seconds, then emulated); by Bluetooth that
  can't be done, so it is emulated only once the player reconnects it.

## The Steam Controller in games (#213, #242, #178)

- In games the Steam Controller is an Xbox 360 pad from Arcadia; the game never sees Arcadia's combo Back + Start.
  ⧉ + ≡ counts for switching the mouse mode on only when the second button comes within 100 ms of the first,
  everywhere in Arcadia (ending the mode still takes them in any order). So while the mouse mode is not "Aus", ⧉ and ≡
  reach the game a little later: let go within 100 ms, as a 50 ms tap; held alone longer, as held from then on, ⧉ also
  while it brings Arcadia back. The pad passes input only while a game on "Arcadia" is in front (#357); everywhere
  else (Arcadia, the mouse mode, Steam, a browser, the desktop, no game running) it reports rest. Own hold times and a
  pause key per game: separate issue.
- Per game the Steam Controller mode has three values: "Standard (Arcadia)" / "Standard (Steam-Eingabe)" (follows the
  launcher's default, set on the launcher's page, `docs/product/launchers.md`; factory: Steam games Steam-Eingabe,
  all others Arcadia), "Arcadia" (Arcadia's pad) and "Steam-Eingabe" (no pad of Arcadia's, the game's own Steam
  configuration). [A] or a click cycles Standard → Arcadia → Steam-Eingabe → Standard; a change applies at the game's
  next start. It shows in preview, detail page and context menu whenever a Steam Controller is connected and
  "Controller-Wächter (Pan)" is on; off, or off Windows, it is gone. While Pan's emulation is on but doesn't run (a
  part missing, hiding lost, stopped) it is greyed out at "Steam-Eingabe" with a triangle, yet focusable: [A] (or a
  click) opens the controller settings on the row that says why, [B] goes back (designs T242Library/T242Zustaende).
- While the copy passes input: Steam gets the Steam Controller for a game on "Steam-Eingabe" and while Steam itself
  is in front, also during a game on "Arcadia" (the pad rests then, #357); never while Arcadia is in front or the
  mouse mode is on. Otherwise Steam
  reads a controller at rest except the Steam button and "…" (`SteamControllerCopyRules`).
- Pan's firmware notice (design T242Firmware without its wait, #295): "Steam will die Firmware aktualisieren" comes to
  the front over everything when Steam tries an update through the copy and no library game is in front (then only a
  notice over the game: "Pan: Steam kann die Firmware nicht aktualisieren, solange Arcadia den Steam Controller
  verwaltet. Für das Update nach dem Spiel Arcadia beenden."). Text: "Solange Arcadia Steam Controller und Puck
  verwaltet, kommt kein Firmware-Update bei ihnen an. Steam meldet deshalb einen Fehler. Für das Update gib die
  Controller frei oder beende Arcadia, und starte es dann in Steam erneut." Not clicked away. "Controller freigeben"
  and "Arcadia beenden" act when [A] (or the mouse) is held 1 s, filling while held; "Schließen" (focused first) acts
  on a plain press, and [B] closes it too. Only a fresh press counts. After the release it says "Steam Controller und
  Puck freigegeben", how the update goes on and "Damit Arcadia sie wieder übernimmt: Pan in den
  Controller-Einstellungen aus- und wieder einschalten, oder Arcadia neu starten."; [A] or [B] closes it.

## Dialogs

Dialogs (Ansicht, settings' prompts) show an ✕ in the top right corner while mouse or keyboard is in use
(`MainViewModel.ShowsCloseButtons`); the controller closes them with B. A dialog that only informs has no "Schließen"
button besides.
