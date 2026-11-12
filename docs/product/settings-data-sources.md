# Product decisions: Settings, category "Datenquellen"

The page and its status messages: [settings.md](settings.md). Where covers and backgrounds come from, their formats
and the crop rule: #13. Order of the categories: Launcher, Controller, Datenquellen, Über Arcadia.

## SteamGridDB key (#489)

- Group "STEAMGRIDDB": row "Schlüssel" (A "Eingeben"/"Bearbeiten": the on-screen keyboard, the field starts empty;
  an empty field taken changes nothing), indented below it "Aus der Zwischenablage einfügen", "Schlüssel-Seite öffnen",
  "Verbindung testen" and "Schlüssel löschen" (greyed out without a key). The row shows only the key's last four
  characters ("Verbunden · endet auf …3f9a").
- A key is 32 letters and digits; spaces and line breaks around it are trimmed, anything else is refused before any
  request. A key typed or pasted is saved and checked at once (one request); "Verbindung testen" checks again,
  10 s at most. No answer or no network keeps the key without a warning.
- "Schlüssel-Seite öffnen" opens SteamGridDB's API page (fixed in code) in the default browser and turns the mouse mode
  on while it is in front, also with "Mausmodus: Aus"; signing in and copying the key there needs the mouse, the
  only part of the flow that isn't controller only.
- Storage: Windows keeps the key encrypted for the Windows user (DPAPI) in Arcadia's data folder, never in
  `settings.json`; a stored key that can't be decrypted counts as no key. Linux keeps it only until Arcadia closes,
  like the Steam sign-in. The key is never logged, never in a toast, a message or an exception text.
- No key: no request at all and no message, SteamGridDB in the order greyed out ("Wird übersprungen …").
  A key SteamGridDB rejects (at entry, the test or during a search): a warning at the category's tab, the row
  ("Schlüssel ungültig · SteamGridDB wird übersprungen") and the header's status chip, whose entry opens the row; no
  further request with it until a new key. A new valid key or deleting it ends the warning.

## Artwork order and the search (#489)

- Group "REIHENFOLGE FÜR ARTWORK": "1. Launcher-Bilder", "2. SteamGridDB" (default). A or a click on a source
  ("Verschieben ⇅") moves it one place up, the first one down; the focus stays on it. Saved in `settings.json`.
- "Zuschneiden erlaubt" (group "ARTWORK"): Wenig · bis 5 %, Normal · bis 15 % (default), Viel · bis 30 %. Each image
  is checked against it in the order; the first that fits wins, none fitting: the first source with any image, cropped.
- SteamGridDB is asked only for a game and kind whose current image doesn't fit, so a key arriving later also
  replaces fallback images (e.g. a 16 : 9 background), never an image set by hand; an image it gave holds SteamGridDB's
  place in the order at later scans. It is asked by the launcher's id (Steam's AppID); games without one wait for the
  search by name (#13). Of its images the best-rated one that fits (static only, no NSFW or humour) is downloaded;
  only one per game and kind.
- After every scan a background run fills what is missing; "Fehlende Cover und Hintergründe" counts the games still
  without ("N Spiele ohne Cover, M ohne Hintergrund"), shows "Suche läuft · n von N Spielen" with a progress bar, and A
  ("Jetzt suchen") starts it at once, asking SteamGridDB again for games it answered before. Only a run the player
  started says what it found and warns when SteamGridDB couldn't be reached; a background run stays silent.
- Throttling waits as SteamGridDB asks (at most 60 s) and goes on; no answer within 10 s, no network or a server
  error stops SteamGridDB for the rest of the run, asked again at the next scan. Never one request per game for a key
  that is rejected or a service that is down.

## Images set by hand and "Alle Artworks neu laden" (#490)

- Group "ARTWORK" also holds "Alle Artworks neu laden" (below the missing images) and the switch "Von Hand gesetzte
  Artworks überschreiben" (default off, saved in `settings.json`). "Neu laden" asks first, with the text matching the
  switch; B changes nothing. It runs like "Jetzt suchen" over every game, also asking SteamGridDB again, keeps images
  set by hand unless the switch is on, and is greyed out while a search or reload runs ("Neu laden läuft · n von N").
- With a usable key "Artwork ändern" offers SteamGridDB's images (by the launcher's id, best rated first) after the
  launcher's; without a key or with a rejected one its notice leads by "Zu den Datenquellen ›" to the row "Schlüssel".
