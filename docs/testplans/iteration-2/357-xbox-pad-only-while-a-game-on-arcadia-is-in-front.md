# #357 Steam Controller: Xbox pad passes input only while a game on "Arcadia" is in front

Arcadia's Xbox pad passes the Steam Controller's input only while a library game on "Arcadia" owns the foreground
window; everywhere else it reports rest. `arcadia.log` says "Xbox-Pad: gibt Eingaben weiter" / "Xbox-Pad: meldet Ruhe"
at each change. Setting "Steams Desktop-Belegung ignorieren" on (the default) unless a case says otherwise.

- [ ] **T357-1** No game running: navigate Steam (client and Big Picture) with the Steam Controller and switch windows
      repeatedly (Steam ↔ browser ↔ desktop ↔ Arcadia, also quickly in a row). Steam's button glyphs never flash to the
      Xbox icons; `arcadia.log` has no "Xbox-Pad: gibt Eingaben weiter". · E2E: yes (rules: Core
      `SteamControllerRoutingTests` T357-1; flow: App `SteamControllerModeTests`; the glyphs are manual)
- [ ] **T357-2** A game on "Arcadia" (e.g. a GOG game): its input arrives while it is in front ("gibt Eingaben weiter");
      Alt+Tab to Steam, to a browser and to the desktop: the game gets nothing there ("meldet Ruhe"), nothing reaches
      Steam as an Xbox pad; in Steam the Steam Controller navigates with Steam's own glyphs ("Kopie für Steam: gibt
      Eingaben weiter (Steam vorne, Spiele auf Arcadia)"); back to the game, input arrives again. CPU: compare the
      `ReadPacing` CPU line in `arcadia.log` during the game with a run on `feat/replace-sisr`; no measurable rise.
      · E2E: yes (App `SteamControllerModeTests`, T357-2; glyphs and CPU are manual)
- [x] **T357-3** In the game on "Arcadia", open Steam's overlay (Shift+Tab, or the Steam button): note whether the
      overlay counts as the game (pad passes, the overlay is driven through the pad) or as Steam (pad rests, the copy
      passes) and whether the overlay can be navigated with the Steam Controller. Same with Big Picture's overlay over the game. Record what was seen. · E2E: no (real
      Steam overlay)
- [ ] **T357-4** In the game on "Arcadia" with the mouse mode on, and with Arcadia in front: the game gets nothing.
      · E2E: yes (App `SteamControllerXboxPadTests`, `SteamDesktopLayoutTests`)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T357-1, T357-2, T357-4 (automated) | 2026-10-31 | Claude | branch build, Linux cloud container, fakes for pad, copy and foreground; no real controller or Steam | ✅ unit and acceptance tests green; the old rule fails T357-1's "desktop, no game" and "Steam in front, game running" rows | Real controller, glyphs and overlay (T357-1 to T357-3) need the owner's test |
| T357-1, T357-2 (automated, after QE review) | 2026-11-01 | Claude | branch build, Linux cloud container, fakes | ✅ copy passes with Steam in front during a game on "Arcadia", pad rests; `ForegroundOwner` cache tests green | Owner's test as above |
| T357-2 (partly) | 2026-11-01 | owner | PR build, owner's PC, Steam game on "Arcadia" | ✅ game in front: pad passes; to Steam: pad rests and the copy passes in the same ms; CPU 0.28 % vs 0.26 % on `feat/replace-sisr` | Alt+Tab to browser/desktop and back to the game still open |
| T357-3 | 2026-11-01 | owner | PR build, owner's PC, Steam game on "Arcadia" | ✅ Steam's overlay counts as the game: driven through the pad (Xbox 360 glyphs), fully usable | Accepted as is |
