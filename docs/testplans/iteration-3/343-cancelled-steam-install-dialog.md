# #343 Steam: cancelled install dialog leaves the game on "Bitte in Steam bestätigen"

Derived from the issue's specification. Every case runs with the gamepad alone; B goes back as before. "Waiting"
means the tile and the detail page show "Bitte in … bestätigen" (Epic engine: "Bitte in … starten"). "1 minute"
allows one install poll on top (2 s). Automated cases use made-up Steam and Epic fixtures and the driver's clock.

**Status:** Automated: T343-1…T343-9 · QE: T343-1…T343-9 · Owner: T343-10…T343-14 · Open for the owner: none

- [x] **T343-1** Waiting ends after 1 minute: install a not-installed Steam game with A, the launcher reports
      nothing. At 59 s the game still waits; after 1 minute the tile, the detail page button and the button hint
      say "Installieren" again. · E2E: yes
- [x] **T343-2** The same for Epic: a game ("Bitte in Epic bestätigen") and an engine ("Bitte in Epic starten") each
      show "Installieren" again after 1 minute. · E2E: yes
- [x] **T343-3** A on a waiting game, in the library and on the detail page: the launcher's install is opened again
      for that game (no game start, no second waiting entry), the toast is the one of the first install, and the
      minute starts over (A at 50 s: still waiting at 1:40, "Installieren" after 1:50). B still goes back. · E2E: yes
- [x] **T343-4** Late confirm: after the waiting state ended, the launcher reports the installation (queued,
      downloading): the game shows its progress and, when the launcher reports it finished, is installed as usual.
      · E2E: yes
- [x] **T343-5** Quick confirm (regression): the launcher reports the installation within a few seconds: the game goes
      from waiting straight to progress, never shows "Installieren" in between. · E2E: yes
- [x] **T343-6** Two games waiting at once: each ends after its own minute; A on one restarts only that game's
      minute. · E2E: yes (unit test on `InstallTracker`)
- [x] **T343-7** Arcadia restarts while a game waits: after the restart the game shows "Installieren". · E2E: yes
- [x] **T343-8** A on a waiting game while the launcher can't be opened (start fails): a toast says why, Arcadia keeps
      running, the game keeps waiting until its first minute ends (it does not start over). · E2E: yes
- [x] **T343-9** A launcher without an install monitor (regression): no waiting state, the game stays pending until a
      rescan shows it installed, as before. · E2E: yes (unit test on `InstallTracker`)
- [x] **T343-10** **Owner:** Real Steam, cancelled dialog: install a not-installed game with A, cancel Steam's install dialog.
      Within about a minute the game shows "Installieren"; A then opens Steam's dialog again. · E2E: no (real Steam
      dialog)
- [x] **T343-11** **Owner:** Real Steam, A while the dialog is still open (assumption in the issue): Steam brings the same dialog
      to the front and opens no second one; the game keeps waiting and ends 1 minute after this A. If a second dialog
      opens, note it: the fallback is the old toast. · E2E: no (real Steam dialog)
- [x] **T343-12** **Owner:** Real Steam, late confirm: wait until the game shows "Installieren" again, then confirm the still open
      dialog in Steam: within a few seconds Arcadia shows the progress, the installation finishes and A starts the
      game. · E2E: no (real Steam installation)
- [x] **T343-13** **Owner (optional):** Real Steam, quick confirm (regression): confirm the dialog at once: "Bitte in Steam bestätigen" turns
      straight into the progress. · E2E: no (real Steam installation)
- [x] **T343-14** **Owner:** Real Epic: cancel Epic's install dialog: "Installieren" after about a minute; install again with A and
      confirm after the minute ended: the progress shows. · E2E: no (real Epic installation)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T343-1, T343-3, T343-4, T343-6 | 2026-11-03 | implementation (unit, `InstallTrackerTests`) | cloud container, Linux, Debug | passed | – |
| T343-3 (Epic engine, A again opens Epic once more) | 2026-11-03 | implementation (`EntryTypeTests`) | cloud container, Linux, Debug, headless | passed | – |
| T343-1…T343-8 (acceptance, `CancelledInstallDialogTests`) | 2026-11-03 | QE review | cloud container, Linux, Debug, headless | T343-3 on the detail page failed, the rest passed | detail page and game menu show the waiting game's button disabled, so A there does nothing: fix in this PR |
| red once: T343-1…T343-4, T343-6, T343-8 without the PR's `src/` changes; T343-5 with the progress of a just confirmed install held back one poll | 2026-11-03 | QE review | cloud container, Linux, Debug, headless | failed as expected | – |
| T343-7 (restart): regression, not made red: nothing keeps the waiting state across a restart to break | 2026-11-03 | QE review | – | – | – |
| T343-10…T343-14 | 2026-11-04 | owner | PR #452, real PC, real Steam and Epic | passed (ticked on the owner's word) | – |
| T343-1…T343-9 (all tests, `run-tests.sh`, after merging main) | 2026-11-04 | implementation | cloud container, Linux, Release, headless | passed | – |
