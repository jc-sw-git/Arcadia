# #500 Detail page: quick selection in "Übersicht" and "Übersicht anpassen"

Derived from the specification in #498 (scenarios 12–19, the failure cases that apply to slice 2, the quick selection
and dialog points of the visual checklist), the decisions in #500 (storage, focus in "Übersicht", the dialog's button,
no context menu entry, the limit of 5) and the approved artboard "Detailseite neu: Mischung aus A, B und C". Every case
runs with the gamepad alone; keys follow `docs/product/controls.md` (WASD, Space = A, Esc = B, Q/E = LT/RT, Shift+Q/E =
LB/RB). "The entries" are the quick selection on the left of "Übersicht"; "the dialog" is "Übersicht anpassen"; "the
question" is "Schnellauswahl aller Spiele ersetzen?". Today's entries are "Steam-Controller-Modus", "Individueller
Mausmodus" ("Controller"), "Artwork ändern" and "Deinstallieren …" ("Verwalten"); cases on the limit of 5 add test
entries (decision in #500). Automated cases use the demo or made-up Steam and Epic fixtures (`ArcadiaDriver`), window
cases `ArcadiaWindow`. Replaces T499-7 (A on "Übersicht" did nothing) and the "facts in full width" part of T499-1.

**Status:** Automated: T500-1 to -23 · QE: T500-24 to -28 · Open for the owner: none (T500-29, T500-30 passed, 2026-11-13)

Quick selection in "Übersicht"

- [x] **T500-1** Open a game whose selection was never changed: "Übersicht" shows on the left under "Schnellauswahl"
      the four default entries in the order of the areas, each with its value (◀ value ▶ on value entries), set apart
      below them "Übersicht anpassen", and on the right the game's facts. · E2E: yes
- [x] **T500-2** Focus on "Übersicht" in the rail, A: the focus is on the first entry; ▲ ▼ walk the entries and
      "Übersicht anpassen" and go round at the ends (a held ▲ ▼ stops at the end, T476-9); the facts never take the
      focus; ◀ ▶ on "Übersicht anpassen" do nothing; B returns to "Übersicht" in the rail. · E2E: yes
- [x] **T500-3** Focus on the entry "Steam-Controller-Modus": ▶ changes the value at once to the next, ◀ to the previous,
      going round at the ends; the entry, the rail's line for "Controller" and the row in "Controller" show the new value
      and it holds for this game after leaving the page and after a restart of Arcadia, as T499-8. The same for
      "Individueller Mausmodus". A held ◀ ▶ stops at the end of the range. · E2E: yes
- [x] **T500-4** A on the entry "Artwork ändern" opens the artwork dialog; after B the focus is on that entry and
      "Übersicht" is open. A on "Deinstallieren …" asks the same question as its row; "Abbrechen" or B returns to the
      entry, confirming hands over to the launcher as today. · E2E: yes
- [x] **T500-5** A greyed entry (scenario 19): a game that isn't installed shows "Deinstallieren …" and the controller
      entries greyed, still in the quick selection, with the same reason as their rows (T499-15), shown below the entries
      while the focus is on it and in its tooltip; without a Steam Controller "Steam-Controller-Modus" is greyed. A greyed
      entry takes the focus as its row does (T499-14), ◀ ▶ and A do nothing. · E2E: yes
- [x] **T500-6** State changes while "Übersicht" is open with the focus on an entry: an installation finishes, the game
      starts or ends, a Steam Controller connects or disconnects: the entries grey or become active in place, values
      follow, the focus stays on its entry. · E2E: yes
- [x] **T500-7** Button hints follow the focus: on a value entry ◀ ▶ "Ändern", on an action entry and on "Übersicht
      anpassen" A names it, B "Bereiche" (as in an area's rows, T499-10); none for a greyed entry's ◀ ▶ or A. · E2E: yes
- [x] **T500-8** Carousel with the focus on the second entry, RT: the next game shows its own quick selection,
      "Übersicht" stays open and the focus stays on the second entry; on a game with fewer entries it goes to the last
      entry, on a game with an empty selection to "Übersicht anpassen". LT the other way round; held RT keeps the
      entries of the first game until release (T499-19). · E2E: yes
- [x] **T500-9** LB/RB, L3, X/B and Y keep "Übersicht" open and the focus on its entry, as T499-12; opening the page
      anew starts on "Spielen" with "Übersicht" open. · E2E: yes
- [x] **T500-10** The library's context menu offers no "Übersicht anpassen" and otherwise the same actions as before
      (decision in #500). · E2E: yes

Dialog "Übersicht anpassen"

- [x] **T500-11** A on "Übersicht anpassen": the dialog opens with the game's title in its subline, every entry of the
      other areas grouped by area ("Controller", "Verwalten"), the ticked ones ticked, the counter "n von 5", below the
      list the note that every game has its own selection and the button "Für alle Spiele übernehmen". The focus is on
      the first entry. · E2E: yes
- [x] **T500-12** In the dialog ▲ ▼ walk all entries across the groups and "Für alle Spiele übernehmen" (reached with ▼
      after the last entry) and go round at the ends; a held ▲ ▼ stops at the end. B closes the dialog and the focus is
      on "Übersicht anpassen". · E2E: yes
- [x] **T500-13** A on an entry ticks or unticks it; the counter changes at once and the page behind shows the change at
      once, entries in the order of the areas whatever the order of ticking. Other games keep their selection.
      After B, leaving the page and a restart of Arcadia the game shows what was ticked. · E2E: yes
- [x] **T500-14** Limit of 5 (test entries): with 5 ticked the counter says "5 von 5", every unticked entry is greyed, the
      note "5 von 5 gewählt. Wähle zuerst einen Eintrag ab, um einen anderen aufzunehmen." shows, and ▲ ▼ pass over the
      greyed entries; unticking one makes the others active again and hides the note. A sixth can't be stored by any
      path. · E2E: yes
- [x] **T500-15** Empty selection: untick every entry: the counter says "0 von 5", after B the left half shows only
      "Übersicht anpassen"; A in the rail puts the focus on it; it holds after a restart. · E2E: yes
- [x] **T500-16** A game state changes while the dialog is open (installation finishes, controller connects): the page
      behind updates in place, the dialog and its focus stay. LT/RT and LB/RB do nothing while the dialog or the question
      is open, the page behind keeps its game. · E2E: yes

For all games

- [x] **T500-17** A on "Für alle Spiele übernehmen" asks "Schnellauswahl aller Spiele ersetzen?" with the note that other
      games' own selections are lost, buttons "Abbrechen" and "Ersetzen". "Abbrechen" or B: back in the dialog on the
      button, nothing changed for any game. · E2E: yes
- [x] **T500-18** "Ersetzen": every game shows this selection, including games that had their own and games of other
      launcher tabs; "Für alle Spiele übernommen." confirms it; the dialog stays open. · E2E: yes
- [x] **T500-19** After "Ersetzen" a game added later (rescan finds a new Steam or Epic game, fixture) shows the selection
      for all games, also after a restart. · E2E: yes
- [x] **T500-20** After "Ersetzen" change one game's selection: only that game differs; the others keep the selection for
      all games, also after a restart. · E2E: yes

Storage and failure cases

- [x] **T500-21** A new default entry (test: the default list gains a test entry between two starts): a game never changed
      shows it when nothing was applied to all games; a game with its own selection and, after "Ersetzen", every game keep
      theirs without it. · E2E: yes
- [x] **T500-22** `settings.json` with an unreadable quick selection (wrong type, broken value): every game shows the
      defaults, no toast or message, `arcadia.log` holds one warning; the rest of the settings still load. A stored
      unknown entry id is dropped silently, the known ones stay. · E2E: yes
- [x] **T500-23** Storing "Ersetzen" fails or Arcadia ends in the middle of it (fault injected at the write): after the
      next start either the old selections or the new one for all games hold, never a mix, and `settings.json` is
      readable. The per-game selections are removed in the same save that stores the one for all games. · E2E: yes
- [x] **T500-24** Mouse: a click on an entry focuses it, the arrows beside a value change it, a click on "Übersicht
      anpassen" opens the dialog, a click on an entry in the dialog ticks it, on "Für alle Spiele übernehmen" asks; the
      dialog and the question show ✕. · E2E: no (window; #517: no new window tests, QE run with screenshot)

Look (QE run of the app, visual checklist of #498)

- [x] **T500-25** Focus ring fully visible, not clipped, on the first and last entry, "Übersicht anpassen", the first and
      last checkbox row of the dialog, "Für alle Spiele übernehmen", "Abbrechen" and "Ersetzen"; exactly one ring at any
      time, none behind the dialog or the question. · E2E: no (window; #517: no new window tests, QE run with screenshot)
- [x] **T500-26** Long values and long entry names in the quick selection and in the dialog end with "…" in one line; the
      left half keeps its width whatever the facts on the right show; with 5 entries nothing overlaps the facts or the
      footer hints. · E2E: no (window; #517: no new window tests, QE run with screenshot)
- [x] **T500-27** Margins and the gap before "Übersicht anpassen" match the artboard at 1920×1080; at 1280×720 nothing is
      cut; greyed entries and greyed checkbox rows are visibly greyed but readable; no colour, brush or shadow value
      outside `Theme/Theme.axaml`. · E2E: no (comparison with the artboard; the theme part is checked on the diff)
- [x] **T500-28** Budgets, measured mechanically: gamepad input to visible focus move < 50 ms for ▲ ▼ in the entries and in
      the dialog and for a tick; start to usable library < 2 s with 1,000 games and < 400 MB RAM with 5,000 games, also
      with a stored selection per game for all 5,000. · E2E: no (timing on the real machine; T500-30 confirms)

For the owner

- [x] **T500-29** **Owner:** On the real PC with the Steam Controller: in a real Steam game change "Steam-Controller-Modus"
      in the quick selection and start the game (the mode holds); untick "Artwork ändern", apply to all games, open two
      other games and a game of another launcher (they show it), give one game its own selection, restart Arcadia: all
      as left. · E2E: no (real controller, real library and settings)
- [x] **T500-30** **Owner:** Release build on the real PC: walking the entries and the dialog and ticking feel instant
      (< 50 ms). · E2E: no (budget on the owner's PC)

Decided with the implementation: a greyed entry shows its reason below the entries while it has the focus, and in its
tooltip, since "Übersicht" has no info box (T500-5); after "Ersetzen" the dialog stays open, "Für alle Spiele
übernommen." stands beside the button and the focus is on it (T500-18).

Automated tests:

| Test cases | Tests |
|---|---|
| T500-1 to -22 | `QuickSelectionTests` (view models through `ArcadiaDriver`) |
| T500-1, -13 to -15, -18 to -23 | `QuickSelectionSettingsTests` (Core: storage, limit, unreadable file, one save) |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T500-1 to -23 | 2026-11-12 | Claude | PR branch, Debug, Linux cloud container; demo and made-up GOG, Steam and Epic libraries, fake Steam Controller | ✅ `QuickSelectionTests`, `QuickSelectionSettingsTests`; `DetailAreasTests`, `DetailPageStateTests` adapted (A in the rail on "Übersicht", overview rows not counted twice) | – |
| All new tests in `QuickSelectionTests`, `QuickSelectionSettingsTests` | 2026-11-12 | Claude | PR branch, Debug, Linux cloud container, deliberately broken builds | ✅ each red once: no entries shown (T500-1 to -9, -13, -16, -21), "Übersicht anpassen" not reachable by ▲ ▼ (T500-2, -11, -12, -15, -17), A in the rail ignored on an empty selection (T500-15), the dialog getting no keys (T500-11 to -13, -16, -17), unticked entries never greyed or not passed over at 5 (T500-14), "Ersetzen" storing nothing (T500-18 to -20), the focus on "Übersicht anpassen" lost on a turn (T500-8), the entry not following its row's value (T500-3), the context menu gaining the entry (T500-10), no defaults (T500-1, -21), per-game selections ignored (T500-13, -20, -21), the converter failing the file (T500-22), a `null` selection not repaired (T500-22) | – |
| T500-1, -2, -5, -7, -11, -12, -17 (look) | 2026-11-12 | Claude | PR branch, Debug, Xvfb 1920 × 1080, `--demo`, keys by xdotool | ✅ screenshots: entries with values and the greyed entries' reason, rings whole on the first and last entry, "Übersicht anpassen", the first and last checkbox row and "Abbrechen"; one ring at a time, none behind the dialog | software rendering only; mouse (T500-24) and the full look checklist left to the QE run |
| T500-6, -25 (QE findings) | 2026-11-12 | Claude | PR branch, Debug, Linux cloud container; Xvfb 1920 × 1080, `--demo` | ✅ `QuickSelectionTests`: an installation finishing activates "Deinstallieren …" in place with the focus on it (red with the focus place not restored); `IsDialogOpen` reported on opening and closing a dialog (red without the notifications); screenshot: no ring behind "Übersicht anpassen" under the dialog (pixel 33 → 10) | T500-9's L3, X and Y stay with the QE run, as T499-12 |
| T500-9 (L3, X, Y), -24 to -28 (QE run) | 2026-11-12 | Claude (QE) | PR branch at ce59576, Release, Xvfb 1920 × 1080 and 1280 × 720, `--demo`, keys by xdotool, mouse by xdotool | ✅ T500-9: L3 ("Ansicht"), X (search) and Y leave "Übersicht" open and the focus on its entry after closing; T500-24: a click ticks an entry in the dialog, ✕ closes it with the focus on "Übersicht anpassen", a click on it opens the dialog, a click on "Für alle Spiele übernehmen" asks; T500-25: rings whole on the entries, "Übersicht anpassen", the checkbox rows, the button, "Abbrechen"; no ring behind the dialog (pixel 31 → 3 after ce59576); T500-26/-27: "Standard (Arcadia)" in one line beside its name at 480 px, nothing cut at 1280 × 720, greyed entries readable, gaps as the artboard RD6Mischung (34, 8, 26 before "Übersicht anpassen"; dialog gap 26, columns 28), no colour outside `Theme.axaml`; T500-28: ▲ ▼ and A in the entries and the dialog 0.6–25 ms press to frame, "Ersetzen" 20 ms; start with 1,031 / 5,031 demo games 2.1 / 2.6 s and 290 / 304 MB, also with 5,394 per-game selections, the same as without (2.3 s): the container's software rendering, not the selection | the arrows beside a value only mechanically (demo has no Pan, so both value entries are greyed); the 2 s start budget can only be judged on the real machine, since the container misses it without the change too |
| T500-29, -30 | 2026-11-13 | Owner | Release build, real PC, Steam Controller | ✅ | – |
