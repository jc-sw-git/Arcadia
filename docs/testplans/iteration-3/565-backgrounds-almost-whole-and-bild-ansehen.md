# #565 Show backgrounds almost completely, and "Bild ansehen" on the detail page

**Status:** Automated: T565-1 to T565-11, T565-13 (layout rule, timer, order, input) · QE: T565-1 to T565-5, T565-12, T565-14 (the look in the running app) · Open for the owner: T565-15

- [x] **T565-1** A game with a flat (3.1 : 1) background focused in the library: the preview card shows it with at most
  10 % cut at the sides and a narrow blurred margin above and below. · E2E: no (window: layout)
- [x] **T565-2** The same with a 16 : 9 background: at most 10 % cut above and below, narrow blurred margins at the sides.
  · E2E: no (window: layout)
- [x] **T565-3** The detail page of a game with a flat background: the image spans the full width uncut at the top and
  fades softly into its blurred copy below; title, tabs and footer stay readable. · E2E: no (window: layout)
- [x] **T565-4** The detail page of a game with a 16 : 9 background: the image fills the screen uncut. · E2E: no (window: layout)
- [x] **T565-5** Another format (21 : 9, 4 : 3, between 16 : 9 and flat): at most 10 % cut and blurred margins, on the
  card and the detail page, never stretched. · E2E: no (window: layout)
- [x] **T565-6** "Bild ansehen" starts: the detail page with nothing open, no input for 10 s: the page fades out, the
  game's background shows whole and centred, its title bottom left. · E2E: yes
- [x] **T565-7** Next image: 10 s later a random other game's background of the tab blends in with its title, then every
  10 s the next; none twice before all were shown, a game without a background never. · E2E: yes
- [x] **T565-8** Input ends it: while it shows another game's image, A on the controller: the original game's page is
  back with the same focus, no game starts; the next press acts again. · E2E: yes (controller); keys, mouse and wheel
  E2E: no (window: real input)
- [x] **T565-9** Not while something is open: with "Ansicht" (L3) open over the page and in the library it never
  starts. · E2E: yes
- [x] **T565-10** Not in the background: Arcadia leaves the front while it shows: it ends at once, no timer runs; back in
  front it starts again after 10 s. · E2E: yes
- [x] **T565-11** The tab has no other game with a background: the own image stays. · E2E: yes
- [ ] **T565-12** "Artwork ändern" (preview and tiles, tab "Hintergrund") and "Warten auf deine Auswahl" show backgrounds
  the same way: almost whole, blurred margins. · E2E: no (window: layout)
- [x] **T565-13** An image of the pool can't be loaded: it is skipped and the next one taken, without an error; a game
  gone from the tab is never picked. · E2E: yes
- [ ] **T565-14** The fades are smooth: the page fades out and back in in about 1 s, the next image blends in about
  1.5 s, the blurred copy is soft (no blocks). · E2E: no (window: animation)
- [ ] **T565-15** **Owner:** on the TV with the controller, a few Steam (flat) and Epic (16 : 9) games: preview card,
  detail page and "Bild ansehen" for a minute, then any button; nothing starts, the page is back. Also while a game runs
  from the detail page: nothing of it shows over the game. · E2E: no (real images, TV, controller)

Automated tests:

| Test cases | Tests |
|---|---|
| T565-1 to T565-5 | `BackgroundFitTests` (the placement rule; the look stays QE's) |
| T565-6 to T565-11 | `ImageShowAcceptanceTests`; the timer's and order's variants in `ImageShowTests` |
| T565-13 | `ImageShowTests.An_image_that_cant_be_shown_is_skipped_and_the_next_one_taken` |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T565-6 to -11, -13 | 2026-11-13 | Claude | branch, Linux, headless tests | ✅ | – |
| T565-1 to -4, -6 to -8, -14 | 2026-11-13 | Claude | branch, Linux/Xvfb, `--demo --demo-games 120` with the design's sample images, keys | ✅ card 16 : 9 and flat with narrow blurred margins; detail page flat full width fading into the blur, 16 : 9 full screen; the show started after 10 s, turned to another game, Esc brought the page back | QE looks at all of it |
| T565-1 to -5, -12 ("Artwork ändern"), -14 | 2026-11-13 | Claude (QE) | bb6141c, Linux/Xvfb, Debug, `--demo --demo-games 120` with test images 3.1 : 1, 16 : 9, 21 : 9, 4 : 3, 2 : 1 (white corners mark the image's edge), keys and mouse | ✅ card: flat and 16 : 9 cut 10 % with narrow blurred margins, 21 : 9 and 2 : 1 cut without margins, 4 : 3 with wide blurred sides; detail page: flat and 21 : 9 full width uncut fading into the blur, 16 : 9 all four corners, 4 : 3 centred with blurred sides; "Artwork ändern" preview and tile alike; show: page fades out, image whole with soft edges, title bottom left. ❌ the cross-fade to the next image passes through the dark page colour while the next image is still decoding (F2); a mouse move followed at once by a click ends the show and the click starts the game (F1) | F1, F2 to the architect; T565-12 "Warten auf deine Auswahl" only checked in the code (no SteamGridDB key here), T565-14 open until F2 |
| T565-8 (keys, mouse, wheel), T565-9 | 2026-11-13 | Claude (QE) | as above | ✅ a key, a click without moving and the wheel end the show and do nothing else; the next key acts; "Ansicht" open: no show for 13 s. ❌ move, then click (F1) | F1 |
| T565-8 (move, then click), T565-14 | 2026-11-13 | Claude | F1/F2 fix, Linux/Xvfb, Debug, `--demo --demo-games 120` with the design's sample images, keys and mouse | ✅ move, then a click on "Spielen" at once and 0.4 s later: the show ends, nothing starts; the next image blends over the one before, never through the page colour | QE checks F1/F2 again |
