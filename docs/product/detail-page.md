# Product decisions: Game start, detail page and game actions

Agreed with the owner; don't change without asking. Read this before changing this area
(index in `CLAUDE.md` → Product decisions).

- A starts a game on release; holding A for 1 s opens the detail page (a ring around the preview fills from
  0.2 s on, letting go before 1 s cancels). With the mouse a click picks a cover, a double click starts it and
  holding the left button 1 s opens the details with the same ring (owner, 2026-10-19). The context menu and the detail page offer the same actions
  (`GameActionViewModel.For`); the detail page has no menu, its actions are buttons (owner, 2026-10-17) in three rows:
  Spielen, Zu Favoriten, In … öffnen, Artwork ändern; then the game's own settings (Steam Controller mode,
  "Individueller Mausmodus", later e.g. launch options); last "Deinstallieren …" alone (owner, 2026-10-21).
- The detail page is a carousel through the tab's games in the library's sort order, across pages and
  wrapping at the ends (two games: the other on both sides; one: no neighbours). LT/RT turn it, LB/RB switch
  the tab and stay on the detail page (first game), Y re-sorts with the shown game kept in the middle, B returns
  to the library page of the game shown last (owner, 2026-10-17). X opens the search over it; B in the search
  returns to it (#127). Below the buttons only "Spielen" has a note (update, installation); a disabled button tells
  why in its tooltip, since the focus skips it (owner, 2026-10-21).
- Launcher actions (open in the launcher, uninstall) are `LaunchTarget`s on `Game`;
  a null one is shown disabled with the reason, or for uninstall opens the launcher instead.
- If an update is available, A still says "Spielen" and starts the game; the launcher installs the update
  first. The update shows as an icon, and the detail page says below the button that the launcher may update
  first (owner, 2026-10-12). Launchers have no update-only command: Steam ignores `steam://install` for
  installed games and `steam://validate` verifies every file first; Playnite offers no update action either.
- What A says depends on the entry's type (#141, owner 2026-10-26): "Spielen" for a game, "Tüfteln" for software and
  tools, "Abrocken" for a soundtrack; the same text on the button, in the context menu and in the button hint. Not
  installed it says "Installieren". While software or a tool runs, A says "Weiter tüfteln" instead of "Zurück ins
  Spiel" (owner, 2026-10-27). A soundtrack is handed to its launcher, so it gets no "Startet" or "Läuft" state: Steam
  plays it and also shows the soundtrack's page, as "In Steam öffnen" does (owner, 2026-10-27; controlling the music
  from Arcadia is its own story). Its detail page notes "Steam spielt den Soundtrack ab." under the actions.
