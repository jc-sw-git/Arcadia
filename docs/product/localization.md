# Product decisions: Localization

Category "Lokalisierung" in the settings (#14, #553), between "Datenquellen" and "Über Arcadia", in the row pattern of
`settings.md`. The language (group SPRACHE) comes with #554.

## Group BILDSCHIRMTASTATUR

- "Standard-Layout": ◀ ▶ over the five layouts (Deutsch · QWERTZ, English (US), English (UK), Français · AZERTY,
  Español), wrapping. Every on-screen keyboard opens with it: the search, every settings text field, the mouse mode's
  keyboard over other windows.
- Indented below, one switch per layout for the quick selection: the layouts R3 offers on an open keyboard, in this
  order. The default's switch is on and greyed out ("Das Standard-Layout ist immer dabei") and still takes the focus; a
  new default leaves the previous one in the quick selection, its switch free again. Without a choice of the player all
  five are in it.
- R3 switches the layout only until the keyboard closes; the next opening shows the default again, nothing is saved.
  With the default alone in the quick selection, "Layout" (R3) is greyed out and does nothing.
- Without a saved default Arcadia takes the system's keyboard layout if it is one of the five (Austria's German counts,
  Swiss German, Dvorak or Italian don't), else the one of the UI language; that default is not written to
  `settings.json` until the player changes the row. The layout last picked with R3 before #553 became the default.

## Group UHRZEIT

- "Uhrzeit": "Wie im System" (default) · "24 Stunden (21:58)" · "12 Stunden (9:58 PM)". It applies at once to every time
  Arcadia shows (header clock, "seit" in the status, "Letzte Meldungen", "Zuletzt gespielt", "Zuletzt geprüft", "um …
  Uhr", the running game's "Läuft seit"); a time already on screen changes when it is shown again, the header clock at
  once. "Uhr" follows a time only in 24 hours ("um 9:58 PM", "um 21:58 Uhr"); 12 hours show AM/PM also where the region
  has none of its own.
- Order and separators of dates and numbers follow the system's regional format ("1.234" / "1,234", "13.11.2026" /
  "11/13/2026"); day and month names, units and the space before "%" follow the UI language. A change in Windows shows
  from Arcadia's next start.

## Failure cases

An unknown or unreadable layout, quick selection entry or clock value in `settings.json` (also of the wrong type) becomes
the default (system layout, the known layouts of the list or all five, "Wie im System") with one log line each; the
rest of the file is kept.
