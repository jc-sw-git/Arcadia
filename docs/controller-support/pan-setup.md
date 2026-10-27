# Pan: setup, update, uninstall, restore

How Pan (#243, [pan.md](pan.md)) gets onto the PC and off it again, and how its data folder is kept its own.

- "Treiber installieren" (settings, or the fix row "Wächterdienst Pan fehlt" / "usbip-Treiber fehlt" / "HidHide fehlt"):
  `ControllerSupportPlatform.InstallDriversAsync` downloads the usbip-win2 installer only when the driver is missing or
  too old (`VerifiedDownload`, GitHub's SHA-256), and HidHide's when it is missing or older than supported (pinned in
  `HidHideRelease`: 1.5.230, URL, SHA-256, size), then runs the bundled `Pan.exe --install --expect-hash <sha256 of the
  bundled exe> [--usbip-installer <path> --usbip-sha256 <hash>] [--hidhide-installer <path>]` elevated: one UAC prompt for all. Pan (elevated) checks the installer's
  Authenticode signature against `UsbipRelease.Publisher` on a copy in Pan's data folder (the user can't swap it
  after the check) and its SHA-256 against the hash Arcadia checked at the download (a signature alone lets any older
  installer of the publisher through), runs it with Inno Setup's `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART
  /RESTARTEXITCODE=3010` (0 or 3010 = installed; up to 5 min, then left running, never killed), copies itself to
  `%ProgramFiles%\Arcadia\Pan\Pan.exe` and compares the copy's SHA-256 with the source and the expected hash, creates the service with a quoted image path, auto
  start, the description in the system language (de, en, fr, es, it; else en), recovery, 90 s pre-shutdown, and the
  DACL `D:(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;RPLCLORC;;;IU)` (users start and query,
  never change). A declined prompt is a failed action ("… abgelehnt"); Pan's exit code 3 points to `pan.log`.
- HidHide (#243 B2, `HidHideInstall`): after usbip-win2 and before Pan's own copy, the same checks on a copy (the
  pinned SHA-256, then the signature
  (`HidHideRelease.Publisher`: Nefarius Software Solutions e.U., AT; its EV certificate expired 2025, the signature is
  timestamped)), run with `/exenoui /qn /norestart`, judged by exit code and the installed driver version
  (`HidHideSetup.Judge`): 0 or 3010 with a supported version is installed. Its setup hangs at the last step (updater
  registration) on some PCs: Pan waits 2 min, accepts a supported driver in place and leaves the setup running, never
  kills it. Then a connected puck is restarted (10 s, best effort) so HidHide filters it without a reboot. A HidHide
  failure doesn't stop Pan's install: exit code 6, Arcadia names the log and the manual download. A supported HidHide
  already installed is reused; VIIPER comes from SISR's folder without UAC until #214.
- Data folder: `%ProgramData%` lets any user create `Arcadia\Pan` first and keep its ownership, so `--install`
  secures both folders before its first log line (`ServiceSetup.SecureDataDirectory`): a reparse point is deleted, a
  folder not owned by SYSTEM/Administrators is renamed to `<name>.untrusted-<time>`, the folder is created with a
  protected DACL (SYSTEM and Administrators full, no inheritance) and Administrators as owner. The service and
  `--restore` refuse to run when the folder doesn't look like that (`CheckDataDirectory`; exit 3 / service failure);
  `--uninstall` then removes only the service and Program Files. `PanLog` writes its file only after the process
  secured or checked the folder (`TrustFolder`); lines from before follow then, or reach only a console.
- Update: at start, when the bundled Pan is newer than the installed one (file versions), one dialog "Pan
  aktualisieren" (A updates with one UAC prompt, B later; asked again at the next start). Pan's `--install` stages the
  new exe as `Pan.exe.new` and verifies it first, then stops (= full teardown) and replaces the old service; if the file
  swap fails or the new Pan can't be created, configured or started, a half-made service goes and the old `Pan.exe`
  comes back as the service. A rejected update changes nothing.
- Uninstall (Velopack hook, `Program.OnBeforeUninstall`): one question "Treiber für die Controller-Emulation auch
  entfernen?" listing what is there (usbip-win2, HidHide, SISR/VIIPER until #214), default no (other programs may use
  the drivers; `ControllerSupportRemoval.Question`). Then `Pan.exe --uninstall [--remove-drivers]` elevated, one UAC
  prompt (when Pan or a driver to remove exists), no question about Pan itself (it exists for Arcadia alone): stop (= teardown), delete the service, undo the journal, delete both folders and the empty
  `%ProgramData%\Arcadia` (a file in use goes at the next boot, `MoveFileEx`). With `--remove-drivers`, only after that undo
  is confirmed (`UninstallOrder`: removing HidHide shows the real puck, so an incomplete undo or a data folder that
  isn't Pan's keeps the drivers, exit 5 or 7) and before the data folder goes (`DriverRemoval`): HidHide first (`msiexec /x {GUID} /qn /norestart` from its uninstall entry), then usbip-win2 (its
  Inno `unins*.exe` under Program Files, `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART`), 3 min each, 0/3010 = removed;
  a driver that stays keeps the data folder with `pan.log`. File logging stops before the data folder is deleted
  (`PanLog.StopFileLogging`): anyone may recreate the path then. Arcadia's wait per command is the sum of Pan's step
  budgets (`PanTimeouts`: install ~10 min, uninstall 9 min). Declined: the service stays, idles
  and stops itself; `Pan.exe --uninstall` by hand removes it. Known limits and accepted risks: [pan-risks.md](pan-risks.md).
- "Controller wiederherstellen" (settings, shown while Pan is installed): `Pan.exe --restore` elevated, for the case
  the service itself hangs: undoes the journal without the service. The Xbox pad comes back afterwards, also after
  Arcadia gave up on VIIPER, and the pad Pan ended doesn't count as a VIIPER failure.
- A Debug build on Windows publishes `Pan\Pan.exe` beside `Arcadia.dll` (target `PublishPanForDebug` in
  `Arcadia.App.csproj`: single-file, framework-dependent, not AOT; skipped when nothing of Pan changed,
  `-p:ArcadiaSkipPan=true` skips it), so "Installieren" works from the IDE; without it Arcadia says "Pan fehlt in
  Arcadias Programmordner …". The `.old` backup of an update is removed best-effort after the new service runs.
- Exit codes of `Pan.exe`: 0 done, 3 failed (`pan.log`), 4 data folder not securable (`--install`), 5 restore
  incomplete (`--restore`, `--uninstall`: what failed stays in the journal, `Pan.exe` stays in Program Files for the
  next `--restore` or install; Arcadia shows "Pan konnte nicht alles aufräumen …"), 6 Pan installed but HidHide not
  (`--install`), 7 Pan removed but a driver stayed (`--uninstall --remove-drivers`).
- Without Pan: no pad is plugged in (`GuardedXboxPads.Unavailable`), the per-game mode is greyed out.
- Missing parts (#243 B2, `EmulationParts`): usbip-win2, VIIPER, Pan and HidHide each get a warning row with
  "Installieren" (an old HidHide: "HidHide-Version wird nicht unterstützt" / "Aktualisieren"; a newer major: no fix)
  and grey out "Steam Controller Kompatibilität" ("Aus – es fehlt: …"); with every part missing the setting itself
  offers "Treiber installieren" instead (`ControllerSupportService.CheckEmulationPartsAsync`, `CheckDrivers`).
  While the settings are open and Pan and HidHide are there, Arcadia asks Pan `CheckHiding` (25 s reply timeout) and
  shows its answer as a row: HidHide not active, unexpected interface, inverse mode, hiding off, or its configuration
  client open ("Bitte schließe sie"). A missing HidHide doesn't stop the Xbox pad: only #242 hides.
- An uninstalled HidHide stays until Windows restarts (owner's B2 test, 2026-10-27): its service key and driver file
  remain, so its version still reads. `HidHideInstallation.IsRemovalPending` (`DeleteFlag` on the service, or the
  driver file deleted via `PendingFileRenameOperations`) gives the row "HidHide: Neustart ausstehend" without a fix
  (its setup would fail on the service marked for deletion), and Pan isn't asked about it. The usbip restart row no
  longer offers "SISR neu starten" once SISR is replaced (`EmulationParts.RebootPending`).
