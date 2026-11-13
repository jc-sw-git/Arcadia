# #541 Detail page: age ratings from every source in their colours, readable hints under the play button

Findings of the owner's hand test of #43 (detail page after #493/#494/#531), changes 1 to 6 of the issue. The cases were
written by the implementing thread from the issue's changes, as the issue had none; the quality engineer checks them in
the review.

**Status:** Automated: T541-1, T541-2, T541-3 (the colours chosen) · QE: T541-3, T541-4, T541-5, T541-6 · Open for the owner: T541-7, T541-8

Every case runs on the detail page with the gamepad alone unless it says otherwise; keys follow `docs/product/controls.md`.

- [x] **T541-1** "Infos" of a Steam game whose USK is Steam's own rating for Germany and which has a PEGI: every rating
  names its source the same way ("Steam"), no "laut Steam". · E2E: yes
- [x] **T541-2** A Steam game where Steam has only the USK and IGDB has USK, PEGI and ESRB: "Infos" shows Steam's USK and
  IGDB's PEGI and ESRB, each with its source; Steam's USK stays first. IGDB's ratings are read from the rating's text or
  the deprecated enum, never guessed from an id; an answer cached by the older reading is asked again at the next scan.
  · E2E: yes
- [x] **T541-3** The age rating badges in the info line and in "Infos" have a coloured border: USK 0 white, 6 yellow,
  12 green, 16 blue, 18 red; PEGI 3/7 green, 12/16 orange, 18 red; ESRB E/E10+ green, T orange, M/AO red.
  · E2E: no (window: the colour is drawn; the choice of colour is automated)
- [x] **T541-4** A game with an update or not installed, over a bright artwork: the note ("Update verfügbar …", "Die
  Installation wird in … geöffnet") reads clearly on a dark cloud that fades out softly at every edge, without a hard
  border, and doesn't cover the cover or the panel. · E2E: no (window: drawn)
- [x] **T541-5** The note stands under "Spielen"/"Installieren", left aligned with it, no longer beside the buttons; the
  button row and the panel don't move. · E2E: no (window: layout)
- [x] **T541-6** "Übersicht" with 5 quick selection entries: "Übersicht anpassen" lies in the middle between the fifth
  entry and the panel's lower edge; a greyed entry's note stands right under the fifth entry. With 3 entries (untick two
  in "Übersicht anpassen") both stay where they are with 5. · E2E: no (window: layout)
- [ ] **T541-7** **Owner:** On your PC with IGDB access, after one scan with this build: "3000th Duel" (Steam has only its
  own USK 12, igdb.com lists PEGI 12 and ESRB E10+) shows USK 12 from Steam and PEGI 12 and ESRB E10+ from IGDB in "Infos".
  Look at two more games of yours: their PEGI/ESRB match igdb.com. · E2E: no (real IGDB data)
- [ ] **T541-8** **Owner:** On the TV, two or three of your own games with bright artworks: the notes under "Spielen" read
  at a glance, the cloud behind them looks soft, the rating colours are easy to tell apart. · E2E: no (look and feel)

Automated tests:

| Test cases | Tests |
|---|---|
| T541-1 | `DetailMetadataTests` |
| T541-2 | `MetadataServiceIgdbTests`, `IgdbRulesTests` (Core) |
| T541-3 | `AgeRatingToneTests` |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T541-1 to T541-3 | 2026-11-13 | Claude | branch, Linux, `dotnet test` | ✅ | – |
| T541-7 | 2026-11-13 | owner, Claude | v0.x main build, real PC: the metadata cache of 700 games IGDB knows | ❌ only USK ever read from IGDB (406 games), never PEGI/ESRB: their category ids were guessed | fixed in this PR (read by text or deprecated enum, old answers asked again) |
| T541-3 to T541-6 | 2026-11-13 | Claude | branch, Release, Linux Xvfb 1920 × 1080, demo data; T541-4 over a bright test image laid over the artwork for the run | ✅ | – |
