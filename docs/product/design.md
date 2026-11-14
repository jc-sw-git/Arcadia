# Product decisions: Design rules

Rules every screen and dialog follows, in Big Picture and desktop mode. Each rule names where its details are; a
detail lives there, not here. The look (fonts, covers, raised buttons): `README.md` → Look. Buttons and keys:
`controls.md`. Wording: `ui-texts.md`.

## Buttons and texts

- **Glyphs follow the controller in use**, and switch as soon as another controller, the keyboard or the mouse is
  used (`controls.md` → Button hints and texts).
- **Presses are glyphs in texts**, never spelled out (`controls.md` → Button hints and texts).
- **The footer names only what works now.** It lists the buttons that act on the focused element in this moment; a
  greyed row, an action that does nothing here or a list that can't scroll gets no hint (e.g. RS only while an info box
  overflows, `controls.md`).

## Focus

- **The focus is always visible and never clipped.** Exactly one element carries the focus ring; it is drawn whole,
  not cut by a scroll edge, a panel's border or a fade (`docs/architecture/avalonia.md` → focus rings and clipping). A
  list or grid scrolls the focused element out of its fades.
- **After every page or dialog change the focus stands on a sensible element:** the first element of a page opened
  anew, the element that opened a dialog once it closes, the place left when going back (e.g. the game in the
  library, the launcher in the list). Greyed elements keep the focus where a row must explain itself
  (`settings.md`, `detail-page/page.md`).
- **B goes back exactly one level:** a menu or the keyboard first, then the dialog, then the page; B never erases or
  confirms. B always leads back to where the player came from, with no exception: a detail page to the library, the
  search or the tag list it was opened from, the settings to the page they were opened over. Each flow's way back is in
  its file (`search.md`, `settings.md`, `detail-page/`). Esc and the mouse's back button act as B (`controls.md`).

## Scrolling

- **Whatever scrolls or can scroll shows the fade-out.** Lists, grids, tab strips, info boxes and long texts fade into
  their surface at each edge where more follows (`EdgeFade`, the curves in `Theme.axaml`), and show none where
  everything fits. Content that can grow (a list, a description) gets its fade even if today's content fits.

## Dialogs

- **✕ for the mouse, B for the controller, no separate "Schließen" button**: `controls.md` → Dialogs. A dialog gets
  a "Schließen" button only where a click beside it must not dismiss it, such as Pan's firmware notice, which
  [B] or its own buttons close (`controllers-in-games.md`). Also with one: the failed driver installation
  ("Schließen", A or B, `settings-controller-install.md`) and the controller settings' notice that a game is running ("Erneut
  versuchen" | "Schließen"), where "Schließen" is one of two answers.

## Sizes

Laid out at 1920 × 1080 for a TV seen from the sofa:

- No text below 12 px; below 14 px only short captions in capitals, badges, chips and the letters in a button glyph
  (a small glyph's letter is 11 px). Reading text (descriptions, info panels, toasts) at least 16 px.
- Every element the mouse can click is at least 44 × 44 px (the ✕ is 44 × 44) or a whole row, tile or pill.

## Feedback

- **Long work shows progress.** Anything that may take longer than about a second shows that it runs (a spinner,
  "Wird … …", a progress bar, a loading state like "Artwork ändern"'s) and never blocks the UI.
- **A failure says what to do.** A failed action names what happened and the next step ("Erneut versuchen", a fix
  in the settings, where to look); the levels and where it shows: `settings.md` → Status messages, Toast levels.

## Theme

- **Colours, brushes and shadows come only from `Theme/Theme.axaml`**, never as values in a view or style, so a theme
  can replace them all in one place (`CLAUDE.md` → Definition of Done, Themeable).
