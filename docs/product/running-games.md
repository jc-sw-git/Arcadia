# Product decisions: Running games

## Seeing a running game (#95, owner 2026-10-20 and 2026-10-21)

- Every running game shows "Läuft", started from Arcadia or in its launcher. The launcher tells
  (`IRunningGamesMonitor`: Steam's registry, Epic's log), else a process from the install folder (`ProcessGames`, only
  for launchers without a monitor, never on the UI thread).
- Asked every 2 s, also with Arcadia in the background, since it may be seen on a second screen (owner, 2026-10-21).
- The cover gets a "Läuft" badge (one badge per cover: while the launcher installs or updates the game, e.g. an update
  before the start, the cover shows that progress instead, #167); preview and detail page say "Läuft seit HH:MM",
  likewise the search hits and the context menu.
- A on a running game is "Zurück ins Spiel" (its window comes to the front, a toast if it has none); several games may
  run at once.
- While it runs or starts, "Deinstallieren …" and the game's settings that only apply at its next start (the Steam
  Controller mode) are disabled with the reason in the tooltip; "Individueller Mausmodus" applies at once and stays.

## Starting

- From A until it is seen running a game shows "Wird gestartet …" (30 s at most, owner 2026-10-28). While the launcher
  installs or updates the game meanwhile, it stays for as long as that runs (#167); if the game doesn't run 10 s after the
  update, Arcadia starts it once more, since Steam doesn't always do it, and waits 30 s again (owner 2026-10-28, #278).
  A second A then only toasts.
- A start that ends by the 30 s timeout (any launcher) toasts „<Spiel>“ wurde nicht gestartet. and leaves Arcadia where
  it is.

## A start the launcher aborts (#277, owner 2026-10-27)

- When the launcher itself aborts the start (Steam: `LaunchApp failed`, e.g. the signed-in account owns no license),
  "Wird gestartet …" ends at once (within 5 s), Arcadia comes to the front as after a game's end (#94) and a toast names
  the reason: a known code its own text („3000th Duel“: Das angemeldete Steam-Konto besitzt dieses Spiel nicht.), any
  other „<Spiel>“: <Launcher> hat den Start abgebrochen.
- The launcher's own window (e.g. Steam's error dialog) stays open; Arcadia doesn't close other programs' windows.
- The launcher waiting for the player (launch option, EULA, cloud conflict) or updating the game first (Steam's
  `AppError_18`) is no failure. Only an explicit abort ends a start early, never a missing signal.
- After an aborted start SISR comes back at once (it paused for the game).
- No warning before a start (e.g. another account's game): the launcher decides.

## After the end

- The library is read again (playtime, "Zuletzt gespielt").
- Only a game started from Arcadia brings Arcadia to the front when it ends (#94), 1 s later, and only if that game,
  its launcher (`ProgramNames`), the desktop or no window is in front then; another program keeps the front.
