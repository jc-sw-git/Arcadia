# #214 Remove SISR on request, or keep it, in the controller settings

Arcadia never starts SISR any more; the group "SISR" in "Controller" removes what an older Arcadia left, or hides
itself. How it works: `docs/controller-support/sisr-removal.md`; the product side: `docs/product/settings.md`. Needed
for the manual cases: Windows, a build of this PR, SISR as an older Arcadia left it (Arcadia 0.1.0 run once with
"Steam Controller Kompatibilität" on: `%LOCALAPPDATA%\SISR`, the "SISR Marker" in Steam, the CEF flag), Steam with
one account. Before T214-3, back up `userdata\<id>\config\shortcuts.vdf` (Run-Check-214 does this too).

Automated: `SisrRemovalTests` (T214-1 to T214-8, T214-11; SISR's files and `shortcuts.vdf` real in a temp folder,
processes and registry faked), `SteamControllerModeTests` (T214-10), unit tests `SisrInstallationTests` (what is found
and removed, the exact marker, VIIPER's key file, the start stop), `ControllerSupportServiceTests` (Steam closed and
started again, Steam game, Steam that won't close, SISR that won't stop, warning row) and `SettingsServiceTests`
(T214-9).

- [ ] **T214-1** Without SISR and its marker, "Controller" has no group "SISR"; with SISR installed or only its marker
      left, the group stands below "STEAM": "SISR entfernen" [Entfernen] ("Pan ersetzt SISR. Arcadia braucht es nicht
      mehr.", or "Übrig ist noch ein Eintrag in Steam" with only the marker) and, indented, "SISR behalten"
      [Ausblenden]. The info panel matches the design T214Settings (what goes, what stays, the warning about SISR and
      Pan at the same time). · E2E: yes
- [ ] **T214-2** Nothing happens without the player: starting Arcadia and opening the settings leave SISR, its marker
      and the CEF flag as they are, and Arcadia never starts SISR. · E2E: yes
- [ ] **T214-3** Steam closed, [Entfernen]: the dialog "SISR wird entfernt" shows briefly, then the toast "SISR ist
      vollständig entfernt.", the group goes and the focus is on "Steams Desktop-Belegung ignorieren". Gone:
      `%LOCALAPPDATA%\SISR`, `%APPDATA%\SISR`, `%APPDATA%\SISR.exe`, SISR's links on the desktop and in the start menu,
      its entry in "Apps & Features", the marker, the CEF flag, `%LOCALAPPDATA%\VIIPER`. Kept: usbip-win2, HidHide, Pan,
      Arcadia's own `viiper.exe` and `%APPDATA%\VIIPER`, the per-game modes, other non-Steam games. No UAC prompt.
      · E2E: yes (files; the real registry, links and Steam by hand)
- [ ] **T214-4** Steam running: [Entfernen] asks "Steam beenden?" ([A] Weiter, [B] Abbrechen); [B] changes nothing;
      [A] closes Steam, removes, starts Steam again in the background, the marker is gone from the library. A Steam
      game running: "Steam-Spiel läuft" names it, only "Schließen", nothing removed. Steam that won't close: the row
      reads "SISR wurde nicht entfernt" with "Steam ließ sich nicht beenden …" in its info panel, nothing removed, and a
      running SISR keeps running (a game may use its pad). · E2E: yes (Steam faked; the real Steam in T214-12)
- [ ] **T214-5** Only shortcuts whose exe is exactly `%LOCALAPPDATA%\SISR\SISR.exe` go, in every account; another
      shortcut named "SISR Marker" that points elsewhere, and every other non-Steam game, stay. `shortcuts.vdf.bak`
      lies beside each changed file. · E2E: no (unit tests; a second account by hand if there is one)
- [ ] **T214-6** [Ausblenden] hides the group at once (focus on "Steams Desktop-Belegung ignorieren"), SISR stays as it
      is, and the group stays hidden after a restart. "Auf Werkseinstellungen zurücksetzen" shows it again: with #146,
      not testable before. · E2E: yes (the factory reset deferred to #146)
- [ ] **T214-7** SISR started by hand while Pan is on: the warning "SISR läuft" stands under "Controller-Wächter (Pan)"
      while the settings are open, also with the group hidden; it goes once SISR ends; with Pan off it doesn't show.
      · E2E: yes
- [ ] **T214-8** Something can't go (SISR.exe that won't stop, a file held open): the row reads "Nicht alles ließ sich
      entfernen" with the cause in its info panel and no toast ("SISR wurde nicht entfernt" when SISR.exe won't stop);
      also when only something that alone doesn't show the group is left. [Entfernen] again, once the cause is gone,
      finishes.
      If Arcadia is killed while Steam is closed for the removal, Steam stays closed. · E2E: yes (the kill by hand)
- [ ] **T214-9** `settings.json` with `SteamControllerSupport` and `LastSisrUpdateCheck`: both are gone after the next
      change of a setting; the per-game modes (`ControllerSupportOverrides`) stay. · E2E: yes
- [ ] **T214-10** Update from Arcadia 0.1.0 with SISR running (it kept running across the update): the new Arcadia stops
      it at start, also a `viiper.exe` from SISR's folder left without its SISR; afterwards the Steam Controller works
      in the desktop as before (Steam's own layout, not the marker's) and `usbip port` lists no pad of the old VIIPER.
      An SISR started by hand (double-click on `SISR.exe`) keeps running with its VIIPER. · E2E: yes (processes faked;
      the real update and Steam's layout by hand)
- [ ] **T214-11** Controller only: the group, both rows, "Steam beenden?" ([A] Weiter, [B] Abbrechen), "Steam-Spiel
      läuft" and the removal work with the gamepad alone; B leaves the settings as always; the removal dialog can't
      be closed while it runs. · E2E: yes
- [ ] **T214-12** The marker stays gone after a removal with Steam closed and a Steam restart (Run-Check-214 on the
      owner's PC: count, close Steam, remove, start Steam and look, close Steam and count again). · E2E: no (real Steam)
- [ ] **T214-13** Uninstalling Arcadia still offers to remove SISR with the drivers; yes removes it as T214-3 does,
      no leaves it. · E2E: no (Velopack's uninstall)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T214-1 to T214-11 | 2026-10-28 | Claude | PR branch, Debug, Linux cloud container; acceptance and unit tests with faked processes and Steam; each acceptance test seen red once (see the PR) | ✅ | Windows CI and owner's PC open |
| T214-3, -4, -8, -10 | 2026-10-28 | Claude | after the QE review: same container; SISR stopped only after Steam closed, clean-up after the stop, causes shown in the row; new acceptance tests each seen red once (see the PR) | ✅ | Windows CI and owner's PC open |
| T214-12 | 2026-10-29 | owner | owner's PC, Windows, Steam with two accounts (marker in one), Run-Check-214 with `SisrMarkerCheck` (Arcadia's `SteamShortcuts.RemoveByExecutable`) | ✅ marker removed with Steam closed, not shown after Steam's start, still gone after Steam exited again | the removal with Steam closed holds; no change |
