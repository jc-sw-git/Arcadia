# #315 "Steams Desktop-Steuerung zulassen" on Steam's page, alike for every controller

The setting moves from the controller settings to Steam's launcher page, group "Desktop-Steuerung", is renamed from
"Steams Desktop-Belegung ignorieren" and inverted (off by default, same behaviour as before), and treats every
controller alike, with Pan on or off. "On"/"off" below mean "Steams Desktop-Steuerung zulassen". Decisions: issue #315, [settings.md](../../product/settings.md),
[steam-controller-copy.md](../../controller-support/steam-controller-copy.md).

- [x] **T315-1** Einstellungen → Launcher → Steam (active or not): the last group "DESKTOP-STEUERUNG" with
      "Steams Desktop-Steuerung zulassen", "Aus", subtitle "Steams Desktop-Steuerung wird momentan ignoriert"; switched
      on: "Steams Desktop-Steuerung ist momentan aktiviert". The info panel has the texts of the issue plus why one would allow
      it ("Das lohnt sich vor allem, wenn du dir in Steam eine eigene Desktop-Belegung eingerichtet hast …"); the Guide button and [RS]
      as glyphs of the controller in use (Xbox's without one); the Steam Controller's note as a blue box in bold. The
      glyphs in the blue box and in "Um den Wechsel des Aktionssets (⧉ + ≡) …" are always the Steam Controller's, also
      with a PlayStation or Nintendo controller in use. No other launcher's page has the row; Einstellungen → Controller has no group "STEAM" any more.
      Controller alone: ▼ reaches it, ◀ ▶ and [A] switch it, [B] goes back. · E2E: yes
- [x] **T315-2** Steam running, setting off, Pan on and off, with a Steam Controller and another controller: in front
      of Arcadia, on the desktop and in the mouse mode no controller triggers Steam's desktop layout (trackpad or Guide
      + [RS] pointer, ⧉ + ≡ Alt+Tab). · E2E: no (real Steam and Steam Controller; the rules are unit- and
      acceptance-tested)
- [x] **T315-3** Setting on, Pan on, Steam Controller in the action set "Desktop", Arcadia in front: the trackpad moves
      the pointer, ⧉ + ≡ switches the action set to "Gamepad" (A2) or makes Alt+Tab; the same in the mouse mode and on
      the desktop without a game. Another controller does the same with Guide + [RS] and ⧉ + ≡. · E2E: yes (A2, part
      automated: the copy passes and lets ⧉ + ≡ through; Steam's reaction only on the owner's PC)
- [x] **T315-4** Switching takes effect at once, also with a game running: a game on "Arcadia" keeps the Xbox pad
      while in front, whatever the setting; a game on "Steam-Eingabe" gets the Steam Controller as before.
      `arcadia.log` names each change of the copy ("Kopie für Steam: gibt Eingaben weiter (…)" / "meldet Ruhe (…)").
      · E2E: yes
- [x] **T315-5** A1: `checks/315/Run-Check-315-v3.ps1 -Lauf PanAn` with the setting on and Steam in front gives
      trackpad pointer, B as Esc and LT as right click like the baseline `-Lauf PanAus`. · E2E: no (check script on
      the owner's PC)
- [x] **T315-6** A `settings.json` from before keeps its meaning ("ignorieren" on there shows "zulassen" off; one
      without the value: off). Factory reset (#146) sets it off. · E2E: no (stored value: `SettingsServiceTests`; #146 isn't built yet, checked with #146)
- [x] **T315-7** Steam installed: Einstellungen → Controller ends with the group "STEAM", row "Controller-Einstellungen
      für Steam", subtitle "Weitere Controller-Einstellungen für Steam.", button "Steam-Einstellungen"; its info
      panel: "Arcadia hat erkannt, dass du Steam installiert hast. …". [A] (or a click) opens Steam's launcher page with
      "Steam Controller" focused; [B] goes to the launcher list. Steam not installed: no group "STEAM". · E2E: yes

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T315-1, T315-3, T315-4 | 2026-10-30 | Claude | PR branch on `feat/replace-sisr`, Debug, Linux cloud container (.NET 10, Avalonia headless) | ✅ `SteamDesktopLayoutTests`, `SteamControllerRoutingTests`; the copy tests were red once with the setting ignored in the wiring | On the owner's PC |
| T315-1 | 2026-10-30 | Claude | same, Release, Xvfb 1920×1080, `--demo`, keys by xdotool | ✅ Steam (inactive) page ends with "DESKTOP-STEUERUNG", subtitle follows the switch, info panel with Guide/RS/⧉/≡ glyphs and the bold blue box; glyphs inside the box were unstyled at first, fixed (`noteText`) | Glyphs of other controllers on the owner's PC |
| T315-1, T315-3, T315-4 | 2026-10-30 | Claude (QE) | same, Debug, Linux cloud container | ✅ three more acceptance tests in `SteamDesktopLayoutTests` (desktop without a game with ▶, switched during a game on "Arcadia" with "meldet Ruhe" in the log, game on "Steam-Eingabe" both ways), each red once against a mutated rule (pad ignores the setting, gate never ignores it, copy rests for "Steam-Eingabe" while off); full suite green | On the owner's PC |
| T315-1 | 2026-10-30 | Claude (QE) | same, Xvfb 1920×1080, `--demo`, keys by xdotool | ✅ last row focused, ring whole, "An"/"Aus" with both subtitles, info panel with glyphs and bold blue box; "⧉ + ≡" breaks between the two glyphs at a line end (review finding) | — |
| T315-1, T315-7 | 2026-10-30 | Claude | same, Debug, Linux cloud container, Avalonia headless | ✅ renamed and inverted row, new row "Controller-Einstellungen für Steam" (`SteamDesktopLayoutTests`); the jump was red once without the focus on "Steam Controller" | On the owner's PC |
| T315-1 | 2026-10-30 | Claude | same, Avalonia headless | ✅ fixed Steam Controller glyphs in the blue box and the last text (`ButtonSymbolsTests`, `SteamDesktopLayoutTests`); red once with the fixed printing ignored | With a PlayStation controller on the owner's PC |
| T315-1 – T315-5, T315-7 | 2026-10-31 | Owner | PR branch, owner's PC (Windows) | ✅ passed | Merge |
