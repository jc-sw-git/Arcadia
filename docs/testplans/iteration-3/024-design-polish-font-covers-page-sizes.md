# #24 Design polish: Figtree font, 2:3 covers, page sizes that fill the grid

Library, search, detail page, on-screen keyboard and every view's text (design: artboard "T24Feinschliff" and the
updated Main, Search, GameDetail boards). Controller only unless a case says otherwise; B goes back. "Ansicht" is on L3
(key C). Acceptance tests use `ArcadiaDriver` (demo or fixture libraries with made-up games), window tests
`ArcadiaWindow` at 1920 × 1080.

**Status:** Automated: T24-1 to T24-4, T24-6 to T24-13 · QE: T24-10, T24-14 (its finding is Review finding 2) · Open for the owner: T24-5, T24-15, T24-16

**Page sizes**

- [x] **T24-1** A tab with 47 games, L3 opens "Ansicht": "Einträge pro Seite" lists 24, 64, 128 in this order; 24 and
      64 can be chosen, 128 is greyed out and can't be chosen (A does nothing on it). Boundaries as unit tests of
      `PageSizes`: 24 games → only 24; 25 → 24, 64; 64 → 24, 64; 65 → all three. · E2E: yes
- [x] **T24-2** Fresh settings, a tab with 30 games: page 1 shows 24 tiles in 3 full rows of 8, all visible at
      1920 × 1080 without scrolling (no tile cut at the bottom edge); page 2 shows the other 6. · E2E: yes (view model
      for the count, window test for "no scrolling")
- [x] **T24-3** `settings.json` with a preferred page size of 25 (variants as unit tests: 10, 50, 100, and the invalid
      0, -1, 7, 1000, a string): after the start the library pages by 24, "Ansicht" marks 24, and `settings.json` holds
      24 without the player changing anything. · E2E: yes
- [x] **T24-4** Choose 64 in "Ansicht", restart: 64 is still marked and used. With 128 preferred, a tab with 30 games
      shows all 30 on one page (64 effective), a tab with 200 games 128; the saved preference stays 128. · E2E: yes
- [ ] **T24-5** **Owner:** 1,000 demo games, 128 per page, Release build: start to usable library < 2 s, focus move between tiles
      < 50 ms (`Bild:` lines in `arcadia.log`), paging forward and back binds the covers without a visible stall
      (`LibrarySpeedTests` extended to 128). · E2E: yes

**Covers 2 : 3**

- [x] **T24-6** Window test: a library tile's cover is 146 × 219, a search hit 190 × 285, the detail page cover
      210 × 315 (each width : height = 2 : 3, ±1 px); the grid still has 8 columns and the gaps of the design. · E2E: yes
- [x] **T24-7** A game without artwork (monogram placeholder) and one with a 60-character title, in library, search and
      detail page: monogram top left, title at the bottom, nothing clipped or overlapping, as in the design. · E2E: yes
      (window test)

**Font**

- [x] **T24-8** Window test over every view and window: library, detail page, search, every settings category,
      "Ansicht", context menu, status modal, driver install dialog, first run, on-screen keyboard, mouse-mode overlay
      and its keyboard window, a toast: every text is set in Figtree (resolved typeface), none in Inter or a system font
      except the symbols of T24-11. · E2E: yes
- [x] **T24-9** No empty box from Arcadia's own texts: an automated check runs every string the App can show (views,
      view models, settings rows and info texts, every controller printing of `PadGlyph`, every on-screen keyboard
      layout) against Figtree and Inter and fails on a character neither has and that isn't drawn as an icon. Red once
      with a test string holding such a character. · E2E: yes
- [x] **T24-10** `THIRD-PARTY-NOTICES.md` names Figtree and Inter with copyright and SIL OFL 1.1; both fonts are
      embedded resources, so Arcadia shows Figtree on a system where it isn't installed (QE run on Linux without the
      font). · E2E: no (document check and QE run)

**Symbols**

- [x] **T24-11** Xbox, generic and Steam Controller printing: ⧉ and ≡ are the theme icons (`PadIconView`,
      `PadIconOptions`) in every place: footers, "Optionen" on the preview, the chip "Suchen", inline in texts
      (`ButtonTextBlock`, e.g. "Mit ⧉ zurück zu Arcadia", mouse-mode legend "⧉ + ≡ halten"); no rendered text holds the
      characters ⧉ ≡ ↹; the icon is as high as a capital letter (±1 px) and has the colour of the text beside it, dark on
      the blue key. · E2E: yes (window test)
- [x] **T24-12** PlayStation (PS5, PS4, PS3) and Nintendo printings look as after #215 (Create/Options icons,
      "Share"/"Options", "Select"/"Start", − +); after a key press the hints show keys (R, Enter), not icons. · E2E: yes
- [x] **T24-13** On-screen keyboard: "Tab" shows the icon `KeyIconTab` beside its name, "Andocken" the ⧉ icon, Enter
      the ≡ icon; ⌫ ↵ ⇧ ␣ stay characters and look as before (Inter fallback, compared with a screenshot of `main`);
      on the focused key the icon follows the text colour. · E2E: yes (window test)
- [x] **T24-14** Screenshot of every view (`tools/dev/Screenshot-Arcadia.ps1` or the Linux run) compared with
      `docs/designs/*.png` and the board "T24Feinschliff"; every difference outside this issue is filed and listed to
      the owner. · E2E: no (visual comparison by the quality engineer)

**Owner**

- [ ] **T24-15** **Owner:** Windows, Release build, real library: library, detail page, search, settings, on-screen
      keyboard and the mouse-mode legend show Figtree, sharp at the PC's resolution and scaling; no empty box anywhere,
      also not in real game titles (umlauts, ™, ®, titles in other scripts if any). · E2E: no (real Windows font
      rendering, real library)
- [ ] **T24-16** **Owner:** with the Steam Controller: footers, preview, search chip and the on-screen keyboard show ⧉
      and ≡ as the new icons, as high as the capitals beside them; after a key press keys show, after a button press
      the icons come back. · E2E: no (real Steam Controller)

Tests: T24-1 `PagingTests`, `LibraryTests.View_options_change_the_page_size…`; T24-2 `GridFadeTests.A_page_that_fits…`,
`LibraryTests.LT_and_RT_turn_the_pages…`; T24-3 `SettingsServiceTests.A_page_size_that_no_longer_exists…`,
`…written_as_text…`, `LibraryTests.A_page_size_of_an_older_version…`; T24-4 `PagingTests.Effective_size…`,
`LibraryTests.Sort_page_size_and_tab_are_the_same_after_a_restart…`, `…Sort_and_page_size_apply_to_every_tab…`;
T24-5 `LibrarySpeedTests.A_page_of_128_turns_with_the_same_covers`; T24-6, T24-7, T24-11, T24-13 `CoverAndIconTests`;
T24-8 to T24-11 `EmbeddedFontTests`; T24-12 `ButtonSymbolSetsTests`, `ButtonSymbolsTests` (#215, unchanged).

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T24-1 to T24-4, T24-6 to T24-13 | 2026-11-04 | Claude | branch of this PR, Linux container, headless window tests, demo and fixture data; T24-8 looks at library, "Ansicht", context menu, detail page, search with keyboard, settings and the mouse mode's keyboard window, the other views take the same theme font | ✅ | – |
| T24-3, T24-6, T24-8, T24-11, T24-13 (red once) | 2026-11-04 | Claude | same, with the fix undone on purpose (save at start removed, tile without `VerticalAlignment`, Xbox ⧉ ≡ as text, Tab icon hidden, theme font Inter, hit 240 high) | ❌ as intended | – |
| T24-9 (red once) | 2026-11-04 | Claude | same, a string "Spiel ⧓" added to the App | ❌ as intended | – |
| T24-5 | 2026-11-04 | Claude | Release, Linux container (Xvfb), `--demo --demo-games 1000`, 128 per page | start 2.5–3.3 s to the library, `main` with 100 per page 2.8–3.0 s in the same container; 247–249 MB; focus move not measured (keys don't reach the window under Xvfb here) | budget only on the owner's PC; no regression against `main` |
| T24-10 | 2026-11-04 | Claude | Release, Linux container without Figtree installed | ✅ the library shows Figtree (embedded) | – |
| T24-1 to T24-13 (QE review) | 2026-11-04 | QE (Claude) | branch of this PR at 1e99209, Linux container: Release build without warnings, `tools/ci/run-tests.sh 4` | ❌ 2 of 607 App tests: T24-9 and the T24-11 source scan don't find `Arcadia.slnx` when run from the shard's copy of the build, as CI runs them; everything else ✅ | fix in this PR (Review finding 1) |
| T24-14 | 2026-11-04 | QE (Claude) | same, real app under Xvfb (library) and headless frames (search row and grid, detail page, mouse-mode keyboard), compared with the boards T24Feinschliff, Main and Search | ❌ search grid: launcher names under the lower row of hits lie under the bottom fade, no row peeks out above or below any more; library, detail page, keyboard icons as designed. `docs/designs/*.png` predate the boards (25 tiles, "Ansicht" on Y): no new difference outside this issue | fix in this PR (Review finding 2) |
| T24-1 to T24-13 | 2026-11-04 | Claude | after Review findings 1 to 3: Linux container, Release build, `tools/ci/run-tests.sh 4` (shards from copies, as CI); search grid test now also checks hits and launcher names clear of the fades; T24-9 skips only log calls | ✅ 607 of 607 App tests, Core and Pan green | – |
| T24-1 to T24-13 | 2026-11-11 | Claude | rebased on `main`, after the owner's finding that the search grid no longer says more hits follow: LT/RT buttons beside the grid (`SearchViewTests` T12-16 checks where they show); Linux container, Release, `tools/ci/run-tests.sh 4` | ✅ 624 of 624 App tests, Core and Pan green | – |
| – (search opens slowly, owner's finding) | 2026-11-11 | Claude | headless, Release, 750 games in 3 launchers, library → search 6 times, branch against `main` | opening takes 0.4–0.8 s on both, the first opening 1.2–1.9 s here against 1.1 s on `main` (noisy) | not caused by this PR; own issue proposed to the owner |
