# Product decisions: Settings, category "Datenquellen"

The page and its status messages: [settings.md](../settings.md). Where covers and backgrounds come from, their formats
and the crop rule: #13. Order of the categories: Launcher, Controller, Datenquellen, Lokalisierung, Über Arcadia.

## Tabs "Artwork" and "Metadaten" (#538)

- "Datenquellen" stays one category; above its rows two tabs with an icon each: "Artwork" (a picture: groups
  "STEAMGRIDDB", "REIHENFOLGE FÜR ARTWORK", "ARTWORK") and "Metadaten" (a page of text: "IGDB", "REIHENFOLGE FÜR
  METADATEN", "METADATEN", "EIGENE DATEN"). LT/RT (Q/E) or a click switch them, at the tab's first row; the footer
  names LT/RT "Tab". Choosing the category opens "Artwork". Not while a value is written or a source is lifted.
- A rejected key or access (or an unreadable `user-data.json`) shows the warning beside its tab's name and beside the
  category's. A status entry, "Artwork ändern" or "Eigene Tags" open the tab that has their row.
- Only the shown tab's rows are drawn, so opening the category costs no more than the others.
- Demo games (`--demo`) are ignored by every data source: SteamGridDB and IGDB are never asked for them, none waits
  for a choice, they count neither as missing nor in a search; their launcher's own images and data still show, and
  IGDB data cached for them earlier doesn't.

## Without internet (#683)

- A search or load the player starts checks the connection first, at most 3 s, the row showing that it works (A again
  stops the check): "Jetzt suchen" and "Alle Artworks neu laden" (tab "Artwork"), "Jetzt suchen" and "Alle Metadaten
  neu laden" (tab "Metadaten"), an own term in "Warten auf deine Auswahl", and opening "Artwork ändern". The check asks
  only the hosts the work would ask (SteamGridDB, IGDB, one image address and one store page per launcher).
- Without a connection a warning toast "Keine Internetverbindung. Prüfe die Verbindung und versuche es dann noch
  einmal." (no status message) and nothing starts; "Artwork ändern" then offers what is on this PC, the waiting list
  shows "… nicht erreichbar".
- A run that loses the connection checks once at the first failed host and then asks nothing more online, so no
  timeout follows another; what is on this PC is still copied. Started by the player it ends with the same toast,
  instead of the source's own warning; a background run only logs it. A single source down while the internet is
  there keeps its own warning (SteamGridDB, the store).

| Topic | File |
|---|---|
| SteamGridDB key: entry, paste, test, delete, the warning (#489) | [steamgriddb-key.md](steamgriddb-key.md) |
| Artwork order, sources on and off, the fixed crop rule, when Arcadia searches, "Jetzt suchen", "Alle Artworks neu laden" (#489, #490, #506) | [artwork-order-and-search.md](artwork-order-and-search.md) |
| Search by name and "Warten auf deine Auswahl" (#491, #506) | [waiting-list.md](waiting-list.md) |
| Metadata: providers, merge, order, doubtful titles, language rule, genres, age ratings, when it loads, the rows of "METADATEN" (#43, #493, #494) | [metadata.md](metadata.md) |
| IGDB: access, fetching, matching by AppID and by name (#494) | [igdb.md](igdb.md) |
| Own tags: the tag window of a game, the tag list "Eigene Tags", names, merge, delete (#495) | [own-tags.md](own-tags.md) |

Group "EIGENE DATEN" (tab "Metadaten"; #496, #495): always "Eigene Tags (N)"; only while `user-data.json` was unreadable at the start, the row "Eigene Titel" with the
warning, why (damaged, locked, from a newer Arcadia) and what to do; the header's entry opens it.
