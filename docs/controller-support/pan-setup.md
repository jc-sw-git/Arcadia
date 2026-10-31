# Pan: setup, uninstall, restore, data folder

How Pan ([pan.md](pan.md)) gets onto the PC and off it again. The update at start: [pan-update.md](pan-update.md).

## Install: one UAC prompt for drivers and service

- "Treiber installieren" (settings, or a fix row "… fehlt"): `ControllerSupportPlatform.InstallDriversAsync` downloads
  the usbip-win2 installer only when the driver is missing or too old (`VerifiedDownload`, GitHub's SHA-256) and
  HidHide's when it is missing or older than supported (pinned in `HidHideRelease`: 1.5.230, URL, SHA-256, size), then
  runs the bundled `Pan.exe --install --expect-hash <sha256> [--usbip-installer … --usbip-sha256 …]
  [--hidhide-installer …]` elevated. VIIPER comes with Arcadia (fetched again without UAC if it went missing).
- Pan (elevated) checks each installer on a copy in its data folder (the user can't swap it after the check): the
  SHA-256 Arcadia checked at the download (a signature alone lets any older installer of the publisher through) and
  the Authenticode signature (`UsbipRelease.Publisher`; `HidHideRelease.Publisher` Nefarius Software Solutions e.U.,
  whose EV certificate expired 2025, the signature is timestamped). usbip-win2 runs with Inno Setup's `/VERYSILENT
  /SUPPRESSMSGBOXES /NORESTART /RESTARTEXITCODE=3010` (0 or 3010 = installed; up to 5 min, then left running, never
  killed); HidHide with `/exenoui /qn /norestart`, judged by exit code and the installed driver version
  (`HidHideSetup.Judge`). HidHide's setup can hang at its last step (updater registration): Pan waits 2 min, accepts a
  supported driver in place and leaves the setup running. Then a connected puck is restarted (10 s, best effort) so
  HidHide filters it without a reboot. A HidHide failure doesn't stop Pan's install (exit code 6; Arcadia names the
  log and the manual download). A supported HidHide already installed is reused.
- Pan copies itself to `%ProgramFiles%\Arcadia\Pan\Pan.exe`, compares the copy's SHA-256 with the source and the
  expected hash, and creates the service: quoted image path, auto start, description in the system language, recovery,
  90 s pre-shutdown, a DACL under which users start and query but never change. A declined prompt is a failed action
  ("… abgelehnt"); exit code 3 points to `pan.log`.
- "HidHide antwortet unerwartet" → "Neu installieren": `--install --hidhide-installer … --hidhide-repair` runs the
  pinned installer with Windows Installer's reinstall, one prompt; only over the very driver it installs and never
  while Pan hides the puck or a firmware attempt runs; with Steam closed ([steam-controller-copy-windows.md](steam-controller-copy-windows.md)).

## Data folder

`%ProgramData%` lets any user create `Arcadia\Pan` first and keep its ownership, so `--install` secures both folders
before its first log line (`ServiceSetup.SecureDataDirectory`): a reparse point is deleted, a folder not owned by
SYSTEM/Administrators or writable by anyone else is renamed to `<name>.untrusted-<time>`, the folder is created with a
protected DACL (SYSTEM and Administrators full, Users read so they reach `pan.log`, no inheritance). The service does
the same at its start, as SYSTEM, and logs what it set aside instead of refusing to run (Explorer's "Fortsetzen" once
gave a user full control). `--restore` still refuses an untrusted folder (exit 3). Commands that change the machine
stop at once without administrator rights (exit 8). `PanLog` writes its file only after the process secured or checked
the folder.

## Uninstall

- Velopack hook (`Program.OnBeforeUninstall`): one question "Treiber für die Controller-Emulation auch entfernen?"
  listing what is there (usbip-win2, HidHide, SISR and its VIIPER as in [sisr-removal.md](sisr-removal.md)), default
  no (other programs may use the drivers; `ControllerSupportRemoval.Question`). Then `Pan.exe --uninstall
  [--remove-drivers]` elevated, one UAC prompt, no question about Pan itself (it exists for Arcadia alone): stop (=
  teardown), delete the service, undo the journal, delete both folders and the empty `%ProgramData%\Arcadia` (a file in
  use goes at the next boot, `MoveFileEx`). Declined: the service stays, idles and stops itself; `Pan.exe --uninstall`
  by hand removes it.
- `--remove-drivers` only after the undo is confirmed (`UninstallOrder`: removing HidHide shows the real puck, so an
  incomplete undo or a data folder that isn't Pan's keeps the drivers, exit 5 or 7) and before the data folder goes:
  HidHide first (`msiexec /x {GUID} /qn /norestart` from its uninstall entry), then usbip-win2 (its Inno `unins*.exe`,
  `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART`), 3 min each, 0/3010 = removed; a driver that stays keeps the data
  folder with `pan.log`. Arcadia's wait per command is the sum of Pan's step budgets (`PanTimeouts`: install ~10 min,
  uninstall 9 min).
- Hand-off (#270): `--uninstall` from anywhere but `%ProgramFiles%\Arcadia` copies itself to `Pan-Uninstall\Pan.exe`
  there, checks the copy's SHA-256, starts it with the same arguments and exits, so the hook ends in seconds and
  Velopack deletes the bundled exe. The copy schedules itself, `Pan-Uninstall` and the then empty
  `%ProgramFiles%\Arcadia` for the next boot (safe there, never in the user's folder: a planted junction). Arcadia
  then sees exit 0; the copy's result stays in `pan.log` (`UninstallHandOff`).
- An uninstalled HidHide stays until Windows restarts: its service key and driver file remain, so its version still
  reads. `HidHideInstallation.IsRemovalPending` (`DeleteFlag` on the service, or the driver file in
  `PendingFileRenameOperations`) gives the row "HidHide: Neustart ausstehend" without a fix, and Pan isn't asked about
  it. The usbip restart row offers no fix either (`EmulationParts.RebootPending`).

## Restore, missing parts, exit codes

- "Controller wiederherstellen" (settings, while Pan is installed): `Pan.exe --restore` elevated, for the case the
  service itself hangs: undoes the journal without the service. The Xbox pad comes back afterwards, also after
  Arcadia gave up on VIIPER, and the pad Pan ended doesn't count as a VIIPER failure.
- Missing parts (`EmulationParts`): usbip-win2, VIIPER, Pan and HidHide each get a warning row with "Installieren" (an
  old HidHide: "Aktualisieren"; a newer major: no fix); with every part missing the setting itself offers "Treiber
  installieren" (`ControllerSupportService.CheckEmulationPartsAsync`, `CheckDrivers`). While the settings are open and
  Pan and HidHide are there, Arcadia asks Pan `CheckHiding` (25 s reply timeout) and shows its answer as a row.
  Without Pan no pad is plugged in (`GuardedXboxPads.Unavailable`) and the per-game mode is greyed out.
- Exit codes of `Pan.exe`: 0 done, 3 failed (`pan.log`), 4 data folder not securable (`--install`), 5 restore
  incomplete (`--restore`, `--uninstall`: what failed stays in the journal, `Pan.exe` stays in Program Files for the
  next `--restore` or install; Arcadia shows "Pan konnte nicht alles aufräumen …"), 6 Pan installed but HidHide not,
  7 Pan removed but a driver stayed, 8 unelevated.
