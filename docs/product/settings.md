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

## Category "Controller" (#169, #243, #242, #214; designs, section "#242 Pan")

- Group "PAN" first, then "STEAM" (only "Steams Desktop-Belegung ignorieren", subtitle "Alle Controller: Maus und
  Tasten, die Steam simuliert, kommen nicht an"; no dependent of Pan's switch, so not indented).
- "Controller-Wächter (Pan)" is a switch (`AppSettings.PanEnabled`, on by default; off: nothing is emulated or hidden,
  no mode, no warning rows, even with a part missing). Its subtitle and lamp say the state: "An – deine Controller
  werden von Arcadia verwaltet" (green), "Wird eingerichtet …", "Bereit – kein Steam Controller verbunden" (grey),
  "Es liegt ein Problem vor. Prüfe die Warnmeldungen darunter." (yellow), "Angehalten – der Grund steht darunter"
  (red). Every part missing: the row offers "Treiber installieren" ("Wird installiert …", after a failure "Erneut
  versuchen" with the red X and the error in the info panel). While a game runs or starts the switch is greyed out:
  "Erst änderbar, wenn „…“ beendet ist". Off Windows: "Auf diesem System noch nicht verfügbar". Its info panel says
  the mode is chosen per launcher on its page and changed per game in its details.
- Below it, indented, one row per reason: a missing part (usbip, VIIPER, Pan, HidHide: a warning row with
  "Installieren", shown also while the switch is off; the category and the library's header carry the triangle,
  checked once at start, then while the settings are open; while such a row offers the install, the switch stays a
  switch and its status points down to it, so there is one install button, not two), HidHide's states (e.g.
  "HidHide-Konfiguration ist geöffnet … Bitte schließe sie", asked only while the settings are open; an old HidHide:
  "HidHide-Version wird nicht unterstützt" with "Aktualisieren"), "Steam Controller lässt sich nicht verstecken",
  "Emulation startet nicht", "Emulation angehalten" with "Erneut versuchen"; "Windows-Neustart ausstehend", "HidHide:
  Neustart ausstehend" and "Pan räumt noch auf" carry the blue i. "Controller wiederherstellen" (Pan's `--restore`)
  stands below the problems, indented like them, while Pan is installed, also while the switch is off.
- After "Nein" in the takeover question, or a release for a firmware update, the switch reads "An – Steam Controller
  und Puck ausgenommen, siehe unten" (no triangle) and the indented row "Steam Controller und Puck übernehmen" (blue i,
  "Übernehmen" held 3 s, a bold red warning in its info panel) takes them over again: the press is the consent, the
  toast "Pan: … übernommen" says so, the row goes and the focus moves to the switch. Neither survives a restart of
  Arcadia.
- "Erneut versuchen" on a Pan row (#308): the row stays while the attempt runs (also when its problem is gone
  meanwhile), reads "Wird versucht …" with a spinner and can't be pressed; when the attempt ends, a toast says how:
  "Pan: Controller-Wächter läuft wieder", "Pan: Erneuter Versuch ohne Erfolg. …", or why there is nothing to do (no
  Steam Controller, Steam Controller and puck excluded); only switching Pan off meanwhile has none.
- Pan's notices: a toast while Arcadia is in front, the overlay notice (where the mouse mode shows its label) while a
  game is in front, so the player sees them in the game too. A hold the player is watching goes on: during the View
  hold the notice stands below "Zurück zu Arcadia". An installed Pan that isn't the bundled one is replaced at start
  without a question (#297, page "Pan wird aktualisiert", `docs/controller-support/pan-update.md`).
- Group "SISR" (#214, designs T214Settings and T214Dialog) below "STEAM", only while SISR or its Steam marker is on
  the PC: "SISR entfernen" [Entfernen] and, indented, "SISR behalten" [Ausblenden]. Arcadia never starts SISR and
  removes nothing without the player. With the marker and Steam running, [Entfernen] asks "Steam beenden?" ([A]
  Weiter, [B] Abbrechen); a Steam game shows "Steam-Spiel läuft" with its name and only "Schließen". While it removes,
  the dialog "SISR wird entfernt" names the step and can't be closed. Everything gone: the toast "SISR ist vollständig
  entfernt.", the group goes, the focus moves to the row above. Something left: the row reads "Nicht alles ließ sich
  entfernen" ("SISR wurde nicht entfernt" when a Steam game, a Steam that won't close or an SISR that won't stop kept
  everything), the cause in its info panel; [Entfernen] tries again. "SISR behalten" hides the group for good
  (`AppSettings.KeepSisr`; the factory reset, #146, shows it again); SISR stays as it is. While SISR runs and Pan is
  on, the warning row "SISR läuft" stands under Pan, also with the group hidden. What goes and why:
  `docs/controller-support/sisr-removal.md`.
