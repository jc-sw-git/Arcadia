# Steam — running games (#95, 2026-10-21)

Index: `README.md`. Verified on the owner's PC.

`SteamRunningGames` reads `HKCU\Software\Valve\Steam\Apps\<appid>\Running` (1 while the game runs) and, like SISR
already does, `RunningAppID` (the game Steam started last, while it runs; it counts even without the flag). There
are thousands of `Apps` keys (every app Steam ever touched), so only a poll after a pause of more than 10 s (Arcadia
just started, or the PC slept) reads them all; the polls in between read `RunningAppID` and the keys
of the games already running. A game that starts meanwhile becomes `RunningAppID`. "Läuft seit" is the last write
time of the app's key, read once per run (`RegQueryInfoKey`). Checked on the owner's PC (T95-12,
2026-10-24): Steam sets `Running` to 1 while the game runs and back to 0 when it ends, also when the game is killed in
Task Manager. **But the flag can stick:** Marvel's Guardians of the Galaxy (1088850), not installed, still had
`Running` = 1, its key last written on 2026-10-20 although it was last played in 2025; cause unknown. Not the cause:
`steam://rungameid` on an uninstalled game (opens the install dialog, leaves `Running` at 0; checked 2026-10-24). A
stale flag would show such a game as running for good once it is installed again (#182).
Play stats: Steam writes `LastPlayed` into the manifest at the start and the account's `localconfig.vdf` at some point after the end; the scanner takes the later of both,
and Arcadia rescans right after a game ended and 30 s later.
