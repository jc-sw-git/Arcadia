# Product decisions: On-screen keyboard

On-screen keyboard (#129, #110). Where the search and the mouse mode use it: `search.md`, `mouse-mode.md`.

## One keyboard, laid out like the real one

- One design for every keyboard Arcadia shows, in the search, over other windows in the mouse mode, later in settings
  or plugins (owner, 2026-10-22).
- A 1:1 replica of the chosen real keyboard, like Steam's: the keys where the real one has them (DE, UK, FR, ES ISO
  with the tall Enter, US ANSI), ⌫ ↹ ↵ ⇧ space and AltGr with symbol and name (the name centred in the key, symbol or
  icon at its left edge, the button at its right), caps lock a blank key the focus skips, no Ctrl/Alt/Win.
- A right block with "Layout: DE" (R3), "Nach oben/unten" (⧉), "Ausblenden" (B) and the arrows.
- On the normal level a key shows its character large, the shift one small above (left out when it is just the capital
  letter) and the AltGr one small at the bottom right. With shift or AltGr on, only that level's characters; keys
  without one stay empty and the focus passes over them. Dead keys type their own character. A layout without AltGr
  (US) greys RB out.

## What the user of the keyboard decides

- Whoever shows it decides what it types into and may grey out or leave out keys at any time (`IKeyboardTarget`,
  `Configure`); a left-out key leaves its place empty.
- It may also name the characters its text takes: then every key whose character on the shown level isn't one is
  greyed out (symbol still shown, the focus passes over it), the space bar too, and ⇧ or AltGr when their level offers
  none (owner, 2026-10-23; e.g. a launcher's keyword).

## Moving over the keys

With the controller the D-pad (left stick) moves over the keys, passing over empty, greyed-out and left-out ones:
left/right at a row's last key go on to the nearest key wholly beside it in another row (² on AltGr: left to ↹),
up/down to the nearest key above or below, so a far key in the next row doesn't win over one right below in the row
after (↹ on AltGr: down to ⇧, not ↵); the tall Enter is one key in both rows (owner, 2026-10-22). In the mouse mode the
buttons are the mouse mode's (`mouse-mode.md`). The gamepad buttons stay Arcadia's, not Steam's (owner, 2026-10-21).
