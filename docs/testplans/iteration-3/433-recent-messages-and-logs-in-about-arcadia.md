# #433 Show recent toasts and status messages and open the logs in "Über Arcadia"

Settings › "Über Arcadia", group "LOGGING" (design: artboard "#433" in the designs). "Entry" = a line of the list in
the info panel of "Letzte Meldungen"; "Meldung" = a toast, "Status" = a status message. Controller only unless a case
says otherwise; B goes back. Acceptance tests use `ArcadiaDriver` (fake time, fake opener, fake Steam and Pan).

**Layout and navigation**

- [x] **T433-1** Open Einstellungen › Über Arcadia: below "VERSION" the group "LOGGING" with, in this order, "Letzte
      Meldungen", "Info", "Warnungen", "Fehler" (the three indented), "Liste leeren", "Arcadia-Log öffnen",
      "VIIPER-Log öffnen", "Pan-Log öffnen". ▼ reaches every row from the version row, ▲ goes back; B leaves the
      settings page from each of these rows, a greyed-out row included. · E2E: yes
- [x] **T433-2** Footer per focused row: "Letzte Meldungen" RS "Blättern"; a switch ◀ ▶ "Ändern" and RS "Blättern";
      "Liste leeren" A "Leeren" (none while the list is empty); a log row A "Öffnen" (none while it is greyed out).
      The info panel shows the list on "Letzte Meldungen", the switches and "Liste leeren"; on a log row its title,
      what the log holds, the mouse-mode sentence and its path under "SPEICHERORT". · E2E: yes
- [x] **T433-3** Mouse (desktop mode or mouse mode): hovering a row focuses it, a click on a switch's arrow flips it, a
      click on "Öffnen" opens the log, the wheel scrolls the list. · E2E: yes (window test)
- [x] **T433-4** Visual check of the design's checklist: focus ring on the version row, on "Pan-Log öffnen" and on an
      indented row, not clipped; a greyed-out row shows the ring too; a long toast text wraps in the panel without
      cutting; 50 entries scroll with RS and the fade at the bottom; 200 % Windows scaling; only theme colours.
      · E2E: no (looking at the real window; QE's own run)

**The list**

- [x] **T433-5** Missed toast: a game start fails, its toast goes away. Open Über Arcadia, focus "Letzte Meldungen":
      the top entry has the red X, the time (hh:mm), the chip "Meldung" and exactly the toast's text. With three
      toasts the newest is on top. The row's subtitle says "3 seit dem Start von Arcadia". · E2E: yes
- [x] **T433-6** A toast appears while the list is on screen: it is on top at once and the subtitle counts it.
      · E2E: yes
- [x] **T433-7** Right after the start and after an Arcadia restart the list is empty ("Noch keine Meldungen seit dem
      Start von Arcadia.", subtitle "Noch keine Meldungen"); `arcadia.log` still holds the earlier toasts. A status
      message present at the start (Steam not running) is an entry as soon as it appears. · E2E: yes
- [x] **T433-8** More than 50: with 50 entries a new toast drops the oldest; the list never holds more than 50
      (boundaries 49/50/51 as unit tests). · E2E: yes
- [x] **T433-9** Toasts and status messages from other threads (Pan, SDL) at the same time reach the list on the UI
      thread, all of them, in the order they were shown; nothing throws. · E2E: yes (unit test with concurrent
      senders)

**Filter**

- [x] **T433-10** Entries of all three levels; ◀ on "Info" switches it "Aus": the info entries disappear at once, the
      subtitle says "<shown> von <all> angezeigt"; ◀ or ▶ again switches it back on (a switch flips with either
      arrow). The same for "Warnungen" and "Fehler". · E2E: yes
- [x] **T433-11** "Info" and "Fehler" off, Arcadia restarted: both are still off, "Warnungen" on. A fresh
      `settings.json` (and one written before #433) has all three on. · E2E: yes
- [x] **T433-12** All three off with entries in the list: the panel says "Alle Level sind ausgeblendet. Schalte Info,
      Warnungen oder Fehler an, um Meldungen zu sehen."; with an empty list it says "Noch keine Meldungen …" instead.
      · E2E: yes
- [x] **T433-13** The filter hides only: 50 entries, 10 of them info, "Info" off shows 40; a new error entry drops the
      oldest of the 50 (also if it was a hidden info entry); "Info" on again shows the remaining info entries.
      · E2E: yes
- [x] **T433-14** "Liste leeren" with entries: A empties the list without a question, the panel says "Noch keine
      Meldungen seit dem Start von Arcadia.", the switches keep their values, `arcadia.log` is unchanged. With the
      list empty the row is greyed out, still takes the focus, A does nothing. · E2E: yes

**Levels and status messages**

- [x] **T433-15** Every toast has a level as in the issue's table: one acceptance test per table row with a real flow
      (e.g. "… wird gestartet …" info, "Ein anderes Spiel wird gerade gestartet …" warning, "… konnte nicht gestartet
      werden …" error, an unexpected error through `AppErrors` error); every call site of `ToastViewModel.Show` passes
      a level (compiler or a unit test, no default). · E2E: yes
- [x] **T433-16** Pan's notices, also the overlay ones while a game is in front, are entries with the level listed in
      the PR. · E2E: yes
- [x] **T433-17** Steam closed while Arcadia runs: a status entry (yellow triangle, chip "Status") "Steam läuft nicht";
      Steam started again: that entry gets the chip "Behoben hh:mm", no new entry, and keeps its level. With only
      "Fehler" on, an error status shows with its "Behoben", the warning doesn't. · E2E: yes
- [x] **T433-18** A status message whose level changes (e.g. Pan's status from warning to error) adds one entry with
      the new level and marks the older one "Jetzt Fehler"; a health refresh that changes nothing adds none, also over minutes of fake time. · E2E: yes
- [x] **T433-19** Repeats (#399): a throttled source throws again 37 times within the quiet period: still one entry,
      its chip "×37" grows while the list is on screen; after the quiet period its next toast is a new entry with its
      own count. · E2E: yes (deferred to #399 if #433 merges first)
- [x] **T433-20** Wording: no UI text contains "Toast" (text scan as `ButtonGlyphTextTests`); the chips say "Meldung"
      and "Status"; keys in the new texts are glyphs. · E2E: yes

**Opening a log**

- [x] **T433-21** Big Picture, gamepad, "Mausmodus: Aus": A on "Arcadia-Log öffnen" opens `arcadia.log` in Windows'
      default app for `.log`, the mouse mode goes on, the on-screen keyboard stays closed; with Arcadia back in front it goes off again. With the mouse
      mode already on it stays on afterwards. The same for "VIIPER-Log öffnen" and, with Pan installed,
      "Pan-Log öffnen". · E2E: yes (fake opener)
- [x] **T433-22** On the owner's PC with only the Steam Controller: A on each of the three log rows opens the real file
      in the default app (usually Notepad); the stick moves the pointer, Arcadia comes back to the front by mouse, the
      mouse mode is off again. · E2E: no (real default app, real Pan and puck)
- [x] **T433-23** Pan never installed: "Pan-Log öffnen" is greyed out with "Pan ist nicht installiert", takes the focus,
      A does nothing, the panel still shows the path. `viiper.log` missing: "VIIPER-Log öffnen" greyed out with its
      reason (text listed in the PR). Pan installed while Arcadia runs: back in Über Arcadia the row is active.
      · E2E: yes
- [x] **T433-24** Windows can't open the file (no default app, access denied; fake opener throws): an error toast "…
      konnte nicht geöffnet werden …", which is an error entry in the list; Arcadia keeps running, the mouse mode
      doesn't stay on for it. · E2E: yes
- [x] **T433-25** Only the three fixed paths reach the process start: the opener takes a log kind, not a string;
      nothing from `settings.json`, a file or the UI reaches the path. · E2E: no (unit test and code review, no player
      flow)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T433-1, -8 to -14, -17, -18, -23, -25 (unit level) | 2026-11-03 | implementation | Branch, Linux cloud container, `dotnet test` (App, Core, Pan) | ✅ unit tests `RecentMessagesTests`, `LoggingRowsTests` green; whole suite green | acceptance tests follow in the QE review |
| T433-1, -2, -4 (partly), -23 | 2026-11-03 | implementation | Branch, Linux, Xvfb, `--demo`, keys | ✅ group laid out as designed; a demo start is an info entry with chip "Meldung"; greyed log rows keep the focus ring, no A in the footer | 200 % scaling, 50 entries and Windows' default app not looked at here |
| T433-1, -2, -5 to -18, -20, -21, -23, -24 (acceptance) | 2026-11-03 | QE | Branch, Linux cloud container, `RecentMessagesAndLogsTests` | ✅ 17 green, each red once against a deliberate break (toast not kept, status changes dropped, filter ignored or not saved, no controller event, no error toast, missing log active, row order, scroll hint, chip word, all-hidden text, overlay not kept, "Liste leeren" not greyed, no live refresh, Pan levels, "Behoben" level, capacity 51); ❌ T433-7: a toast is not in `arcadia.log` (test skipped) | finding to the implementation; T433-17 runs with "Wächterdienst Pan fehlt" and "Pan läuft nicht", Arcadia has no status "Steam läuft nicht" |
| T433-4 (partly), -23 | 2026-11-03 | QE | Branch, Linux, Xvfb, `--demo`, keys | ✅ list with three entries, chips, focus ring on "Liste leeren" and on the greyed "Pan-Log öffnen", no A in its footer | 200 % scaling, 50 entries with fade, T433-3 (mouse) not looked at |
| T433-19 (unit level) | 2026-11-03 | implementation | Branch after merging main with #399, Linux, `dotnet test` | ✅ `LoggingRowsTests.Repeats_of_a_throttled_error_grow_its_count_and_add_no_line` | ×N wired through `AppErrors.ReportRepeating`; acceptance test for the QE |
| T433-17, -18 (unit and acceptance) | 2026-11-03 | implementation | Branch, Linux, `dotnet test` | ✅ resolved status marked in its own entry ("Behoben hh:mm"), older entry "Jetzt Fehler"; HidHide/usbip restart pending is Info | owner's feedback on PR #437; QE acceptance tests adapted |
| T433-1 to -25 | 2026-11-03 | owner | Branch, owner's PC (Windows) | ✅ handed cases tested, released for merge | — |
