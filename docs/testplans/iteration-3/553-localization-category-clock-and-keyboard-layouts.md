# #553 Settings category "Lokalisierung" with clock and on-screen keyboard layouts

Derived from #14 (category, scenarios 10–16, the failure case for layout and clock) and the scope of #553; the group
SPRACHE is #554. Automated cases fake the system's keyboard layout, time format and region format, and the time;
nothing reads the real Windows settings. Under #517 there are no window tests per feature: cases marked
`E2E: no (window)` are checked in the quality engineer's run. The new category is one entry in `Guards/GuardPages.cs`
(`GuardPageListTests` fails without it). "The quick selection" are the layouts whose switch is on; "the layout menu" is
what R3 (G) opens on the on-screen keyboard.

Agreed with the owner (cases resting on one say so):
A1 Without a saved quick selection all five layouts are in it, as R3 offers all five today.
A2 A changed default leaves the previous default in the quick selection (its switch on, now changeable).
A3 A default taken from the system (scenario 13) is not written to `settings.json` until the player changes the row.
A4 The layout saved today by R3 (`KeyboardLayout`) becomes the saved default after the update.
A5 After a 12-hour time no "Uhr" follows ("um 9:58 PM"); after a 24-hour time it stays.
A6 A change of the Windows time or region format while Arcadia runs applies at the latest at Arcadia's next start.
A7 Region decides order and separators of dates and numbers; day and month names, units and the space before "%"
follow the UI language.
A8 R3 stays a menu; it lists the quick selection in the order of the settings rows, the layout in use marked.

**Status:** Automated: T553-1, T553-3 to T553-18, T553-23 · QE: T553-2, T553-19 · Open for the owner: T553-20, T553-21 (T553-22 · RC on the release candidate)

- [x] **T553-1** Settings, controller only: LB/RB reach the tab "Lokalisierung" between "Datenquellen" and "Über Arcadia";
  it opens on its first row, scrolled to the top. Groups BILDSCHIRMTASTATUR ("Standard-Layout" with ◀ ▶; indented one
  switch per layout: DE QWERTZ, EN-US, EN-UK, FR AZERTY, ES, in this order) and UHRZEIT ("Uhrzeit"); no group SPRACHE.
  "Standard-Layout" and "Uhrzeit" show one value box per value in the info panel, the current one highlighted; the
  footer names the focused row's buttons; B leaves the settings to where they were opened. · E2E: yes
- [x] **T553-2** Look at 1920 × 1080: indents, the greyed default switch with the blue note "Das Standard-Layout ist
  immer dabei" at the top of the info panel, fades and focus ring (#237), no text cut off, nothing over the edge. ·
  E2E: no (window)
- [x] **T553-3** "Standard-Layout": ◀ ▶ step through the five in the order above and wrap (ES ▶ → DE QWERTZ, DE QWERTZ ◀
  → ES); saved in `settings.json` at once, still there after a restart of Arcadia. A click on an arrow does the same. ·
  E2E: yes
- [x] **T553-4** The default's switch: on and greyed out, still takes the focus; ◀ ▶, A and a click change nothing.
  Making a layout whose switch was off the default turns its switch on and greys it; the previous default's switch
  becomes changeable and stays on *(A2)*. · E2E: yes
- [x] **T553-5** The other switches: ◀ ▶ (either arrow flips) and a click turn a layout in or out of the quick selection,
  saved at once, kept after a restart. · E2E: no (variant: view model test)
- [x] **T553-6** Scenario 10: default FR AZERTY → the keyboard opens in FR AZERTY ("Layout: FR") wherever it shows: the
  search, every settings text field (SteamGridDB key, IGDB access, "Titel ändern", "Eigene Tags", the artwork search
  term) and the mouse mode's keyboard over another window. · E2E: yes (one acceptance test per place; the mouse mode's
  keyboard as view model test)
- [x] **T553-7** Scenario 11: keyboard open in FR AZERTY, R3 → EN-US: the keys are EN-US at once, the typed text stays;
  close the keyboard (B, or close the search or field; in the search also B then X) and open it again → FR AZERTY; `settings.json` and the row
  "Standard-Layout" still say FR AZERTY. Same in a settings text field and in the mouse mode. · E2E: yes
- [x] **T553-8** Scenario 12: the layout menu offers only the quick selection *(A8)*; a layout switched out is missing at
  the next opening. With only the default in it, "Layout" (R3) in the keyboard's right block and in the footer is greyed
  out and R3 (G, a click on the key) does nothing; switching a second layout in makes it active again. · E2E: yes
- [x] **T553-9** R3 changes no layout while no keyboard is open (search with the keyboard put away, library, settings): no
  menu, no change, R3's other meaning there (the library's filter) unchanged. · E2E: no (variant: view model test)
- [x] **T553-10** Scenario 13, no saved default: the system's layout decides: German (also Austrian, same layout) → DE
  QWERTZ, US → EN-US, UK → EN-UK, French → FR AZERTY, Spanish → ES; any other (Swiss German, US Dvorak, Italian, none
  readable) → the one matching the UI language (German → DE QWERTZ). The row shows it; nothing is written *(A3)*. ·
  E2E: no (unit test with a fake system layout)
- [x] **T553-11** Settings from before the update: a saved `KeyboardLayout` is the default *(A4)*; no saved quick selection
  means all five *(A1)*. · E2E: no (unit test)
- [x] **T553-12** Scenario 14, fake time 21:58: "Uhrzeit" ◀ ▶ steps "Wie im System" (default) · "24 Stunden" ·
  "12 Stunden" (examples 21:58 / 9:58 PM in the value box) and wraps, saved at once. Without a restart, with the screens open, every time shown follows:
  header clock, "seit hh:mm" in the status modal, the times and "Behoben hh:mm" in "Letzte Meldungen", "Zuletzt
  gespielt" and other date-times on the detail page, "Zuletzt geprüft um …" in "Datenquellen", "Zuletzt gesucht: …" in
  "Über Arcadia", the launcher rows' "um … Uhr". · E2E: yes (one acceptance test walking these places)
- [x] **T553-13** 12 hours: 00:05 → "12:05 AM", 09:05 → "9:05 AM", 12:00 → "12:00 PM", 23:59 → "11:59 PM", with AM/PM
  also where the region has no AM/PM of its own (German); 24 hours: 09:05 → "09:05", 00:05 → "00:05". "Uhr" after a
  time only in 24 hours *(A5)*. · E2E: no (unit test)
- [x] **T553-14** No display text keeps a fixed format or culture: no `de-DE` in `Formatting`, no `HH:mm`/`dd.MM` in
  `*Texts.resx` and views (logs stay invariant). · E2E: no (unit test over the resource files and `Formatting`)
- [x] **T553-15** Scenario 15, "Wie im System": a fake system with 24 hours shows "21:58", one with 12 hours "9:58 PM",
  whatever the UI language; a system change while Arcadia runs is shown at the latest after a restart *(A6)*. · E2E: no
  (unit test with a fake system format)
- [x] **T553-16** Scenario 16: with the UI in German and the region English (US), dates and numbers follow the region
  (release date "11/13/2026", size "12.5 GB", "1,234" games), with region German "13.11.2026", "12,5 GB", "1.234"; texts,
  day and month names stay German *(A7)*. Also UK and French regions as variants. · E2E: yes (one acceptance test with
  region English (US); the regions as unit tests per formatter)
- [x] **T553-17** Failure case: `settings.json` with an unknown or unreadable layout ("XX", empty, a number), a quick
  selection with unknown ids, duplicates or not a list, or an unknown clock value → the default (system layout per
  T553-10, the known layouts of the list or all five, "Wie im System"), one log line each, no toast, no crash; the other
  settings are kept; the next change writes valid values. · E2E: no (unit test)
- [x] **T553-18** Speed: opening the keyboard and a ◀ ▶ on the rows stay within 50 ms; the header clock reads no system
  setting per tick (idle CPU as before). · E2E: no (unit test that the tick reads cached values; timing by hand)
- [x] **T553-19** Mouse and keyboard: in the desktop mode a click on ◀ ▶, on a switch and on the keyboard's "Layout" key
  works as by controller; W/S, A/D, G (R3) and Esc (B) on a real keyboard as well. · E2E: no (window)
- [ ] **T553-20** **Owner:** on your PC, with the Windows region and time format as they are: the clock and dates look
  like the rest of Windows. Then set Windows to time format 12 hours and region English (US), restart Arcadia: header
  clock with AM/PM, dates US style, texts German. Set Windows back. · E2E: no (real Windows settings)
- [ ] **T553-21** **Owner:** with the Steam Controller, in the search and in the mouse mode over another window: the
  keyboard opens in the default layout, R3 offers only the quick selection, a switched layout holds until the keyboard
  closes and the next opening shows the default again. · E2E: no (real controller, real window in front)
- [ ] **T553-22** After updating from the last release: the layout last chosen with R3 is the default, all five layouts
  are in the quick selection, the clock is "Wie im System" *(A1, A4)*. · E2E: no (installed update) · RC
- [x] **T553-23** Owner's finding: an R3 switch keeps the keys that stand where they stood (only what they type
  changes) and shows within a frame, not after half a second; the info of "Standard-Layout" shows [R3] as the
  controller's button also while the keyboard is in use (Xbox without a controller). · E2E: no (window guard, no
  rendering for the glyph)

Automated tests:

| Test cases | Tests |
|---|---|
| T553-1, T553-3 to T553-10, T553-12 to T553-16, T553-18 | `Arcadia.App.Tests.LocalizationSettingsTests` |
| T553-4, T553-10, T553-11, T553-17 | `Arcadia.Core.Tests.LocalizationSettingsTests` |
| T553-23 | `Arcadia.App.Tests.Guards.SearchSpeedTests` (window guard, `main` only), `LocalizationSettingsTests` (info) |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| Owner's PC check (Run-Check-14.ps1) | 2026-11-13 | Owner | Windows, before implementation | Region de-DE, time "HH:mm", keyboard 04070407 read as expected | Arcadia reads `CultureInfo.CurrentCulture` and `GetKeyboardLayout(0)` |
| T553-1, T553-3 to T553-18 | 2026-11-13 | Implementer | Debug, Linux cloud container, `dotnet test` | Passed | – |
| Look at the category, the greyed default switch, 12 hours in the header | 2026-11-13 | Implementer | Release, Linux, Xvfb 1920 × 1080, `--demo`, keys | Rows, indents, blue note, footer without actions on the default switch, "7:54 PM" | – |
| T553-2, T553-19 (QE run) | 2026-11-13 | QE | Release, Linux, Xvfb 1920 × 1080, `--demo`, keys and mouse | Failed: value boxes with the [R3] glyph cut off, "Español · QWERTY con Ñ" shortened in the pill, "12 Stunden (9:58 PM)" touching the arrows; in the search B then X kept the R3 layout (T553-7); a quick selection of the wrong type left only the default (T553-17) | Fixed in 5075b3c (texts without glyph, shorter pill values, keyboard reopens in the default, unreadable list = all five) |
| T553-2, T553-19 (QE rerun) | 2026-11-13 | QE | Release, Linux, Xvfb 1920 × 1080, `--demo`, keys and mouse | Passed: nothing cut off, focus ring and greyed switch fine; clicks on ◀ ▶, switches and the "Layout" key, W/S/A/D, Esc work; G in the search types (a real keyboard types there) | – |
| T553-23 (owner's finding: switch lagged) | 2026-11-13 | Implementer | Release, Linux, headless window, Stopwatch around R3 → accept | Before: 700–1,400 ms, 0 of 63 key controls kept; after: 6–100 ms, 59 of 63 kept | Keys reused across layouts |
