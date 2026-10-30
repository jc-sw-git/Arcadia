# #315 "Steams Desktop-Belegung ignorieren" on Steam's page, alike for every controller

The setting moves from the controller settings to Steam's launcher page, group "Desktop-Steuerung", and treats every
controller alike, with Pan on or off. Decisions: issue #315, [settings.md](../../product/settings.md),
[steam-controller-copy.md](../../controller-support/steam-controller-copy.md).

- [ ] **T315-1** Einstellungen → Launcher → Steam (active or not): the last group "DESKTOP-STEUERUNG" with
      "Steams Desktop-Belegung ignorieren", "An", subtitle "Steams Desktop-Steuerung wird momentan ignoriert"; switched
      off: "Steams Desktop-Steuerung ist momentan aktiviert". The info panel has the five texts of the issue, the Guide
      button and [RS] as glyphs of the controller in use (Xbox's without one), the Steam Controller's note as a blue box
      in bold. No other launcher's page has the row; Einstellungen → Controller has no group "STEAM" any more.
      Controller alone: ▼ reaches it, ◀ ▶ and [A] switch it, [B] goes back. · E2E: yes
- [ ] **T315-2** Steam running, setting on, Pan on and off, with a Steam Controller and another controller: in front
      of Arcadia, on the desktop and in the mouse mode no controller triggers Steam's desktop layout (trackpad or Guide
      + [RS] pointer, ⧉ + ≡ Alt+Tab). · E2E: no (real Steam and Steam Controller; the rules are unit- and
      acceptance-tested)
- [ ] **T315-3** Setting off, Pan on, Steam Controller in the action set "Desktop", Arcadia in front: the trackpad moves
      the pointer, ⧉ + ≡ switches the action set to "Gamepad" (A2) or makes Alt+Tab; the same in the mouse mode and on
      the desktop without a game. Another controller does the same with Guide + [RS] and ⧉ + ≡. · E2E: yes (A2, part
      automated: the copy passes and lets ⧉ + ≡ through; Steam's reaction only on the owner's PC)
- [ ] **T315-4** Switching takes effect at once, also with a game running: a game on "Arcadia" keeps the Xbox pad
      while in front, whatever the setting; a game on "Steam-Eingabe" gets the Steam Controller as before.
      `arcadia.log` names each change of the copy ("Kopie für Steam: gibt Eingaben weiter (…)" / "meldet Ruhe (…)").
      · E2E: yes
- [ ] **T315-5** A1: `checks/315/Run-Check-315-v3.ps1 -Lauf PanAn` with the setting off and Steam in front gives
      trackpad pointer, B as Esc and LT as right click like the baseline `-Lauf PanAus`. · E2E: no (check script on
      the owner's PC)
- [ ] **T315-6** A `settings.json` from before keeps the stored value (one without it: on). Factory reset (#146) sets
      it on. · E2E: no (stored value: `SettingsServiceTests`; #146 isn't built yet, checked with #146)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T315-1, T315-3, T315-4 | 2026-10-30 | Claude | PR branch on `feat/replace-sisr`, Debug, Linux cloud container (.NET 10, Avalonia headless) | ✅ `SteamDesktopLayoutTests`, `SteamControllerRoutingTests`; the copy tests were red once with the setting ignored in the wiring | On the owner's PC |
| T315-1 | 2026-10-30 | Claude | same, Release, Xvfb 1920×1080, `--demo`, keys by xdotool | ✅ Steam (inactive) page ends with "DESKTOP-STEUERUNG", subtitle follows the switch, info panel with Guide/RS/⧉/≡ glyphs and the bold blue box; glyphs inside the box were unstyled at first, fixed (`noteText`) | Glyphs of other controllers on the owner's PC |
