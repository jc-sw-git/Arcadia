# #369 Show a failed scan instead of an empty launcher tab

- [ ] **T369-1** Close Steam, lock `steamapps\libraryfolders.vdf` (e.g. PowerShell:
  `$f = [IO.File]::Open("<Steam>\steamapps\libraryfolders.vdf", 'Open', 'ReadWrite', 'None')`), start Arcadia: the Steam
  tab says "Steam konnte nicht eingelesen werden" (below the tab name "Einlesen fehlgeschlagen"), not "Keine Spiele".
  With the controller ▶ and A on "Erneut versuchen": the toast says "Steam konnte nicht eingelesen werden.". Release the
  lock (`$f.Close()`), A again: the games are back. `arcadia.log` has a line "Einlesen von Steam fehlgeschlagen" with the reason. · E2E: yes
- [ ] **T369-2** With the Steam tab showing games, lock the same file and switch away from Arcadia and back (or "Neu
  einlesen" on Steam's launcher page): the games stay, a toast says "Steam konnte nicht neu eingelesen werden; die zuletzt
  gelesenen Spiele bleiben." once, not again on the next switch. After releasing the lock nothing remains of it. · E2E: yes
- [ ] **T369-3** Make one Epic manifest (`%PROGRAMDATA%\Epic\EpicGamesLauncher\Data\Manifests\*.item`, a copy) carry
  `"InstallSize": "123"`: the Epic tab still shows all other installed games, that game without a size. · E2E: yes

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T369-1 to T369-3 (automated) | 2026-11-01 | Claude | branch build, Linux cloud container, recreated Steam and Epic files with made-up ids; the lock is .NET's `FileShare.None` | ✅ `ScanFailureTests` green, each red once with the fix taken out | Real Steam/Epic on Windows need the owner's test |
| T369-1 (looked at) | 2026-11-01 | Claude | branch build, headless Avalonia frame 1920×1080, fake Steam tab without games whose scan throws | ✅ logo with "!", title, text, "Erneut versuchen" focusable with ▶; found and fixed that an empty tab didn't switch between "Keine Spiele" and the failed state (`The_window_shows_the_failed_state_…`) | – |
| T369-2 (automated, after QE review) | 2026-11-01 | Claude | branch build, Linux cloud container, recreated Steam files | ✅ "Neu einlesen" and "Alle neu einlesen" with a locked file now say the games stay (were "Steam konnte nicht eingelesen werden." / "Alle Launcher sind neu eingelesen."); full App tests green | – |
