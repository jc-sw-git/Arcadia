# #296 Restart the Steam Controller's puck for a driver install only while Steam is closed

HidHide's installer restarts the connected puck. "Treiber installieren" (and the part rows "HidHide fehlt" /
"Wächterdienst Pan fehlt" / "usbip-Treiber fehlt") and "Neu installieren" for "HidHide antwortet unerwartet" now close a
running Steam first, after the same "Steam beenden?" as the SISR setup, and start it again afterwards, but only when the
step runs HidHide's installer (`IControllerSupportPlatform.InstallsHidHide`). A running Steam game: "Steam-Spiel läuft",
nothing happens. The Pan update at start does the same while it still installs missing drivers; #297 replaces it with a
pure Pan update without HidHide's installer.

- [ ] **T296-1** HidHide uninstalled, Steam running (no game). PAN group, "HidHide fehlt" → "Installieren" with [A]:
      "Steam beenden?" opens, the focus on "Steam beenden und installieren", the text says the installation restarts the
      puck. [B] closes it, nothing is installed, Steam keeps running. [A] again, then [A] in the dialog: Steam closes,
      Windows asks once, HidHide is installed, Steam comes back in the tray, the row goes. · E2E: yes
      (`EmulationSetupTests`; the real UAC and the puck restart by hand)
- [ ] **T296-2** Only Pan missing (HidHide current), Steam running: "Installieren" asks nothing about Steam, Steam keeps
      running, Windows asks once. · E2E: yes (`EmulationSetupTests`, Core)
- [ ] **T296-3** "HidHide antwortet unerwartet" with Steam running: "Neu installieren" asks "Steam beenden?"; [A] closes
      Steam, reinstalls HidHide, starts Steam again; no "Beende zuerst Steam" message any more. · E2E: yes
      (`EmulationSetupTests`; the real reinstall by hand)
- [ ] **T296-4** A Steam game runs (e.g. started from Steam), HidHide missing: "Installieren" shows "Steam-Spiel läuft"
      naming the game; [B] closes it, nothing is installed, the game keeps running. Ending the game while the notice is
      open turns it into "Steam beenden?" within a second. · E2E: yes (`EmulationSetupTests`)
- [ ] **T296-5** HidHide missing, Steam running: [A] in "Steam beenden?", then "Nein" in the Windows prompt: the row
      shows the "abgelehnt" message, and Steam is started again. · E2E: no (real UAC; Core covers declined, failed and
      cancelled installs)
- [ ] **T296-6** Steam that won't close (e.g. a Steam dialog blocking its exit): the action stops with "Steam ließ sich
      nicht beenden …" and installs nothing. · E2E: no (needs a real Steam that refuses; Core covers it)
- [ ] **T296-7** (dropped with #297: the update at start installs only Pan, never HidHide, and leaves Steam alone; see T297-4) Arcadia brings a newer Pan, HidHide uninstalled, Steam running: the question "Pan aktualisieren" at
      start ends with "Steam wird dafür kurz beendet und danach wieder gestartet."; [A] closes Steam, updates, starts
      Steam again. With a Steam game running the question doesn't come at this start; with a current HidHide it comes
      without the Steam sentence and leaves Steam alone. · E2E: yes (`PanGuardianTests`; obsolete once #297 lands)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T296-1 to T296-4 (App), T296-1 to T296-6 (Core `ControllerSupportServiceTests`, incl. a Steam slow to come back), all App and Core tests | 2026-10-28 | Claude | PR branch, Linux cloud container, Avalonia headless | ✅ (each also ❌ once with the code broken on purpose, see the PR) | – |
| T296-4 hand-over (App: `EmulationSetupTests` the game ending turns the notice into "Steam beenden?", [A] installs with Steam closed), all App, Core and Pan tests | 2026-10-28 | Claude (QE review) | PR branch c7b02d0, Linux cloud container, Avalonia headless; Pan 190, Core 1930, App 365 plus the new test; the new test seen red once (game prompt closing instead of handing over) | pass | Pan update at start (B1 in the QE review) open |
| T296-7 (App: `PanGuardianTests`), all App, Core and Pan tests | 2026-10-28 | Claude | PR branch 683c9f9, Linux cloud container, Avalonia headless | ✅ (both new tests ❌ once with the question ignoring Steam) | B1 fixed |
| T296-1 to T296-7 manual | 2026-10-28 | owner | – | ⏭️ Not run before the merge: moved to the release candidate at the iteration end (owner, 2026-10-28) | Runs in the iteration's acceptance review on the RC |
