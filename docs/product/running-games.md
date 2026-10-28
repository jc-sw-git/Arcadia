# Product decisions: Running games

Agreed with the owner; don't change without asking. Read this before changing this area
(index in `CLAUDE.md` → Product decisions).

- Running games (#95, owner 2026-10-20 and 2026-10-21): every running game shows "Läuft", started from Arcadia or in its
  launcher; the launcher tells (`IRunningGamesMonitor`: Steam's registry, Epic's log), else a process from the install
  folder (`ProcessGames`, only for launchers without a monitor, never on the UI thread). Asked every 2 s, also with
  Arcadia in the background, since it may be seen on a second screen (owner, 2026-10-21). From A until it is seen running
  a game shows "Wird gestartet …" (30 s at most, owner 2026-10-28; while the launcher installs or updates the game
  meanwhile, for as long as that runs, #167; if the game doesn't run 10 s after the update, Arcadia starts it once more,
  since Steam doesn't always do it, and waits 30 s again, owner 2026-10-28, #278); a second A then only toasts. A on a running game is "Zurück ins
  Spiel" (its window comes to the front, a toast if it has none); several games may run at once. The cover gets a
  "Läuft" badge (one badge per cover: while the launcher installs or updates the game, e.g. an update before the start,
  the cover shows that progress instead, #167), preview and detail page "Läuft seit HH:MM", likewise the search hits and the context menu. While it
  runs or starts, "Deinstallieren …" and the game's settings that only apply at its next start (the Steam Controller
  mode) are disabled with the reason in the tooltip; "Individueller Mausmodus" applies at once and stays. After the end
  the library is read again (playtime, "Zuletzt gespielt"). Only a game started from Arcadia brings Arcadia to the front
  when it ends (#94), 1 s later, and only if that game, its launcher (`ProgramNames`), the desktop or no window is in
  front then; another program keeps the front.
- A start that doesn't run (#277, owner 2026-10-27): when the launcher itself aborts the start (Steam: `LaunchApp failed`,
  e.g. the signed-in account owns no license), "Wird gestartet …" ends at once (within 5 s), Arcadia comes to the front
  as after a game's end (#94) and a toast names the reason: a known code its own text („3000th Duel“: Das angemeldete
  Steam-Konto besitzt dieses Spiel nicht.), any other „<Spiel>“: <Launcher> hat den Start abgebrochen. The launcher's
  own window (e.g. Steam's error dialog) stays open; Arcadia doesn't close other programs' windows. The launcher waiting
  for the player (launch option, EULA, cloud conflict) or updating the game first (Steam's `AppError_18`) is no failure. Only an explicit abort ends a start early, never a
  missing signal; a start that ends by the 30 s timeout (any launcher) toasts „<Spiel>“ wurde nicht gestartet. and
  leaves Arcadia where it is. After an aborted start SISR comes back at once (it paused for the game). No warning before a start (e.g. another account's game): the launcher decides.
