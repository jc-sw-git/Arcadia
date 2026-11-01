# Product decisions: Settings page and status messages

## The page (#15)

- A whole page in place of the library (≡, Enter, the chip); B or ✕ go back to the same place. Categories are tabs in
  the header like the launcher tabs (LB/RB, Shift+Q/E, a click; Q/E = LT/RT switch tabs inside a category, e.g.
  launcher pages, as in the library), only those with content; a warning or an error of a category shows its symbol
  beside the name, info nothing. Choosing a category's tab opens it at its top level (the launcher list, not the
  launcher page left there).
- Pattern "Leiste" for every category: one column of rows, ▲ ▼ (W/S) move, ◀ ▶ (A/D) change switches and choices at
  once, A (Space) starts actions; only settings and actions are rows, facts about a setting (e.g. Pan's status, what a
  removal takes) are boxes (`InfoSection`) below its description in the info panel. The info panel beside the rows
  explains the focused row, RS (Shift+W/S, the wheel) scrolls it, fades show more; the mouse over a row focuses it.
- ◀ ▶ wrap at the ends of a value range (e.g. Zeigergeschwindigkeit 1 → ◀ → 9; a switch flips with either arrow), so
  an arrow is never greyed out just for standing at an end (a click on a greyed-out arrow fell through to the row and
  changed the value the other way). Only a value that can't be taken greys an arrow out (e.g. "Aktiv" for a launcher
  that isn't installed). A row that can't change now (e.g. while Pan sets up) still takes the focus. The footer names
  the focused row's buttons. Applies at once, no save button. Questions (e.g. "Steam beenden?") stay dialogs over the
  page; the header's status symbols open their category.
- Dependent options sit in the same box as their main switch; on the settings page in the same group right below it,
  indented one step per level (`SettingsRowViewModel.Indent`), greyed out while it is off, keeping their values. Each
  group of the settings page is a box of its own.
- Every launcher page has its own group "Controller" with the launcher's default Steam Controller mode (#178,
  `docs/product/launchers.md`); the controller category has no per-launcher links.

## Status messages

`HealthIssue` (header, the settings row and category they belong to) has three levels, each with its own symbol:
**Info** (round blue i) for intended behaviour in a situation, e.g. "Windows-Neustart ausstehend", or a launcher
without games; **Warning** (yellow triangle with !) when a function is impaired and the user must act, e.g. Steam not
running, usbip driver missing, SISR running beside Pan; **Error** (red X) for lasting failures, e.g. Pan keeps ending
at once. The header shows the worst level; info alone doesn't make a status unhealthy. A failed fix shows its error at
its own row (the error icon, the message in place of the subtitle with how to go on, the detail panel), not at the
switch.

## Category "Controller"

Its rows (Pan, the mouse mode, Steam, SISR): [settings-controller.md](settings-controller.md).
