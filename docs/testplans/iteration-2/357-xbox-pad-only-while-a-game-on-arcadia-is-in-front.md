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
      Steam as an Xbox pad; back to the game, input arrives again. · E2E: yes (App `SteamControllerModeTests`, T357-2)
- [ ] **T357-3** In the game on "Arcadia", open Steam's overlay (Shift+Tab, or the Steam button): note whether the
      overlay counts as the game (pad passes, the overlay is driven through the pad) or as Steam (pad rests) and what
      the overlay then reacts to. Same with Big Picture's overlay over the game. Record what was seen. · E2E: no (real
      Steam overlay)
- [ ] **T357-4** In the game on "Arcadia" with the mouse mode on, and with Arcadia in front: the game gets nothing.
      · E2E: yes (App `SteamControllerXboxPadTests`, `SteamDesktopLayoutTests`)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T357-1, T357-2, T357-4 (automated) | 2026-10-31 | Claude | branch build, Linux cloud container, fakes for pad, copy and foreground; no real controller or Steam | ✅ unit and acceptance tests green; the old rule fails T357-1's "desktop, no game" and "Steam in front, game running" rows | Real controller, glyphs and overlay (T357-1 to T357-3) need the owner's test |
