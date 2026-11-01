# Product decisions: Settings, category "Controller"

The page and its status messages: [settings.md](settings.md).

## The category (#169, #243, #242, #214; designs, section "#242 Pan")

- Group "PAN" first, then "MAUSMODUS". "Steams Desktop-Steuerung zulassen" is on Steam's launcher page (#315,
  `docs/product/launchers.md`). While Steam is installed, the last group "STEAM" has "Controller-Einstellungen für
  Steam" ("Weitere Controller-Einstellungen für Steam.", button "Steam-Einstellungen"): it opens Steam's launcher
  page with "Steam Controller" focused; [B] there goes back to the launcher list.
- "Controller-Wächter (Pan)" is a switch (`AppSettings.PanEnabled`, on by default; off: nothing is emulated or hidden,
  no mode, no warning rows, even with a part missing). Its subtitle and lamp say the state: "An – deine Controller
  werden von Arcadia verwaltet" (green), "Wird eingerichtet …", "Bereit – kein Controller verbunden" (grey),
  "Es liegt ein Problem vor. Prüfe die Warnmeldungen darunter." (yellow), "Angehalten – der Grund steht darunter"
  (red). Every part missing: the row offers "Treiber installieren" ("Wird installiert …", after a failure "Erneut
  versuchen" with the red X and the error in the info panel). While a game runs or starts the switch is greyed out:
  "Erst änderbar, wenn „…“ beendet ist". Off Windows: "Auf diesem System noch nicht verfügbar". Its info panel says
  the mode is chosen per launcher on its page and changed per game in its details.
- Below it, indented, one row per reason: a missing part (usbip, VIIPER, Pan, HidHide: a warning row with
  "Installieren", shown also while the switch is off; the category and the library's header carry the triangle,
  checked once at start, then while the settings are open; while such a row offers the install, the switch stays a
  switch and its status points down to it, so there is one install button, not two), HidHide's states (e.g.
  "HidHide-Konfiguration ist geöffnet … Bitte schließe sie", asked only while the settings are open; an old HidHide:
  "HidHide-Version wird nicht unterstützt" with "Aktualisieren"), "Steam Controller lässt sich nicht verstecken",
  "Emulation startet nicht", "Emulation angehalten" with "Erneut versuchen"; "Windows-Neustart ausstehend", "HidHide:
  Neustart ausstehend" and "Pan räumt noch auf" carry the blue i. "Controller wiederherstellen" (Pan's `--restore`)
  stands below the problems, indented like them, while Pan is installed, also while the switch is off.
- Controllers Arcadia saw right before a hide and doesn't see again within 5 s (#396; HidHide installed while Arcadia
  ran): the emulation ends, Pan shows them, a toast says so, and nothing is hidden until Arcadia restarts, also after
  switching Pan off and on. The row "Arcadia sieht deine Controller nicht mehr" (yellow triangle, the switch's lamp
  yellow) lists them in the box "NICHT MEHR SICHTBAR FÜR ARCADIA", ends with "Kommt diese Meldung nach dem Neustart von
  Arcadia wieder, starte Windows neu.", and offers "Arcadia neu starten".
- Every install that runs HidHide's installer ("Installieren", "Aktualisieren", "Neu installieren", "Treiber
  installieren") asks "HidHide installieren?" first, also without Steam running: Arcadia restarts once afterwards, as
  only a new Arcadia sees what Pan hides; with Steam running the same dialog says Steam closes ("Steam beenden und
  installieren"). After the install Pan hides nothing until Arcadia has restarted, which it does by itself. No Windows
  restart is needed for this.
- Every other controller (#238, designs T238F05–F07), only while the emulation runs: one warning per firmware tool that
  runs, "Xbox Zubehör ist geöffnet" / "DualSense-Firmware-Updater ist geöffnet" (subtitle "Arcadia übernimmt den …,
  sobald du … schließt", box "WARTET AUF ÜBERNAHME"), gone once the tool has ended; then, with the blue i, one row for
  the connected controllers Pan couldn't hide: "<Name> lässt sich nicht verstecken" / "<n> Controller lassen sich nicht
  verstecken", box "BETROFFEN" with each name and why. Not shown while Pan is off (the player chose that).
- "Controller zuordnen" (#238 D3a, designs T238Settings, T238Modal): indented below those rows, above "Controller
  wiederherstellen", while Pan is on and any controller is connected; subtitle "Ändere das Layout deiner angeschlossenen
  Controller", [Zuordnen]; its info panel ends with the box "SPIELE SEHEN" (each connected model → its layout, or "den
  echten Controller (nicht übernommen)"). [Zuordnen] opens a modal with one row per connected model (its default as
  subtitle "Vorgabe: …"): ▲ ▼ moves, ◀ ▶ cycles Xbox 360 Controller, DualShock 4, DualSense, (DualSense Edge only for
  an Edge,) Switch 2 Pro Controller, "Nicht übernehmen" (wraps), as does A on a row, without applying. Below the rows the button
  [Übernehmen] (▲ ▼ reach it, A or a click) is the only way to apply: all changes once, then it closes. B (✕) closes
  without them. Not at once: each applied change replaces the pad,
  and "Nicht übernehmen" and back restart the real controller, which Steam announces each time. The Steam Controller's row ("Steam Controller (Modus „Arcadia“)") is locked to Xbox 360 Controller and
  takes the focus; its note shows while focused (#336 lets it choose). Stored per model (USB ids), for every controller
  of it, kept across restarts; choosing the default stores nothing. The modal closes when the row goes.
- After "Controller freigeben" for a firmware update, the switch reads "An – Steam Controller und Puck freigegeben, bis
  du Pan aus- und wieder einschaltest" (no triangle, no row below); switching Pan off and on, or a restart of Arcadia,
  takes them over again. While Steam flashes a firmware update, the switch reads "Wird eingerichtet …" (no triangle)
  until Pan may hide again.
- "Erneut versuchen" on a Pan row (#308): the row stays while the attempt runs (also when its problem is gone
  meanwhile), reads "Wird versucht …" with a spinner and can't be pressed; when the attempt ends, a toast says how:
  "Pan: Controller-Wächter läuft wieder", "Pan: Erneuter Versuch ohne Erfolg. …", or why there is nothing to do (no
  Steam Controller, Steam Controller and puck excluded); only switching Pan off meanwhile has none.
- Pan's notices: a toast while Arcadia is in front, the overlay notice (where the mouse mode shows its label) while a
  game is in front, so the player sees them in the game too. A hold the player is watching goes on: during the View
  hold the notice stands below "Zurück zu Arcadia". An installed Pan that isn't the bundled one is announced at start
  and updated only on [A] (#297, #326, page "Neue Pan-Version", `docs/controller-support/pan-update.md`); declined,
  Pan is switched off, and the switch reads "Neue Pan-Version verfügbar" (blue i, a blue box first in its info panel)
  and updates Pan before it switches on.
- Group "SISR" (#214, designs T214Settings and T214Dialog) below "STEAM", only while SISR or its Steam marker is on
  the PC: "SISR entfernen" [Entfernen] and, indented, "SISR behalten" [Ausblenden]. Arcadia never starts SISR and
  removes nothing without the player. With the marker and Steam running, [Entfernen] asks "Steam beenden?" ([A]
  Weiter, [B] Abbrechen); a Steam game shows "Steam-Spiel läuft" with its name and only "Schließen". While it removes,
  the dialog "SISR wird entfernt" names the step and can't be closed. Everything gone: the toast "SISR ist vollständig
  entfernt.", the group goes, the focus moves to the row above. Something left: the row reads "Nicht alles ließ sich
  entfernen" ("SISR wurde nicht entfernt" when a Steam game, a Steam that won't close or an SISR that won't stop kept
  everything), the cause in its info panel; [Entfernen] tries again. "SISR behalten" hides the group for good
  (`AppSettings.KeepSisr`; the factory reset, #146, shows it again); SISR stays as it is. While SISR runs and Pan is
  on, the warning row "SISR läuft" stands under Pan, also with the group hidden. What goes and why:
  `docs/controller-support/sisr-removal.md`.
