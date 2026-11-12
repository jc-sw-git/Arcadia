# Product decisions: "Datenquellen", IGDB

Index and the category's frame: [README.md](README.md). The merge and the order: [metadata.md](metadata.md).

## Access (#494)

- Group "IGDB": "Client-ID" and "Client-Secret" (A "Eingeben"/"Bearbeiten": the on-screen keyboard, the field starts
  empty; an empty field or B changes nothing), each with an indented "Aus der Zwischenablage einfügen", then "Zugang
  anlegen", "Verbindung testen", "Zugang löschen" (greyed out without access, still focusable). The rows show only the
  values' last four characters.
- Each value is 20 to 64 letters and digits after trimming; anything else is refused before any request. The access
  counts only with both; it is checked (one token request to Twitch) as soon as both are there and after each change;
  "Verbindung testen" checks again, 10 s at most. No answer or no network keeps the access, without a warning.
- "Zugang anlegen" opens Twitch's developer console (fixed in code) in the default browser, the mouse mode on while it
  is in front, also with "Mausmodus: Aus". Creating the application there needs the mouse, the only part not
  controller only.
- Storage, logging and the rejected access as for the SteamGridDB key ([steamgriddb-key.md](steamgriddb-key.md)):
  DPAPI on Windows, memory only on Linux; never logged; a rejected access warns at the tab, the row ("Zugang ungültig ·
  IGDB wird übersprungen") and the status chip, and nothing is sent with it until it changes. The token stays in
  memory and is renewed once when IGDB turns it down. The first usable access loads what is missing at once.
- No access: not one request to Twitch or IGDB, no message.

## Fetching and matching (#494)

- After the launchers' stores in every run, for the games IGDB hasn't answered: at most 4 requests per second, 10 s
  each; 429 waits and goes on. No network, a timeout, a server error, a broken answer or a rejected access leave IGDB
  out for the rest of the run (one log line); the next scan tries again.
- A Steam game is asked by its AppID only (`external_games`, source Steam); an AppID IGDB doesn't know gives no IGDB
  data and no name search. Every other game by its shown launcher title without ™ ® ©, once per game and run.
- Main games are IGDB's types main game, remake, remaster, expanded game, port and standalone expansion; DLC,
  expansions, bundles, mods, episodes, seasons, packs, updates and forks are dropped before hits are counted. A hit
  whose name is the same (case, punctuation and ™ ® © ignored, each `?` one character) is taken when it is the only
  one; one main game left of any name is taken too; two equal or several others are several hits (at most 8, IGDB's
  order), handled by "Bei mehreren Treffern" (shared with the artwork, waiting list: #494 part 2); no hit: launcher
  data, searched again at the next scan. A game matched once is asked by its IGDB id from then on.
- IGDB's fields: summary as description (English), genres onto the German list (Action from the theme "Action"),
  first release date, developers and publishers from the involved companies, USK/PEGI/ESRB by organization and rating
  category, themes and keywords as provider tags.
