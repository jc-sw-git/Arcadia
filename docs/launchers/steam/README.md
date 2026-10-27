# Steam — findings

Read before touching `src/Arcadia.Launchers/Steam`. Verified on the owner's PC. One file per topic; read only the one
you touch:

| Topic | File |
|---|---|
| Library, installs, updates, soundtracks, `content_log.txt` | `library-and-installs.md` |
| Online account, sign-in, owned games (#83) | `account.md` |
| Running games, play stats (#95) | `running-games.md` |

## Shortcuts (2026-10-09)

- Steam shortcut app ids in modern Steam are *not* CRC32(exe+name) — read the stored `appid`.
  `rungameid` = `(appid << 32) | 0x02000000`. Steam writes `shortcuts.vdf` only on exit;
  never write it while Steam runs. `BinaryVdf` round-trips the real file byte for byte.

## Finding Steam (2026-10-23, #138)

`SteamInstallation.Find` reads `HKCU\Software\Valve\Steam\SteamPath`, else `%ProgramFiles(x86)%\Steam`. A Steam without
registry entry (e.g. copied to another drive) is picked by hand on Steam's launcher page: the folder must hold
`steam.exe` and `steamapps`, or be `steamapps` itself (its parent is taken); it is checked when picked and only then
saved (`LauncherPreferences.Folder`). Once set, `ChosenRoot` is the only place `Find` looks; a folder that has gone
means "not found", not a fall back to the registry. Such a Steam has no logged-in account in the registry. Not yet
verified with a real Steam outside the registry.

