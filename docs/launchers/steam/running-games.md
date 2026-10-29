# Steam — running games (#95)

`SteamRunningGames` reads `HKCU\Software\Valve\Steam\Apps\<appid>\Running` (1 while the game runs) and
`RunningAppID` (the game Steam started last, while it runs; it counts even without the flag). There
are thousands of `Apps` keys (every app Steam ever touched), so only a poll after a pause of more than 10 s (Arcadia
just started, or the PC slept) reads them all; the polls in between read `RunningAppID` and the keys
of the games already running. A game that starts meanwhile becomes `RunningAppID`. "Läuft seit" is the last write
time of the app's key, read once per run (`RegQueryInfoKey`). Steam sets `Running` to 1 while the game runs and back
to 0 when it ends, also when the game is killed in Task Manager. **But the flag can stick:** an uninstalled game on the
owner's PC still had `Running` = 1 a year after its last start; cause unknown (not `steam://rungameid` on an uninstalled
game, which opens the install dialog and leaves `Running` at 0). A stale flag would show such a game as running for
good once it is installed again (#182).
Play stats: Steam writes `LastPlayed` into the manifest at the start and the account's `localconfig.vdf` at some point after the end; the scanner takes the later of both,
and Arcadia rescans right after a game ended and 30 s later.

## Starts Steam aborts (#277)

Steam logs every step of a game start to `logs/console_log.txt` (local time, English, appended, CRLF), e.g.
`[2026-10-27 17:27:35] GameAction [AppID 214490, ActionID 1] : LaunchApp failed with AppError_5 with ""` within a
second of `steam://rungameid/<id>`, after `changed task to RequestingLicense` and before `changed task to Failed`.
Codes seen: `AppError_5` = no license (Steam's dialog "Keine Lizenzen"), `AppError_28` with the exe path (presumably
missing executable), `AppError_18` = update required: Steam starts the game's update 4 s later on its own
(`content_log.txt`), so it is no refusal for Arcadia
and "Wird gestartet …" waits for the update (#167). Steam waiting for the player is no failure:
`LaunchApp waiting for user response to ShowLaunchOption` / `ShowEula` / `ShowInterstitials` / `CreatingProcess`, then
`continues with user response …`; a start that works ends with `changed task to Completed`. `SteamConsoleLog` reads only
what Steam writes after Arcadia's first read, logs the code alone (Steam's detail may hold a path), and the tracker
ignores an abort from before the second Arcadia asked for the start.
