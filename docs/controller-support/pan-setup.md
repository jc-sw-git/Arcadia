# Pan: setup, update, uninstall, restore

How Pan (#243, [pan.md](pan.md)) gets onto the PC and off it again, and how its data folder is kept its own.

- "Treiber installieren" (settings, or the fix row "Wächterdienst Pan fehlt" / "usbip-Treiber fehlt"):
  `ControllerSupportPlatform.InstallDriversAsync` downloads the usbip-win2 installer only when the driver is missing or
  too old (`VerifiedDownload`, GitHub's SHA-256), then runs the bundled `Pan.exe --install --expect-hash <sha256 of the
  bundled exe> [--usbip-installer <path>]` elevated: one UAC prompt for both. Pan (elevated) checks the installer's
  Authenticode signature against `UsbipRelease.Publisher` on a copy in Pan's data folder (the user can't swap it
  after the check), runs that `/S` (5 min), copies itself to
  `%ProgramFiles%\Arcadia\Pan\Pan.exe` and compares the copy's SHA-256 with the source and the expected hash, creates the service with a quoted image path, auto
  start, the description in the system language (de, en, fr, es, it; else en), recovery, 40 s pre-shutdown, and the
  DACL `D:(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;RPLCLORC;;;IU)` (users start and query,
  never change). A declined prompt is a failed action ("… abgelehnt"); Pan's exit code 3 points to `pan.log`.
- Data folder: `%ProgramData%` lets any user create `Arcadia\Pan` first and keep its ownership, so `--install`
  secures both folders before its first log line (`ServiceSetup.SecureDataDirectory`): a reparse point is deleted, a
  folder not owned by SYSTEM/Administrators is renamed to `<name>.untrusted-<time>`, the folder is created with a
  protected DACL (SYSTEM and Administrators full, no inheritance) and Administrators as owner. The service and
  `--restore` refuse to run when the folder doesn't look like that (`CheckDataDirectory`; exit 3 / service failure);
  `--uninstall` then removes only the service and Program Files. `PanLog` writes its file only after the process
  secured or checked the folder (`TrustFolder`); lines from before follow then, or reach only a console.
- Update: at start, when the bundled Pan is newer than the installed one (file versions), one dialog "Pan
  aktualisieren" (A updates with one UAC prompt, B later; asked again at the next start). Pan's `--install` stages the
  new exe as `Pan.exe.new` and verifies it first, then stops (= full teardown) and replaces the old service; if the new
  Pan can't be created or started, the old `Pan.exe` comes back as the service. A rejected update changes nothing.
- Uninstall (Velopack hook, `Program.OnBeforeUninstall`): after the SISR and data questions, `Pan.exe --uninstall`
  elevated, one UAC prompt (only when the service or its Program Files folder exists), no question (Pan exists for
  Arcadia alone): stop (= teardown), delete the service, undo the journal, delete both folders and the empty
  `%ProgramData%\Arcadia` (a file in use goes at the next boot, `MoveFileEx`). Declined: the service stays, idles
  and stops itself; `Pan.exe --uninstall` by hand removes it. Known limits and accepted risks: [pan-risks.md](pan-risks.md).
- "Controller wiederherstellen" (settings, shown while Pan is installed): `Pan.exe --restore` elevated, for the case
  the service itself hangs: undoes the journal without the service.
- Exit codes of `Pan.exe`: 0 done, 3 failed (`pan.log`), 4 data folder not securable (`--install`), 5 restore
  incomplete (`--restore`, `--uninstall`: what failed stays in the journal, `Pan.exe` stays in Program Files for the
  next `--restore` or install; Arcadia shows "Pan konnte nicht alles aufräumen …").
- Without Pan: no pad is plugged in (`GuardedXboxPads.Unavailable`), the per-game mode is greyed out, the Controller
  category shows the warning "Wächterdienst Pan fehlt" also while "Steam Controller Kompatibilität" is off
  (`ControllerSupportService` → `IControllerSupportPlatform.CheckDrivers`, only while the settings watch).
