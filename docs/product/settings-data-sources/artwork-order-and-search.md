# Product decisions: "Datenquellen", artwork order, switches and the search

Index and the category's frame: [README.md](README.md).

## Artwork order, the switches and the search (#489, #506)

- Group "REIHENFOLGE FÜR ARTWORK": "1. Launcher-Bilder", "2. SteamGridDB" (default), each a switch An/Aus (◀ ▶ or A;
  default on). L3 (C) lifts a source, ▲ ▼ move it (no wrap), L3 drops it, B puts it back without leaving the page; the
  mouse drags it, a click switches it; as in the launcher list. Order and switches are saved in `settings.json`.
- A source switched off is never asked; its automatic images disappear everywhere at once but stay on disk (one per game
  and kind, the one shown last), so switching it on brings them back without a download. Images set by hand stay. Both
  off: placeholders, except images set by hand. SteamGridDB off: no search by name, "Warten auf deine Auswahl" greyed
  out with its count, "Jetzt suchen" greyed out with the reason in the info box, the key stays stored, a first key
  starts no search; "Artwork ändern" offers none of an off source's images and its notice says it is off and leads to
  that source's row.
- Formats: a cover 2 : 3; a background 16 : 9, the full-screen detail page's shape, or flat (about 3.1 : 1, Steam's and
  SteamGridDB's), which the detail page crops to 16 : 9 (#562). Of one source's images whose sizes are known before the
  download (SteamGridDB's), a 16 : 9 background comes before a flat one.
- An image fits when a centred crop to its format cuts away at most 15 %, for covers and backgrounds alike; there is no
  setting for it (#566: Steam's and SteamGridDB's covers are exactly 2 : 3, so a setting would only choose between Epic's
  3 : 4 covers, which lose about 11 %, and SteamGridDB's). Each image is checked in the order; the first that fits wins,
  none fitting: the first source with any image, cropped (an image SteamGridDB gave stands as its image then).
- Changing the order or a switch starts no search: the images on disk and the launchers' own are picked again at once;
  SteamGridDB is asked in the new order for new games and at the next "Jetzt suchen".
- SteamGridDB is asked only for a game and kind whose current image doesn't fit, so a key arriving later also
  replaces fallback images (e.g. a 21 : 9 background), never an image set by hand; an image it gave holds SteamGridDB's
  place in the order. It is asked by the launcher's id (Steam's AppID, Epic's `AppName` on `egs`); a game without one, or
  whose id SteamGridDB doesn't know, is searched by name (below), also in launchers without images of their own. Of its
  images the best-rated one that fits (static only, no NSFW or humour) is downloaded; only one per game and kind.

## When Arcadia searches (#506)

- On its own only for a game new to the library, once, silently in the background; never again on its own, also when
  the network was missing. Rescans (Arcadia back in front, a game ended, an install) send no request at all, to no
  source (also no launcher image to download); a launcher's local files are still read at every scan. A game
  arriving without a usable key or with SteamGridDB off counts as seen and waits for the first key or "Jetzt suchen".
  The first start with this version counts every game in the library as known; a game leaving and coming back is not
  new and comes back with its images (they stay on disk while it is gone). Closing Arcadia during such a run: the games it didn't reach are searched after the next start.
- The first usable key (none or a rejected one before, also when checked without network) searches once every game
  still missing images: "Schlüssel gespeichert. Fehlende Bilder werden gesucht." Replacing a working key starts nothing.
- "Fehlende Cover und Hintergründe" counts the games still without ("N Spiele ohne Cover, M ohne Hintergrund"), and
  while a search runs (any of them) shows "Suche läuft · n von N Spielen" with a progress bar. A ("Jetzt suchen")
  searches every game missing an image, also those SteamGridDB answered before. During a search it reads "Suche
  abbrechen", during "Alle Artworks neu laden" that row reads "Neu laden abbrechen"; A stops the run after the request
  in flight. What was found stays, also games put on the waiting list; the rest waits for the next "Jetzt suchen".
- Only a run the player started says what it found and warns when SteamGridDB couldn't be reached; others stay silent.
- Throttling waits as SteamGridDB asks (at most 60 s) and goes on; no answer within 10 s, no network or a server
  error stops SteamGridDB for the rest of the run. Never one request per game for a key that is rejected or a service
  that is down. `arcadia.log` gets a line at a search's start and end (trigger, number of games), never key or names.

## Images set by hand and "Alle Artworks neu laden" (#490)

- Group "ARTWORK" also holds "Alle Artworks neu laden" (below the missing images) and, indented right under it, the
  switch "Von Hand gesetzte Artworks überschreiben" (default off, saved in `settings.json`). "Neu laden" asks first, with the text matching the
  switch; B changes nothing. It runs like "Jetzt suchen" over every game, also asking SteamGridDB again, keeps images
  set by hand unless the switch is on, and is greyed out while a search runs; while it runs ("Neu laden läuft · n von
  N") it reads "Neu laden abbrechen".
- With a usable key "Artwork ändern" offers SteamGridDB's images (by the launcher's id, in SteamGridDB's order; its API rates every image 0, so no rating is shown) after the
  launcher's; without a key or with a rejected one its notice leads by "Zu den Datenquellen" to the row "Schlüssel".
