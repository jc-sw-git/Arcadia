# Product decisions: English UI texts

The English translation of every text (`*Texts.en.resx`, `docs/architecture/ui-texts.md`). The rules of
`ui-texts.md` hold in English too (no "we", subtitle on one line, subtitle and info never repeat each other, the PR
lists changed texts); this file adds what differs. Every new or changed German text gets its English one in the same
PR.

- US English: color, favorite, canceled, virtualize.
- Sentence case for titles, rows and buttons ("Apply language", "Restart PC"); group headings in capitals as in
  German ("LANGUAGE").
- Contractions are fine and read friendlier ("can’t", "isn’t"); the player is "you".
- Controller buttons as in German: ⧉ and ≡, the others in brackets ("[A]", "[LB]"); `ButtonGlyphTextTests` checks
  LB, RB, LT, RT, L3, R3, LS, RS and D-Pad, not A, B, X, Y, which are words in English.

## Glossary

| Term | Use | Instead of |
|---|---|---|
| real / virtual controller | the player's pad / the copy games see; verb *virtualize* | emulated, emulate |
| emulation, controller emulation | the process as a whole (noun only) | — |
| controller guard (Pan) | first mention on a page; after that *Pan* | guardian service, watchdog |
| Steam Input | Steam's controller mode | Steam-Input |
| rescan, scan | reading a launcher's games (again) | refresh, update (for a rescan) |
| update | a new version (Arcadia, Pan, drivers, games, firmware) | upgrade |
| restart the PC, PC restart | restarting the machine | restart Windows, reboot, computer |
| administrator prompt, asks for administrator rights | the system's prompt for admin rights | UAC prompt, Windows prompt |
| Steam Controller, Xbox Controller, puck | product names as in German | — |
| for example | — | e.g., i.e. or any other abbreviation (units like h, min, GB, % stay) |
| your own, own | what the player set (your own title, your tags, own file); a launcher logo: custom image | — |
| info, info panel | the panel beside a row | details, tooltip |

## Typography

- Quotes “…”, inside them ‘…’; apostrophe ’.
- En dash with spaces (" – ") as in German.
- Ellipsis without a space before ("Loading…", "Upload…").
- Numbers and dates follow the system's region, not the UI language; units keep a space ("12 MB", "30 %").

The CI check `UI texts` (`tools/ci/check-ui-texts.sh`) finds the forbidden spellings per language: straight quotes,
"...", " - ", a space before "…", German quotes or letters, "emulate", "e.g.", "restart Windows" and British spellings.
