# Product decisions: Launchers: activate, order, launcher page, first start

Agreed with the owner; don't change without asking. Read this before changing this area
(index in `CLAUDE.md` → Product decisions).

- Launchers (#136, owner 2026-10-22): the settings' first category "Launcher" lists "Aktiv · erscheinen als Tabs" in tab
  order (key 1 … 0), then "Nicht aktiv" (installed first, then not found, greyed out). ◀ ▶ switch "Aktiv": off means not
  scanned, no tab, no search hits, not watched; on goes to the end of the active ones, off to the top of the inactive
  ones; a click anywhere on the switch's box switches it too (a click elsewhere on the row opens its page). Not found can't be switched on; active and no longer found can be
  switched off. Y lifts a row, ▲ ▼ move it,
  Y drops it (under "Nicht aktiv": off), B cancels; the mouse drags. X, or the row "Alle neu einlesen" in a group "Bibliothek"
  below the launchers (for the mouse alone, its pill shows [X] / F, owner 2026-10-24), reads all again; A opens the launcher's page
  (#137, below). Saved as `ActiveLaunchers` (order = tabs); without it every launcher found is active. An active launcher
  without games or no longer found keeps its tab with an empty state ("Keine Spiele in …" / "… nicht gefunden",
  "Launcher-Einstellungen" and "Neu einlesen" / "Erneut suchen"); with no launcher active the library says "Kein Launcher
  aktiv" the same way, with only "Launcher-Einstellungen" (nothing to read before one is switched on, owner 2026-10-24). Beside the rows two boxes: "Launcher-Reihenfolge" (the tabs as they will be, the
  library's selected tab not marked, owner 2026-10-23) and "Launcher-Details" (head,
  status message, library, location; no repeat of what row and preview show). An active launcher no longer found is a
  warning; one without games is information (blue i in its row and note, no symbol on the category's tab, owner
  2026-10-24). "Steam Controller Kompatibilität" has a lamp
  beside its name while on: green while SISR runs. A status message row shows one short line below its title
  (`HealthIssue.Summary`), the whole text in the info panel; descriptions below a row name stay at one, at most two
  lines. A row's action is a pill as wide as the value chips, filled with the accent while focused (owner, 2026-10-22).
- Launcher page (#137, #138, owner 2026-10-23): A or a click on a launcher in the list opens its page in place of the rows;
  B (or a click on "Launcher" above its name) goes back to the list with that launcher focused, LT/RT (Q/E, a click on a
  neighbour in the head) turn to the
  launcher before or after it in the list's order, wrapping. Groups "Allgemein": Aktiv, "Name im Tab", "Such-Kürzel",
  "Logo"; "Bibliothek": "Ordner" and "Neu einlesen" (only active and found). Name and keyword are written with the shared
  on-screen keyboard below the page (without ↹ and the edge key; the search's buttons, ≡/Enter take, B/Esc drop); a
  keyword's keyboard offers only the keys it may hold (lower-case letters and digits); a
  problem (keyword taken, too long) shows in red while typing, taken anyway the old value stays and a toast says
  why; empty means the launcher's own again. "Logo" is a button, not a choice (owner, 2026-10-23):
  "Hochladen …" opens the file dialog (PNG, SVG, ICO, WebP up to 2 MB, copied); only while an own logo is used an indented
  row "Standardlogo" below it offers "Wiederherstellen". In the tab a logo is fitted into 24 × 24 px. "Neu einlesen" reads
  only that launcher (also on its empty tab); X and "Alle neu einlesen" in the list read all. "Ordner" is only a fallback when Arcadia can't find
  the launcher (no choice between automatic and own): the folder is checked when picked and taken only if the launcher
  is there, then it is switched on and read. The mouse mode's "Bei System-Dialogfenstern einschalten" (under "Mausmodus",
  for "Von Hand" and "Automatisch", greyed out with "Aus") switches it on while a dialog opened from the controller is open.
- First start (#139, owner 2026-10-11, 2026-10-12 and 2026-10-22): Arcadia never takes a launcher on just because it is
  installed. While `settings.json` has no `ActiveLaunchers` (also after an update from a version without it), a page
  "Welche Launcher soll Arcadia nutzen?" stands in place of the library, pattern "Leiste": "Gefunden" switched on with
  their games, "Nicht gefunden" greyed out and not switchable; ◀ ▶ (and A, a click on the switch) switch, the info panel
  shows above the preview "Launcher-Reihenfolge" as in the launcher settings, below the focused launcher (games,
  location, keyword), "Später änderbar" and "Fertig"; the header has date and clock like every page. ≡,
  Enter or a click on "Fertig" save the list; B does nothing. Only on/off is chosen there, order and looks in the
  settings. `--demo` skips it (all launchers active, nothing saved); `--first-run` shows it again (with `--demo` the
  answer holds for the session only).
