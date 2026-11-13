# Product decisions: Game start, detail page and game actions

| Topic | File |
|---|---|
| The page: title, buttons and note, the panel's areas, "Übersicht", "Infos", "Übersicht anpassen", rows and info box | [page.md](page.md) |
| The carousel through the tab's games, LT/RT, LB/RB, filter, wheel | [carousel.md](carousel.md) |
| "Artwork ändern", "Titel ändern", "Tags" | [game-actions.md](game-actions.md) |

## Game start

- A starts a game on release; holding A for 1 s opens the detail page (a ring around the preview fills from
  0.2 s on, letting go before 1 s cancels). With the mouse a click picks a cover, a double click starts it and
  holding the left button 1 s opens the details with the same ring. The context menu and the detail page offer the same actions
  (`GameActionViewModel.For`); the detail page has no menu (#499); its layout: [page.md](page.md).
- Launcher actions (open in the launcher, uninstall) are `LaunchTarget`s on `Game`;
  a null one is shown disabled with the reason, or for uninstall opens the launcher instead.
- If an update is available, A still says "Spielen" and starts the game; the launcher installs the update
  first. The update shows as an icon, and the detail page's note says that the launcher may update first. Where the launcher's install monitor tells (Steam), the icon and the note appear and go live within a poll,
  without a rescan and without a toast (#288). Launchers have no update-only command: Steam ignores `steam://install` for
  installed games and `steam://validate` verifies every file first; comparable products offer none either.
- What A says depends on the entry's type (#141): "Spielen" for a game, "Tüfteln" for software and
  tools, "Abrocken" for a soundtrack; the same text on the button, in the context menu and in the button hint. Not
  installed it says "Installieren". While software or a tool runs, A says "Weiter tüfteln" instead of "Zurück ins
  Spiel". A soundtrack is handed to its launcher, so it gets no "Startet" or "Läuft" state: Steam
  plays it and, a moment later, also shows the soundtrack's page, as "In Steam öffnen" does. Its detail page notes "Steam spielt den Soundtrack ab." under "Spielen".
- After "Installieren" the game shows "Bitte in … bestätigen" (an Epic engine: "Bitte in … starten") for 1 minute,
  then "Installieren" again: launchers don't report a cancelled install dialog, and a later confirm still shows its
  progress. A on a waiting game opens the launcher's install dialog again and restarts the minute (#343).
