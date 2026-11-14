# Product decisions: Spanish UI texts

The Spanish translation of every text (`*Texts.es.resx`, `docs/architecture/ui-texts.md`). The rules of `ui-texts.md`
hold in Spanish too (no "we", subtitle on one line, subtitle and info never repeat each other, the PR lists changed
texts); this file adds what differs. Every new or changed German text gets its Spanish one in the same PR.

- Spanish of Spain (es-ES) as the base, words understood in Latin America too where one exists; the player is *tú*
  (“Elige”, “tus juegos”), never *usted*.
- Sentence case for titles, rows and buttons (“Aplicar idioma”, “Reiniciar el PC”); group headings in capitals as in
  German (“IDIOMA”). Language names are lowercase inside a sentence (“El idioma del sistema es italiano”).
- Arcadia is feminine when it acts (“por sí misma”), as *la aplicación*.
- Controller buttons as in German: ⧉ and ≡, the others in brackets (“[A]”, “[LB]”); `ButtonGlyphTextTests` checks LB,
  RB, LT, RT, L3, R3, LS, RS and D-Pad, not A, B, X, Y (“a” and “y” are words in Spanish).
- Names of other programs' settings and tabs stay as those programs show them (“Enable device hiding”, “Devices”).

## Glossary

| Term | Use | Instead of |
|---|---|---|
| mando real / mando virtual | the player's pad / the copy games see; verb *virtualizar* | control, emular |
| emulación, emulación de mandos | the process as a whole (noun only) | — |
| controlador | a driver (usbip, HidHide) — never the pad | driver |
| guardián de mandos (Pan) | first mention on a page; after that *Pan* | servicio vigilante |
| Steam Input | Steam's controller mode | — |
| escanear, volver a escanear | reading a launcher's games (again) | actualizar (for a rescan) |
| actualización, actualizar | a new version (Arcadia, Pan, drivers, games, firmware) | — |
| reiniciar el PC, reinicio del PC | restarting the machine | reiniciar Windows, ordenador |
| solicitud de administrador, pide permisos de administrador | the system's prompt for admin rights | UAC |
| launcher | a launcher, as in the product's vision | lanzador |
| imágenes (portada y fondo) | artwork | arte, artwork |
| etiquetas | tags | tags |
| distribución | a keyboard or controller layout; Steam's own: *configuración de Steam* | layout |
| modo ratón | mouse mode | modo mouse |
| ajustes | settings | configuración (for Arcadia's settings) |
| por ejemplo | — | p. ej. or any other abbreviation (units like h, min, MB, % stay) |
| propio, tu propio | what the player set (tu propio título, imagen propia) | personalizado |
| info, panel de información | the panel beside a row | detalles, tooltip |

## Typography

- Questions and exclamations open with ¿ and ¡ (“¿Reiniciar el PC?”, “¡Todo listo!”).
- Quotes «…», inside them “…”; apostrophe ’ (only in names).
- En dash with spaces (" – ") as in German.
- Ellipsis without a space before (“Cargando…”, “Subir…”).
- Numbers and dates follow the system's region, not the UI language; units keep a space ("12 MB", "30 %").

The CI check `UI texts` (`tools/ci/check-ui-texts.sh`) finds the forbidden spellings: straight quotes, "...", " - ", a
space before "…", German quotes or letters, “ ” outside «…», a “?” or “!” without its opening sign, *emular*,
*p. ej.*, *reiniciar Windows*, *usted*, *ordenador* and *lanzador*.
