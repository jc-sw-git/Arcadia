# Product decisions: Library

Agreed with the owner; don't change without asking. Read this before changing this area
(index in `CLAUDE.md` → Product decisions).

- Page sizes 10/25/50/100, default 25. A size is selectable if the library has *more* games
  than the next smaller size (`PageSizes.IsAllowed`).
- Sorting: name / install date / last played, each ascending or descending; games without a
  date always last.
- Sort order and preferred page size are global, not per tab, and are saved; Arcadia opens in the
  tab the user last chose, always on page 1 with the first game focused (owner, 2026-10-17).
- The header's tab strip sits in the middle between logo and status, as wide as its tabs need, LB/RB right beside
  them. At its widest it keeps 40 px to both, with a divider in each gap; the selected tab stands in its middle and
  it fades out where more tabs follow. Dividers also stand between a status message and the search chip and
  between the settings chip and the date (owner, 2026-10-21).
