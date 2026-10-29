# Product decisions: Search

Search (#12).

## Opening and the two layouts

- Opened with X the on-screen keyboard is out and the hits sit in one row above it (sliding sideways, faded
  neighbours at the edges, LT/RT buttons beside them, "RS Treffer wählen" above).
- Opened with F or a click, or after B, a real key or the tab on the keyboard's top edge, the keyboard slides in and
  the hits fill a grid that scrolls up and down (faded rows at the top and bottom); the D-pad and A act on the hits as
  on covers. X brings the keyboard back.
- The legend at the bottom is always there and names the input in use: the controller's buttons, or mouse and
  keyboard (it never hides, so the keyboard above it doesn't move under a click).

## Buttons

- B closes the layout menu first, then puts the keyboard away, then closes the search; B never erases.
- D-pad/left stick type, the right stick picks a hit. LT/RT (or Page Up/Down, the wheel) turn the hits a window at a
  time, as LT/RT turn pages and the detail page; the row wraps round like the detail page, the grid stops at its ends
  like the library and the wheel. A scroll bar shows where the shown hits are: across below the row, down beside the
  grid.
- X erases (held: repeats), Y space, LB shift and RB AltGr (each: tap on/off, held only while held; one at a time).
- L3 "Filter und Sortierung": launchers (any number) and the library's sort orders plus relevance, the default; not
  saved. R3 layout menu (all five, the last chosen saved globally).
- ≡ starts the focused hit, ≡ held 1 s opens its details, ⧉ its menu; with a real keyboard Enter and Enter held do the
  same.

## Keyboard

The shared on-screen keyboard (`docs/product/on-screen-keyboard.md`) without ↹ and the edge key; ↵ starts the focused
hit like ≡, the arrows move the field's caret (up: to the start, down: to the end), "Ausblenden" puts it away like B.

## Chips, prefix and hits

- The sort button sits in the chip row; with many launchers the chips shrink (keyword first, then the name, last only
  the icon) and a divider appears before the sort button.
- The `/launcher` prefix still works when typed but isn't advertised; chips and filter rewrite a typed prefix and never
  add one.
- No hits and nothing typed show an empty state the theme draws (`ContentControl.searchEmpty`). ™ ® © count for nothing
  in the search.

## Detail page from the search

A detail page opened from the search turns through the hits in their order; LB/RB and Y do nothing there, X returns to
the search with its text unchanged, and B, as on every detail page, goes to the library: the tab of the game's
launcher, its page, the game focused; the search closes.
