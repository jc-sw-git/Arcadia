# Product decisions: changing a game from the detail page

Index: [README.md](README.md).

- "Artwork ändern" (#490, prototype in #13) works on the open game at once, nothing to confirm or cancel: A uses the
  focused tile, also the one in use (it becomes set by hand); Y gives only the open tab back to Arcadia's choice. An own
  file stays offered under "Eigene Datei" after another choice, until X replaces it; it is cropped centred like every
  image. A file that isn't a readable PNG, JPEG or WebP of at most 20 MB is refused with a warning. While a tab's images
  are looked for, the grid shows a loading state (frames under a magnifier) with a funny text that changes at random
  every 2.5 s (#532). The preview draws the detail page as it is laid out; "Format" names the image's pixel size.
- "Titel ändern" (#496) opens the on-screen keyboard over the page, prefilled with the shown title; ↵, Enter or ≡ take it,
  B or Esc keep the old one. Spaces around it are dropped; an empty title or the automatic one (the launcher's, or a
  provider's correction) clears the own title. The own title shows everywhere at once (library, search, sorting, the
  carousel's order) and outlives rescans, "Alle Metadaten neu laden" and the game's absence from the library. Like
  every game action also in the context menu; from there the focus follows the game to its new place in the sort.
  "Titel zurücksetzen" is greyed without an own title. While the game has one, ⧉ (R) shows and hides "Original-Titel:
  <automatic title>" under the title in place of the info line (so a two-line title keeps the button row in place), the
  hint "[⧉] Original-Titel" only then (R3 is the filter there, as in the library, #530); every page shown anew (another game, the page left) starts without the line.
  While `user-data.json` is unreadable both rows are greyed with the reason, the header has a warning, and its entry
  opens "Eigene Titel" in "Datenquellen", which says why (damaged, locked, from a newer Arcadia) and what to do.
- "Tags" (#495) opens the tag window of the game; own tags show before the providers' tags
  ([own-tags.md](../settings-data-sources/own-tags.md)).
