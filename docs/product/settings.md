# Product decisions: Settings page and status messages

Agreed with the owner; don't change without asking. Read this before changing this area
(index in `CLAUDE.md` → Product decisions).

- Dependent options sit in the same box as their main switch; on the settings page in the same group right below it,
  indented one step per level (`SettingsRowViewModel.Indent`), greyed out while it is off, keeping their values. Each
  group of the settings page is a box of its own (owner, 2026-10-22).
- Settings (#15, owner 2026-10-21 and 2026-10-22): a whole page in place of the library (≡, Enter, the chip); B or ✕ go
  back to the same place. Categories are tabs in the header like the launcher tabs (LB/RB, Shift+Q/E, a
  click; Q/E = LT/RT switch tabs inside a category, e.g. launcher pages, as in the library, owner 2026-10-23), only those with content; a warning or an error of a category shows its symbol beside the name, info nothing.
  Pattern "Leiste" for every category: one column of rows, ▲ ▼ (W/S) move, ◀ ▶ (A/D) change switches and choices at
  once, A (Space) starts actions; only settings and actions are rows, facts about a setting (e.g. SISR's status,
  version, drawbacks) are boxes (`InfoSection`) below its description in the info panel. The info panel beside the
  rows explains the focused row, RS (Shift+W/S, the wheel) scrolls it, fades show more; the mouse over a row focuses
  it. A row that can't change now (e.g. while SISR starts) still takes the focus. The footer names the focused row's buttons. Applies at once, no save button. Questions
  (e.g. "Steam beenden?") stay dialogs over the page; the header's status symbols open their category.
- Status messages (`HealthIssue`, header, the settings row and category they belong to) have three levels, each with its own
  symbol: **Info** (round blue i) for intended behaviour in a situation, e.g. SISR paused because other controllers
  use Steam Input, or a launcher without games; **Warning** (yellow triangle with !) when a function is impaired and the user must act, e.g.
  Steam not running, usbip driver missing, SISR switched on but no longer installed; **Error** (red X) for lasting
  failures, e.g. SISR keeps crashing. The header shows the worst level; info alone doesn't make a status unhealthy
  (owner, 2026-10-21).
