# #166 Open and fix the status messages in a modal with Y

Library and detail page, the header's status chip and the modal "Status" (designs: artboards "#166" T166Library,
T166Modal, T166Empty, T166Bausteine). Controller only unless a case says otherwise; B goes back. The issue had no
test cases from the quality engineer before implementation: these are its scenarios, failure cases and acceptance
criteria one to one, for the quality engineer to check in the review. Acceptance tests use `ArcadiaDriver` (fake
Steam, Pan and releases, fake time).

**Status:** Automated: T166-1 to T166-7, T166-9 to T166-14, T166-17, the failing download of T166-8 · QE: T166-15 · Open for the owner: T166-16; T166-8's real update is an RC test

**Opening**

- [x] **T166-1** The header shows a warning (e.g. "HidHide fehlt"): Y in the library opens "Status" with the focus on the
      first entry; the same on the detail page; B closes it and leaves library or detail page as they were. · E2E: yes
- [x] **T166-2** No message, right after the start before the first check, or only "Controller wird eingerichtet …": Y
      does nothing, the header shows no chip and no hint. · E2E: yes
- [x] **T166-3** "Ansicht" or the context menu open: Y opens no modal (also not over settings or search). · E2E: yes

**Entries and order**

- [x] **T166-4** Each entry shows level symbol, title, the row's short line and "seit hh:mm" (when it appeared; a level
      change counts as new); the Arcadia update is its own entry; "Controller wird eingerichtet …" is none. · E2E: yes
- [x] **T166-10** An info from 10:00 and a warning from 10:05: "Dringlichkeit" and "Neueste zuerst" both show the warning
      first; a warning from 10:00 and an info from 10:05: "Neueste zuerst" shows the info first. L3 (C) switches, the
      footer names the order, the focus stays on its entry, the choice survives a restart. · E2E: yes

**Fixes and the way into the settings**

- [x] **T166-5** "HidHide fehlt", A on "Installieren": the modal closes, "Controller" opens on that row and "HidHide
      installieren?" appears; B there leaves the focus on the row; B again returns to the library. · E2E: yes
- [x] **T166-6** A fix already runs (row busy): "Installieren" only jumps to the row, nothing starts a second time.
      · E2E: yes
- [x] **T166-7** "Pan läuft nicht", "Zu den Einstellungen": the controller category opens on the row that says why; B
      returns to the library. · E2E: yes
- [ ] **T166-8** A newer Arcadia, "Jetzt aktualisieren": if the download fails, an error toast shows and the entry stays
      (E2E: yes); on the installed Arcadia it closes, installs and restarts. · E2E: no (real Velopack update) · RC
- [x] **T166-9** The update's "Zu den Einstellungen": "Über Arcadia" opens on "Jetzt aktualisieren"; B returns. · E2E: yes
- [x] **T166-17** The message goes while the jump to the settings runs: the jump lands on the category's top row, no
      question. · E2E: yes

**Live list and controls**

- [x] **T166-11** Modal open: a new message takes its place without moving the focus; the focused one goes, the focus
      moves to the next; the last one goes, the empty state with its graphic and "Hier gibt es nichts zu sehen. Alles
      erledigt! Das hast du wunderbar gemacht!" shows, only B is left and closes it. · E2E: yes
- [x] **T166-12** ▲ ▼ (W/S) entry, ◀ ▶ (A/D) button, A (Space) presses it, B (Esc) closes; mouse and keyboard work
      too (✕ top right, a click on a button presses it). · E2E: yes (window test)
- [x] **T166-13** The chip in the header: Y, △ or V matching the input beside the status symbols, only while the modal
      can open; a click on it opens the modal instead of the category. · E2E: yes (window test)
- [x] **T166-14** "Ansicht" is on L3 (C) in library and detail page; in the settings category "Launcher" L3 (C) lifts a
      row, ▲ ▼ move it, L3 drops it, B cancels; Y does nothing in the settings. · E2E: yes
- [x] **T166-15** Visual check of the design's checklist: focus ring on first, last and a middle entry and on both
      buttons; chip hint follows the input (Xbox, PlayStation △, keyboard V), hover like the search chip; top edge stays
      when entries go; more than five entries fade at the bottom; long title and short text end with "…", "seit hh:mm"
      and the buttons never cut; footer as designed; empty state centred, confetti falls once; 200 % scaling.
      · E2E: no (looking at the real window; QE's own run)
- [ ] **T166-16** **Owner:** A game or the mouse mode comes to the front while the modal is open: back in Arcadia it is open as it
      was. · E2E: no (Arcadia really going to the background on Windows)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T166-1 to T166-14, T166-17 | 2026-11-04 | Claude | PR branch, `dotnet test`, Linux container, headless | ✅ (automated) | – |
| T166-1 to T166-14, T166-17 (+ new test for "seit" on a level change, T166-4) | 2026-11-04 | QE (Claude) | PR branch, Release, `tools/ci/run-tests.sh 4`, Linux container, headless | ✅ (automated, all 3,500 tests green) | – |
| T166-15 | 2026-11-04 | QE (Claude) | PR branch, Release, headless window captures on Linux (fake Steam and Pan, DualSense and keyboard simulated, render scaling 2) | ✅ focus ring on first, middle, last entry and both buttons; chip Y/△/V and hover like the search chip; top edge stays (120 px with 2, 1, 0 entries); fade under the sixth entry; long title and short text end with "…", "seit" and buttons whole; footer; empty state centred with ✕ | Not seen: real Windows display scaling and the confetti's motion (only its end state) |
| T166-8 (update loading: progress in the entry, no second start) | 2026-11-04 | Claude | PR branch, `dotnet test`, Linux container, headless, download held by the fake feed | ❌ before the fix (the entry showed nothing while loading), ✅ after | Fixed in this PR (QE finding) |
