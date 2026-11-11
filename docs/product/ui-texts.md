# Product decisions: UI texts

Read before writing or changing any text the user sees. Controller buttons in texts: `docs/product/controls.md`.
Where texts live and how to add one (resource files, placeholders): `docs/architecture/ui-texts.md`.

- German UI terms, no English loan words where a German one exists: "Steam-Eingabe", not "Steam Input".
- Arcadia doesn't speak as "Wir" in its messages; say what happens or what to do.
- A subtitle below a row or a title fits on one line. Longer explanations go into the info text or panel.
- Subtitle and info text never say the same thing twice; the info text adds what the subtitle leaves out.
- A PR that adds or changes UI texts lists them (old → new) in its description, so the owner can
  approve them at a glance.
- The texts in the designs are examples: they show layout. For wording, the resource files and the rules here count.

## Glossary

| Term | Use | Instead of |
|---|---|---|
| echter / virtueller Controller | the player's pad / the copy games see; verb *virtualisieren* | emuliert, emulieren, Emulation of a controller |
| Emulation, Controller-Emulation | the process as a whole (noun only) | — |
| Controller-Wächter (Pan) | first mention on a page; after that *Pan* | Wächterdienst Pan, Controller-Wächter Pan |
| Steam-Eingabe | Steam's controller mode | Steam Input |
| neu einlesen | reading a launcher's games again | aktualisieren (for a rescan) |
| aktualisieren, Update | a new version (Arcadia, Pan, drivers, games, firmware) | — |
| PC neu starten, Neustart des PCs, PC-Neustart | restarting the machine | Windows neu starten, Windows-Neustart, Computer, neustarten |
| Steam Controller, Xbox Controller | product names, no hyphen alone; coupled in compounds (Steam-Controller-Modus) | Steam-Controller |
| zum Beispiel, eventuell | — | z. B., ggf., any other abbreviation (units like Std., Min., GB, % stay) |
| Abfrage nach Administratorrechten | the system's prompt for admin rights | Windows-Abfrage |
| kept loan words | Launcher, Tab, Artwork, Soundtrack, Firmware, Log, Update | — |

## Rules

- **Platform-neutral:** a text doesn't name Windows where the same holds on another OS ("Arcadia fragt nach
  Administratorrechten", "die Abfrage nach Administratorrechten bestätigen", "Dialogfenster des Systems"). Windows is
  named only where the player acts in a Windows place (Startmenü, Windows-Einstellungen → Apps) or where something
  exists only on Windows.
- **Technical names:** HidHide, usbip-win2 and VIIPER appear only in the rows that install, update or repair them, where
  the player changes something in HidHide himself, and in the names of their logs; SISR only in its settings group and
  the "Beende SISR" warning. Log file names (arcadia.log, pan.log) stay in error texts. Elsewhere: "Treiber",
  "Controller-Emulation".
- **Typography:** „…“ quotes, en dash with spaces (" – "), ellipsis with a space before ("Wird geladen …"),
  *neu starten* as two words.
- The CI check `UI texts` (`tools/ci/check-ui-texts.sh`) finds the forbidden spellings in the resource files on the
  owner's PRs; the rest of this file is checked by review.
