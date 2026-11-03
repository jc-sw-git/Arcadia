# Pan: uninstall

How Pan ([pan.md](pan.md)) leaves the PC with Arcadia; the install: [pan-setup.md](pan-setup.md).

- Velopack hook (`Program.OnBeforeUninstall`): first Arcadia's own VIIPER goes (`%APPDATA%\Arcadia\viiper`, no admin,
  whatever the answers; only `viiper.exe` started from there is ended). Then one question "Treiber für die
  Controller-Emulation auch entfernen?" listing what is there (usbip-win2, HidHide, SISR and its VIIPER as in
  [sisr-removal.md](sisr-removal.md)), default no (other programs may use the drivers; `ControllerSupportRemoval.Question`).
  Then `Pan.exe --uninstall [--remove-drivers]` elevated, one UAC prompt, no question about Pan itself (it exists for
  Arcadia alone), in `UninstallOrder`: record the pending uninstall (`uninstall-pending.json` in the data folder: drivers
  yes/no, driver attempts), stop (= teardown), undo the journal, delete stale device entries, the drivers if asked,
  only then delete the service, then both folders and the empty `%ProgramData%\Arcadia` (a file in use goes at the next
  boot, `MoveFileEx`). A data folder that isn't Pan's gets no marker and keeps the drivers (exit 7).
- `--remove-drivers` only after the undo is confirmed (removing HidHide shows the real puck): HidHide first
  (`msiexec /x {GUID} /qn /norestart` from its uninstall entry), then usbip-win2 (its Inno `unins*.exe`,
  `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART`), 3 min each, 0/3010 = removed. Arcadia's wait per command is the sum of
  Pan's step budgets (`PanTimeouts`: install ~10 min,
  uninstall ~9.5 min); the hook waits for Pan at most 40 s, the hand-off below makes that enough.
- **Pending uninstall (#422):** an incomplete undo (exit 5) or a driver that stays (exit 7) keeps the stopped service
  (auto start) and the marker. At each Windows start the service recovers the journal; once it is empty and no
  Arcadia is found, it finishes as the marker says: the drivers (each run counts, at most 4 in all: the uninstall and
  three starts; then it gives up, the drivers stay under Apps and the data folder keeps `pan.log`), its data folder,
  its own service (deleted while it runs, Windows removes it at the stop), and its running exe, which it moves into
  `Pan-Uninstall` for deletion at the next boot (scheduling `Pan\Pan.exe` itself would delete a Pan installed again
  before that boot). A crash, a power loss or an unexpected error in between: the marker stays, the next start goes on (an error
  ends the service normally, so Windows' recovery doesn't use up the attempts in one start). No notice: the
  service shows no windows, so a restart the drivers want is not announced.
- **No Arcadia left (#422):** the service also removes itself, without the drivers, when its journal is empty and no
  Arcadia is found (the UAC prompt declined, or Velopack ended the hook first). It looks at its start (`SelfRemoval`,
  `ArcadiaProbe`): Velopack's `AppData\Local\Arcadia\current\Arcadia.exe` in every profile under `ProfileList`, and
  each `Arcadia.exe` whose bundled Pan ran `--install` or that connected as a client (`arcadia-installs.txt`, up
  to 8, carried over when the data folder is set aside; a build without the installer). Twice, 10 s apart, so a
  Velopack update swapping `current` can't make Pan go; a place it can't read keeps Pan, and so does a client
  that connects meanwhile (the pipe is open while Pan looks: an Arcadia it doesn't know). It only reads attributes there, never writes or deletes in a user's folder. Any Arcadia found cancels a pending
  uninstall, and `--install` deletes the marker first thing (also while a finish runs: it stops before the drivers and
  before the service goes). `--console` never removes itself.
- Hand-off (#270): `--uninstall` from anywhere but `%ProgramFiles%\Arcadia` copies itself to `Pan-Uninstall\Pan.exe`
  there, checks the copy's SHA-256, starts it with the same arguments and exits, so the hook ends in seconds and
  Velopack deletes the bundled exe. The copy schedules itself, `Pan-Uninstall` and the then empty
  `%ProgramFiles%\Arcadia` for the next boot (safe there, never in the user's folder: a planted junction). After a
  hand-off Arcadia sees exit 0 before the copy has removed anything, so exit codes 5 and 7 never reach it; the copy's
  result stays in `pan.log` (`UninstallHandOff`). If the copy fails (not creatable, not startable, or a build that isn't
  a single file), Pan uninstalls in place and Arcadia gets its exit code only if it ends within the 40 s.
- An uninstalled HidHide stays until Windows restarts: its service key and driver file remain, so its version still
  reads. `HidHideInstallation.IsRemovalPending` (`DeleteFlag` on the service, or the driver file in
  `PendingFileRenameOperations`) gives the row "HidHide: Neustart ausstehend" without a fix, and Pan isn't asked about
  it. The usbip restart row offers no fix either (`EmulationParts.RebootPending`).

