# #417 Check all docs against the code and write down the design rules

<!-- The issue is docs work; its only product change is the fade-out rule of docs/product/design.md applied to the two
     scrolling areas that lacked it. -->

**Status:** Automated: – · QE: T417-1 · Open for the owner: T417-2, T417-3

- [x] **T417-1** "Artwork ändern" on a game whose tiles fit the grid (demo data, no SteamGridDB key): the grid shows no
  fade, the focus ring of the focused tile is drawn whole · E2E: no (window)
- [ ] **T417-2** **Owner:** "Artwork ändern" with a SteamGridDB key on a game with more suggestions than fit: the grid
  fades out at the bottom (and at the top once scrolled), ▼ through the tiles keeps the focused tile out of the fade
  with its ring whole, B closes · E2E: no (window, needs a SteamGridDB key)
- [ ] **T417-3** **Owner:** "Warten auf deine Auswahl", tab "Metadaten", a hit with a long description: the facts box
  fades out where more text follows, RS scrolls it, the fade goes at the end · E2E: no (window, needs IGDB access)

Automated tests: none; the change is two `EdgeFade`s in views (the control itself is covered by the window guards of
#102/#237).

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T417-1 | 2026-11-13 | Claude | PR for #417, Release, `--demo --demo-games 60`, Xvfb 1920×1080 | ✅ | – |
| all tests | 2026-11-13 | Claude | same, `tools/ci/run-tests.sh 4` | ✅ 793 App test methods, Core and Pan green | – |
