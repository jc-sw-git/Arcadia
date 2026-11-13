# Product decisions: "Datenquellen", metadata

Index and the category's frame: [README.md](README.md). The specification is #43; Steam's and Epic's data:
`docs/launchers/steam/metadata.md`, `docs/launchers/epic.md`.

## Where metadata comes from (#43, #493)

- Providers: each launcher with metadata (Steam: `appinfo.vdf` and the store page `appdetails?l=german`, no key;
  Epic's catalog: only the developer) and IGDB with the player's access (#494, [igdb.md](igdb.md)). Fields are merged
  per field in the order of "Reihenfolge für Metadaten"; a field missing from one comes from the next, and each keeps
  the provider it came from (shown only in "Infos").
- Group "REIHENFOLGE FÜR METADATEN": "1. Launcher-Daten", "2. IGDB" (default), each a switch An/Aus, moved and
  switched as the artwork's sources (L3, ▲ ▼, B, the mouse drags), saved in `settings.json`. A change merges every game
  again from the cache at once, without a request; a source switched off is never asked and its fields and corrected
  titles disappear (the cache keeps them). IGDB without a usable access is greyed out ("Wird übersprungen …").
- Doubtful titles: a launcher title with `?` (Epic's ASCII-only catalog) is replaced by IGDB's title regardless of the
  order, while IGDB is on; a title without `?` stays the launcher's. The broken title is listed only in "Infos",
  beside the corrected one. Own titles (#496) stand above both.
- Language rule: a description that isn't German, or has a `?` inside a word, counts only when no provider has a German
  one; a broken one only when there is no other.
- Genres: Arcadia's fixed German list (Action, Abenteuer, Rollenspiel, Strategie, Simulation, Rennspiel, Sport, Shooter,
  Puzzle, Plattformer, Kampfspiel, Gelegenheitsspiel, Indie, MMO, Arcade, Musik, Point-and-Click, Visual Novel, Karten-
  und Brettspiel, Taktik); unmapped genres are dropped. Provider tags are taken as delivered.
- Age ratings USK, PEGI, ESRB as delivered, unknown values dropped, merged per board (#541): each board's rating comes
  from the first provider in the order that has it, so IGDB adds the boards Steam lacks. Without a real USK from any
  provider, Steam's own rating for Germany counts as USK. Every rating names its provider the same way ("Steam", "IGDB").
- When: after every scan, in the background, for the entries shown (a switched-off type asks nothing); one request at a
  time within the store's limit (Steam: 180 per 5 minutes, below its 200), 10 s each, HTTP 429 waits and continues. No
  network, a timeout or a server error skip the game silently until the next scan. Nothing is fetched before the first
  scan; what is cached shows at once and offline. No toast.

## Rows in group "METADATEN" (#493)

- A row per launcher that delivers metadata: subtitle what it delivers, info text where it comes from, the store's
  limit and what the store learns (Steam: only the AppID).
- "Fehlende Metadaten": while a run goes "412 von 1.000, noch etwa 20 Minuten" ("noch etwa 1 Minute", "weniger als
  eine Minute") with a bar; else the counts; none missing "Alle Metadaten geladen", greyed; before the first scan
  "Arcadia lädt nach dem Einlesen von selbst". The estimate is the longer of the store's gap and the rate seen.
- "Alle Metadaten neu laden" asks first ([B] or "Abbrechen" sends nothing); the old data stays until replaced; own
  titles and tags stay. While it runs it is greyed out ("Neu laden läuft"), a second A starts nothing. Reaching no
  store at all: one warning; a background run says nothing.
