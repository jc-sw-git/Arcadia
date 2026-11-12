# Product decisions: "Datenquellen", the SteamGridDB key

Index and the category's frame: [README.md](README.md).

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
