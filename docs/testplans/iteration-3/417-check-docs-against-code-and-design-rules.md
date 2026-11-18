# #417 Check all docs against the code and write down the design rules

<!-- The issue is docs work; its only product change is the fade-out rule of docs/product/design.md applied to the two
     scrolling areas that lacked it. -->

**Status:** Automated: – · QE: T417-1, T417-2, T417-3 · Open for the owner: none

- [x] **T417-1** "Artwork ändern" on a game whose tiles fit the grid (demo data, no SteamGridDB key): the grid shows no
  fade, the focus ring of the focused tile is drawn whole · E2E: no (window)
- [x] **T417-2** "Artwork ändern" on a game with more SteamGridDB suggestions than fit: the grid fades out at the
  bottom (and at the top once scrolled) with a soft edge right at the grid's clip, ▼ through the tiles keeps the
  focused tile out of the fade with its ring whole, B closes · E2E: no (window)
- [x] **T417-3** "Warten auf deine Auswahl", tab "Metadaten", a hit with a long description and genres, developer and
  publisher (a description alone never overflows: 600 characters, 9 lines): the facts box fades out at the bottom,
  RS scrolls it, at the end the fade is at the top only; a hit whose facts fit shows none · E2E: no (window)

Automated tests: none; the change is two `EdgeFade`s in views (the control itself is covered by the window guards of
#102/#237).

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T417-1 | 2026-11-13 | Claude | PR for #417, Release, `--demo --demo-games 60`, Xvfb 1920×1080 | ✅ | – |
| T417-2, T417-3 | 2026-11-13 | QE (Claude) | PR #570, Release, real `MainWindow` headless 1920 × 1080, fake SteamGridDB (30 covers) and fake IGDB hit | T417-2 ❌ 8 px unfaded band with a hard edge above and below the fade, ✅ with the fade's `Margin="-8"`; T417-3 ✅ with the set-up above | Fixed in #570 (`TileFade` margin); T417-3's precondition rewritten |
| all tests | 2026-11-13 | Claude | PR #570, Release, Linux cloud container, `tools/ci/run-tests.sh 4` | ✅ 793 App test methods, Core and Pan green | – |
