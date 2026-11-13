# Product decisions: Settings page and status messages

## The page (#15)

- A whole page in place of the library (≡, Enter, the chip); B or ✕ go back to the same place. Categories are tabs in
  the header like the launcher tabs (LB/RB, Shift+Q/E, a click; Q/E = LT/RT switch tabs inside a category, e.g.
  launcher pages, as in the library), only those with content; a warning or an error of a category shows its symbol
  beside the name, info nothing. Choosing a category's tab opens it at its top level (the launcher list, not the
  launcher page left there) and at its first row, scrolled to the top, not at the row left there (#237); coming back
  from a launcher's page, the list stands on that launcher.
- Pattern "Leiste" for every category: one column of rows, ▲ ▼ (W/S) move, ◀ ▶ (A/D) change switches and choices at
  once, A (Space) starts actions; only settings and actions are rows, facts about a setting (e.g. Pan's status, what a
  removal takes) are boxes (`InfoSection`) below its description in the info panel. The info panel beside the rows
  explains the focused row, RS (Shift+W/S, the wheel) scrolls it, fades show more; the mouse over a row focuses it.
  It starts at its top for another row or when the row's value or greying changes, never because a run in the
  background refreshes the rows (#547).
  A row with named values gets one box per value below its name, the current one highlighted (#506; a number range,
  e.g. a hold time, gets none); a greyed row with a reason shows it as the blue note at the very top.
- Rows that don't fit fade into the page at an edge where more rows follow, none where everything fits (#237, same
  look as the library). ▲ ▼ and a jump to a row scroll it out of the fades with its focus ring, at a group's first row
  with the heading; the mouse focuses a row under a fade without scrolling, a click passes through, the wheel scrolls.
- ◀ ▶ wrap at the ends of a value range (e.g. Zeigergeschwindigkeit 1 → ◀ → 9; a switch flips with either arrow), so
  an arrow is never greyed out just for standing at an end (a click on a greyed-out arrow fell through to the row and
  changed the value the other way). Only a value that can't be taken greys an arrow out (e.g. "Aktiv" for a launcher
  that isn't installed). A row that can't change now (e.g. while Pan sets up) still takes the focus. The footer names
  the focused row's buttons. Applies at once, no save button. Questions (e.g. "Steam beenden?") stay dialogs over the
  page; the header's status chip opens the status modal (below).
- Dependent options sit in the same box as their main switch; on the settings page in the same group right below it,
  indented one step per level (`SettingsRowViewModel.Indent`), greyed out while it is off, keeping their values. Each
  group of the settings page is a box of its own.
- Every launcher page has its own group "Controller" with the launcher's default Steam Controller mode (#178,
  `docs/product/launchers.md`); the controller category has no per-launcher links.

## Status messages

`HealthIssue` (header, the settings row and category they belong to) has three levels, each with its own symbol:
**Info** (round blue i) for intended behaviour in a situation, e.g. "PC-Neustart ausstehend" (also HidHide's and usbip's restart pending), or a launcher
without games; **Warning** (yellow triangle with !) when a function is impaired and the user must act, e.g. Steam not
running, usbip driver missing, SISR running beside Pan; **Error** (red X) for lasting failures, e.g. Pan keeps ending
at once. The header shows the worst level; info alone doesn't make a status unhealthy. A failed fix shows its error at
its own row (the error icon, the message in place of the subtitle with how to go on, the detail panel), not at the
switch.

## Status modal (#166)

- Y (V) or a click on the header's status chip (Y/V/△ and the symbols, like the search and settings chips) opens
  "Status" in the library and on the detail page, only while the header shows a message or the update and never over
  another dialog or menu; otherwise Y does nothing. "Controller wird eingerichtet …" alone is no entry and shows no chip.
- One entry per message the header shows: every Steam Controller message (info too), "Pan läuft nicht" and the Arcadia
  update. Each: level symbol in a tinted disc, title, the row's short line, "seit hh:mm" (when it appeared; a level change
  counts as new), its fix (if any) and always "Zu den Einstellungen".
- L3 (C) switches "Dringlichkeit" (error, warning, update, info; newest first within a level) and "Neueste zuerst"; the
  footer names it, saved for every opening, the focus stays on its entry.
- A controller fix closes the modal, opens "Controller" on its row and starts there (its question, the progress
  dialog); a fix already running only jumps there. "Jetzt aktualisieren" runs from the modal; a failure is an error
  toast and the entry stays. "Zu den Einstellungen" opens the category on the message's row ("Pan läuft nicht": the row
  that says why; the update: "Jetzt aktualisieren"), its top row if the message went meanwhile; B returns to library
  or detail page.
- The list follows the header live: a new entry never moves the focus, a gone focused one hands it to the next. After
  the last one: a celebrating graphic (its confetti falls once) and "Hier gibt es nichts zu sehen. Alles erledigt! Das
  hast du wunderbar gemacht!"; B closes.

## Toast levels

Every toast has a level like a status message (#433): **Info** for progress and confirmations ("… wird gestartet …",
installed, rescans, every "Demo-Spiel: …"), **Warning** when the action didn't happen and the player can retry or act
(another start running, no window found, not installable here, a launcher refused, a scan failed), **Error** for
failures ("… konnte nicht gestartet werden …", unexpected errors, a failed save or install, a file that won't open,
controllers not recognised). Pan's notices take the level of their source. The UI never says "Toast": a toast is a
"Meldung", a status message a "Status".

## Category "Über Arcadia"

- Group "VERSION": the version, the search for updates, the update, "Was ist neu".
- Group "LOGGING" (#433): "Letzte Meldungen" (the list in the info panel, RS scrolls), indented switches "Info",
  "Warnungen", "Fehler" (all on by default, saved; they only hide), "Liste leeren" (no question, greyed out while
  empty), "Arcadia-Log öffnen", "VIIPER-Log öffnen", "Pan-Log öffnen".
- The list: the last 50 toasts and status changes since Arcadia started, newest first, in memory only (arcadia.log
  keeps everything). A status message is an entry when it appears; when it goes, that entry gets the chip "Behoben hh:mm"
  in place, so a status entry without it is still pending. A level change adds an entry with the new level and marks
  the older one "Jetzt <Level>". Each entry: level symbol, time, chip "Meldung" or "Status", and "×N" for an unexpected
  error that came again N times without a toast of its own (#399); the count grows while the list is shown.
- A log opens in Windows' default app for `.log`; opened by controller, the mouse mode comes on for it (also with
  "Mausmodus: Aus"), without the on-screen keyboard, and goes off again once Arcadia is back in front if it came on for
  it. A log that doesn't exist is greyed out with the reason ("Pan ist nicht installiert", "Die Datei gibt es noch
  nicht"); its info panel shows the path. Failing to open is an error toast.

## Category "Controller"

Its rows (Pan, the mouse mode, Steam, SISR): [settings-controller.md](settings-controller.md).

## Unexpected errors

An unexpected error is a toast "Unerwarteter Fehler (<context>) – Details stehen in arcadia.log." and a log line with
stack trace, each time. The places that repeat an error every few seconds or every millisecond (SDL's controller
events per handler, the controllers for games per delegate, Pan's loops and events per context, #399) toast once per
streak: a source quiet for 10 minutes toasts its next error again; a steady error toasts once however long it lasts.
Its repeats reach arcadia.log at most once a minute per source, as one line with the latest message and the count since
the source's last line, written with that next line (no timer). Nothing of it is kept across a restart.
