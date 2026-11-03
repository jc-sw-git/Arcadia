# Pan: setup, restore, data folder

How Pan ([pan.md](pan.md)) gets onto the PC. The update at start: [pan-update.md](pan-update.md); the uninstall: [pan-uninstall.md](pan-uninstall.md).

## Install: one UAC prompt for drivers and service

- A driver install from the settings (`DriverInstallation`, progress in `DriverInstallProgress`, #413) starts with the
  Windows prompt: `PanSetup.StartInstallAsync` runs the bundled `Pan.exe --install --expect-hash <sha256> --run <id>
  --client <Arcadia's pid> --installers <%TEMP%\Arcadia\<id>> [--usbip] [--hidhide [--hidhide-repair]]` elevated
  (`InstallRun`: id 128 random bits; a half run on the command line is refused before anything is touched). Declined:
  nothing else happens. Then Arcadia installs VIIPER (no UAC; pinned in `ViiperRelease`, SHA-256, into
  `%APPDATA%\Arcadia\viiper\`), checks again for a Steam game or a firmware update, closes Steam if HidHide is
  installed, downloads the installers into the run's folder (usbip-win2 only when missing or too old, `VerifiedDownload`,
  GitHub's SHA-256; HidHide when missing or older than supported, pinned in `HidHideRelease`: 1.5.230, URL, SHA-256,
  size), each step with its own timeout, and hands them over.
- Hand-over (`InstallHandOver`, `InstallHandOverPipe`): elevated Pan secures its data folder, then waits for one message
  on the pipe `Arcadia.Pan.Install.<id>` (first instance, DACL: Arcadia's user only, read/write). Arcadia checks that
  the pipe's server is the Pan it started; Pan hears only `<pid>`, and refuses a `<pid>` that started after itself.
  `go` (UTF-8, ≤ 4 KB, one `key=value` per line: `usbip=`, `usbip-sha256=`, `hidhide=`) must name exactly the installers
  the command line announced, each directly in `--installers`, no reparse point (exit 3 otherwise); HidHide's hash is
  always the pinned one. `abort`, Arcadia's exit, a broken pipe, no `go` within `PanTimeouts.HandOver` (30 min, more
  than Arcadia's steps up to the hand-over) or a pipe name already taken end Pan with exit 9 and one `pan.log` line,
  nothing changed beyond securing the data folder. A failure between prompt and hand-over sends `abort`, then starts
  Steam again. Exit 9 that Arcadia didn't cause has its own message (`PanExitCodes`).
- Step report: Pan writes `install-<id>.report` into its data folder (users read only; earlier reports deleted): `run
  <id>`, then `usbip|hidhide|pan running|done|failed` per line. Display only: the exit code decides how the run ends; a
  report of another run, a garbled one or none makes the dialog show Pan's steps as one.
- Pan (elevated) checks each installer on a copy in its data folder (the user can't swap it after the check): the
  SHA-256 Arcadia names (the one it checked at the download; it ties the run to that file, but it comes from the
  unelevated client, so it is no version floor: [pan-risks.md](pan-risks.md)) and
  the Authenticode signature (`UsbipRelease.Publisher`; `HidHideRelease.Publisher` Nefarius Software Solutions e.U.,
  whose EV certificate expired 2025, the signature is timestamped). usbip-win2 runs with Inno Setup's `/VERYSILENT
  /SUPPRESSMSGBOXES /NORESTART /RESTARTEXITCODE=3010` (0 or 3010 = installed; up to 5 min, then left running, never
  killed); HidHide with `/exenoui /qn /norestart`, judged by exit code and the installed driver version
  (`HidHideSetup.Judge`). HidHide's setup can hang at its last step (updater registration): Pan waits 2 min, accepts a
  supported driver in place and leaves the setup running. Then a connected puck is restarted (10 s, best effort) so
  HidHide filters it without a reboot. A HidHide failure doesn't stop Pan's install (exit code 6; Arcadia names the
  log and the manual download); a usbip-win2 failure does: it runs first, and neither HidHide nor Pan follows (exit 3). A supported HidHide already installed is reused.
- Pan copies itself to `%ProgramFiles%\Arcadia\Pan\Pan.exe`, compares the copy's SHA-256 with the source and the
  expected hash, and creates the service: quoted image path, auto start, an English description (Pan runs with `InvariantGlobalization`), recovery,
  90 s pre-shutdown, a DACL under which users start and query but never change. A declined prompt is a failed action
  ("… abgelehnt"); exit code 3 points to `pan.log`.
- "HidHide antwortet unerwartet" → "Neu installieren": a run with `--hidhide --hidhide-repair` runs the
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

How Pan leaves the PC, also when something gets in the way: [pan-uninstall.md](pan-uninstall.md).

## Restore, missing parts, exit codes

- "Controller wiederherstellen" (settings, while Pan is installed): `Pan.exe --restore` elevated, for the case the
  service itself hangs or a model stays hidden: undoes the journal in a second process, beside a running service.
  Before it Arcadia lets go of Pan as for an update (`WhyPanIsLeftAlone`, `PanClient.LetGoAsync`: a hide under way
  gets its answer first), so the service journals no new hide the restore could overwrite; the service's teardown
  only shows and removes, as the restore does. The Xbox pad comes back afterwards, also after Arcadia gave up on
  VIIPER, and the pad Pan ended doesn't count as a VIIPER failure.
- Missing parts (`EmulationParts`): usbip-win2, VIIPER, Pan and HidHide each get a warning row with "Installieren" (an
  old HidHide: "Aktualisieren"; a newer major: no fix); with every part missing the setting itself offers "Treiber
  installieren" (`ControllerSupportService.CheckEmulationPartsAsync`, `CheckDrivers`). While the settings are open and
  Pan and HidHide are there, Arcadia asks Pan `CheckHiding` (25 s reply timeout) and shows its answer as a row.
  Without Pan no pad is plugged in (`GuardedXboxPads.Unavailable`) and the per-game mode is greyed out.
- Exit codes of `Pan.exe`: 0 done, 3 failed (`pan.log`), 4 data folder not securable (`--install`), 5 restore
  incomplete (`--restore`, `--uninstall`: what failed stays in the journal; after `--uninstall` the service stays and
  retries at every start; Arcadia shows "Pan konnte nicht alles aufräumen …"), 6 Pan installed but HidHide not,
  7 a driver stayed (Pan retries at the next start, or gave up), 8 unelevated, 9 the install ended before the hand-over.
