# Product decisions: French UI texts

The French translation of every text (`*Texts.fr.resx`, `docs/architecture/ui-texts.md`). The rules of `ui-texts.md`
hold in French too (no "nous", subtitle on one line, subtitle and info never repeat each other, the PR lists changed
texts); this file adds what differs. Every new or changed German text gets its French one in the same PR.

- The player is "vous" (vouvoiement), as in comparable products; Arcadia is "il" ("Arcadia redémarre de lui-même").
- Sentence case for titles, rows and buttons ("Appliquer la langue", "Redémarrer le PC"); group headings in capitals,
  accents kept ("PARAMÈTRES", "ÉDITEUR").
- Buttons and actions are infinitives ("Installer", "Réessayer"); toasts and states are short sentences or nouns.
- Controller buttons as in English: ⧉ and ≡, the others in brackets ("[A]", "[LB]").
- A placeholder is never followed by a word that has to agree with it (gender, number): "Impossible de cacher
  {controller}", not "{controller} ne peut pas être cachée".
- A language name from the system ({language}) stands after a colon: "Langue du système : {language}."

## Glossary

| Term | Use | Instead of |
|---|---|---|
| real / virtual controller | manette réelle / manette virtuelle; verb *virtualiser* | émuler |
| emulation | émulation (noun only) | — |
| controller guard (Pan) | gardien des manettes (Pan); after that *Pan* | service de surveillance |
| Steam Controller | le Steam Controller (masculine, as Steam writes it) | la Steam Controller |
| Xbox Controller, DualSense | la manette Xbox, la DualSense | — |
| Steam Input | Steam Input | Steam-Input |
| scan, rescan | analyse, analyser; nouvelle analyse, réanalyser | actualiser |
| update | mise à jour, mettre à jour | upgrade |
| restart the PC | redémarrer le PC, redémarrage du PC | redémarrer Windows, ordinateur |
| administrator prompt | demande de droits d’administrateur | UAC |
| for example | par exemple | p. ex., c.-à-d. |
| launcher / library / settings | lanceur / bibliothèque / paramètres | launcher, réglages (for the page) |
| cover / background / artwork | jaquette / arrière-plan / illustrations | cover, fond |
| placeholder image | image de remplacement | placeholder |
| info, info panel | infos, panneau d’infos | détails, infobulle |
| on-screen keyboard / mouse mode | clavier virtuel / mode souris | — |
| your own | votre propre, personnalisé (custom image, own file: fichier personnel) | — |
| keys | Entrée, Échap, Maj, Espace, Suppr | Enter, Esc, Shift |
| units | Mo, Go, s, min, h | MB, GB |

Third-party UI terms the player has to find in another program stay in that program's language
("Enable device hiding", "Inverse application cloak", SteamGridDB's "Preferences › API").

## Typography

- Quotes « … » with a no-break space (U+00A0) inside; apostrophe ’.
- A no-break space before : ; ! ? (U+00A0, not the narrow one, which not every font has); none in times, drive
  letters, URLs and ratios ("21:58", "C:", "16:9").
- En dash with spaces (" – ") as in German; ellipsis without a space before ("Chargement…").
- Numbers and dates follow the system's region, not the UI language; units keep a space ("12 Mo", "30 %").

The CI check `UI texts` (`tools/ci/check-ui-texts.sh`) finds per language: straight quotes and apostrophes outside
markup, “…” or „…“, « » without their no-break space, a normal space before : ; ! ?, "...", " - ", a space before "…",
"émuler", "p. ex.", "redémarrer Windows", "Steam-Input" and German letters.
