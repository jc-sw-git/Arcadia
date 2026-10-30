# #169 Group the Steam settings under "Steam"

T169-4 and T169-5 no longer apply: the link on Steam's page went with #178 (owner, 2026-10-28).

- [ ] **T169-1** Einstellungen → Controller: a box "STEAM" comes first with "Steam Controller Kompatibilität", its
      messages and "SISR aktualisieren" if any, then "Steams Desktop-Belegung ignorieren" ("Alle Controller: …").
      After it "MAUSMODUS" and "ZURÜCK ZU ARCADIA"; there is no box "STEAM CONTROLLER" any more. · E2E: yes
- [ ] **T169-2** "Steams Desktop-Belegung ignorieren" is not indented and can be switched (◀ ▶), also with
      "Steam Controller Kompatibilität" off and the mouse mode "Aus". · E2E: yes
- [ ] **T169-3** Controller only: ▲ ▼ reach every row in the new order, the list scrolls along, B goes back. · E2E: yes
- [ ] **T169-4** Einstellungen → Launcher → Steam (active or not): at the bottom a box "CONTROLLER" with
      "Controller-Einstellungen für Steam" and "Öffnen"; its info text shows A and B as glyphs. The other launchers'
      pages don't have it. · E2E: yes
- [ ] **T169-5** A (space, click) on "Öffnen" shows the category "Controller" with the focus on "Steam Controller
      Kompatibilität"; B (Esc) goes back to Steam's page with the focus on the link, B again to the launcher list.
      After LB/RB to another category and back, B works as usual (in "Controller" it closes the settings). · E2E: yes
- [ ] **T169-6** "Steam Controller Kompatibilität" off or not installed: subtitle "Erlaubt es, den Steam Controller
      auch in Spielen außerhalb von Steam zu verwenden". On: a one-line status instead (e.g. "Aktiv – der Steam
      Controller läuft als Xbox Controller", "Pausiert, solange ein Spiel mit „Steam Input“ läuft"). · E2E: yes
- [ ] **T169-7** The lamp beside the name: green while SISR runs and no message is there; grey when on but paused
      (Steam game with "Steam Input"), while SISR starts or no Steam Controller is found; yellow as soon as a message
      with a warning triangle is below (e.g. "Steam meldet sich noch an"); red with one with a red X (e.g. "SISR
      stürzt wiederholt ab"). Off: no lamp. · E2E: yes (paused: no, needs a running Steam game)
- [ ] **T169-8** Messages and "SISR aktualisieren" stand right below "Steam Controller Kompatibilität", one step
      indented, before "Steams Desktop-Belegung ignorieren" (not indented). · E2E: yes

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T169-1, T169-3 | 2026-10-24 | Claude | Branch `feat/steam-einstellungen-in-einer-gruppe-169`, Linux, Xvfb 1920×1080, `--demo`, keys by xdotool; `dotnet test` | ✅ Box "STEAM" first with both rows, then "MAUSMODUS"; S, S goes from the compatibility via the desktop layout to the mouse mode. SISR isn't available on Linux, so no messages, update or status | T169-1 with SISR and T169-2 on the owner's PC |
| T169-4, T169-5 | 2026-10-24 | Claude | same, `--demo` (Steam not found, not active) | ✅ Box "CONTROLLER" on Steam's page, "Öffnen" goes to "Steam Controller Kompatibilität", Esc back to the link; after ⇧E, ⇧Q Esc closes the settings. Click and the other launchers' pages not looked at | On the owner's PC |
| T169-4 | 2026-10-24 | owner | PR #170 | ❌ The link's info text wrote A and B as letters instead of glyphs | Fixed in PR #170 ([A], [B]; also the "Y" row of the mouse mode help), new test `ButtonGlyphTextTests` |
| T169-4 | 2026-10-24 | Claude | same, Linux, Xvfb, `--demo` | ✅ Info text shows [A] and [B] as glyphs, the mouse mode help "Bildschirmtastatur ([B] blendet sie aus)" too | On the owner's PC |
| T169-6 to T169-8 | 2026-10-24 | Claude | same, Linux, build and tests only | ➖ not runnable: SISR doesn't exist on Linux, the row only shows "Momentan nur unter Windows verfügbar." | Acceptance tests below, then the owner's PC |
| T169-1 to T169-8 | 2026-10-25 | Claude | PR #170 merged with `main`, Linux, `SteamSettingsGroupTests` (headless, Steam and SISR faked) | ✅ 7 tests green; each was red once with its feature broken on purpose (named in the PR). Paused lamp not automated | Owner's manual test |
