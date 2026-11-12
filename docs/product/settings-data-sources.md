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
  place in the order at later scans. It is asked by the launcher's id (Steam's AppID, Epic's `AppName` on `egs`); a game
  without one, or whose id SteamGridDB doesn't know, is searched by name (below), also in launchers without images of
  their own. Of its images the best-rated one that fits (static only, no NSFW or humour) is downloaded;
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

## Search by name and "Warten auf deine Auswahl" (#491)

- The name sent is the shown title without ™ ® ©, trimmed, once per game and run. A hit "matches" when its name is the
  same ignoring case, punctuation and ™ ® ©. One matching hit, or one result at all, is taken without a question;
  several matching, or none matching but several results, are "several hits" (at most 8, SteamGridDB's order). No
  result: the launcher image or the placeholder, searched again at the next scan.
- "Bei mehreren Treffern" (group "ARTWORK"): "Selbst auswählen" (default) puts the game on the waiting list, "Ersten
  nehmen" takes the first hit. Switching to "Ersten nehmen" while games wait gives each its first hit at once (one
  toast); switching back puts none of them back.
- Before a hit is offered or taken, SteamGridDB is asked whether it has an image of the kind sought (#506); a hit
  without one is dropped, and the rules above count only the hits left (one left: taken; none: no result). "Sought" is
  what the game is missing or has that doesn't fit and wasn't set by hand: cover, background or both; with both a hit
  needs a cover. A failed check stops SteamGridDB for the run (one request, not one per hit).
- Indented below, always shown, "Warten auf deine Auswahl" with the count; greyed out with no game waiting or with
  "Ersten nehmen". A opens the screen with two levels. Games list (left; name and launcher, no hit count since hits drop out as they are checked): ▲ ▼ choose a game, round at the ends; A goes
  into its hits; B closes (undecided games stay in their order). Hits (right): "Gesucht: Cover und Hintergrund" /
  "Gesucht: Cover" / "Gesucht: Hintergrund" (decided again when shown), the field "Eigener Suchbegriff" with
  "Zurücksetzen", and a grid of the hits' images of that kind (cover when both; five covers or three backgrounds a row; names wrap to two lines, the focused hit shows its name in full, a mouse hover shows it as a tooltip).
  ◀ ▶ ▲ ▼ go round in rows and columns, the field's row counts as each column's top; A takes the hit and goes back to
  the games list on the next waiting game, B goes back without; Y skips the game on both levels. Until chosen a game
  keeps its launcher image or placeholder.
- Hits not yet checked (a waiting list from before #506, a failed check) are checked when the game is shown, never in
  the background: "Treffer werden geprüft …" while they are, "Keine Treffer" when none is left (the game stays until
  skipped). Checks and images start only once the focus rests on a game (150 ms); moving on cancels them.
- "Eigener Suchbegriff" shows the game's search name; A opens the on-screen keyboard prefilled, ↵ searches, B keeps the
  term. Its results (the first 25 with an image of the sought kind) replace the hits; "Zurücksetzen" (greyed out with
  the search name in the field) brings back the search name and its hits. The term isn't stored. Without network
  "SteamGridDB nicht erreichbar", with a rejected key the key notice.
- The games list and the grid show a scroll bar and fade at the edge where more follow.
- A chosen hit is the game's SteamGridDB game from then on; its images are automatic (order, "Zuschneiden erlaubt" and
  "Alle Artworks neu laden" apply), an image set by hand stays. A skipped game is never searched by name again.
  "Artwork ändern" offers the chosen game's images; for a waiting or skipped game none from SteamGridDB.
- The choice, a skip and the waiting list are kept per game across restarts and a game leaving the library and coming
  back.
