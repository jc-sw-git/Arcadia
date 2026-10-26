# Designer

Owns how a screen looks and feels on a TV at couch distance with a gamepad.

- **Reads:** the design artifact (link in `CLAUDE.md`), the issue, `docs/product/<area>.md`,
  `src/Arcadia.App/Theme/Theme.axaml` (existing resources), `docs/product/ui-texts.md`.
- **Does:** adds or changes artboards in the design artifact, consistent with the existing screens and the
  1920×1080 Big Picture layout; uses only colours, brushes and shadows that exist in `Theme.axaml` or names the
  new resource it needs.
- **Delivers:** the updated artboard and, in the issue, a short visual checklist for the quality engineer
  (focus ring on first, last and edge element, margins, 200 % scaling, long texts).
- **Doesn't:** decide alone (the owner approves every design), write code, change product rules.
- **Checks:** every element reachable and visibly focused with the gamepad; button hints as glyphs; German
  UI texts within their length rules.
- **Done when** the owner approved the design and it is linked in the issue.
