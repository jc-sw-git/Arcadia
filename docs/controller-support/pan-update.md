# Pan: the mandatory update at start (#297)

How Arcadia makes sure it runs with its own Pan ([pan.md](pan.md)); setup and uninstall: [pan-setup.md](pan-setup.md).
Owner's decisions 2026-10-28 (issue #297).

- Compatible = exactly the bundled Pan, no version range. Before the window shows,
  `ControllerSupportService.CheckPanAtStart` compares the SHA-256 of the installed and the bundled exe
  (`PanCompatibilityCheck`, the hash `--install --expect-hash` checks too). A newer Pan after an Arcadia downgrade is
  replaced as well; an unreadable exe or a service without one counts as different. Reads two small files, so it stays
  within the start budget when nothing is due.
- Not installed: no check, no page. Switched off in Arcadia (`PanEnabled`): replaced all the same, and stays off. A
  build without a bundled Pan (`NotBundled`, a Debug build that skipped it) checks nothing.
- Different: the page "Pan wird aktualisiert" (`PanUpdatePageViewModel`, `MainViewModel.PanUpdate`) covers everything
  but Pan's own dialogs; there is no question. Pan's emulation waits meanwhile (`IsPanUpdateDue`), since the update
  ends whatever the old Pan runs.
- Firmware hold first: the update waits until Pan answered its first hello (`PanClient.FirstAttempt`, at most 5 s)
  and the emulation ticked once, so a hold from before the start opens its dialog ([pan-firmware-hold.md](pan-firmware-hold.md));
  the update starts when that dialog is closed (`MainViewModel.StartPanUpdate`).
- Only Pan: `--install --expect-hash` without driver installers, so no download (works offline), no HidHide setup, no
  puck restart; Steam may run and a running Steam game doesn't block it. Missing drivers stay the "Treiber
  installieren" row. One UAC prompt, which needs mouse or keyboard until #247.
- Declined, timed out or failed (Pan's exit code; details in `pan.log`): "Pan muss aktualisiert werden" with the error,
  [A] Erneut versuchen, [X] Pan entfernen (`--uninstall` without drivers, one prompt; Arcadia then starts with Pan
  switched off; declined, the page stays), [B] Arcadia beenden. The library can't be reached before the update worked
  or Pan is removed; while a prompt is up every press is ignored.
- Pan's `--install` stages the new exe as `Pan.exe.new` and verifies it first, then stops (= full teardown) and
  replaces the old service; if the file swap fails or the new Pan can't be created, configured or started, a half-made
  service goes and the old `Pan.exe` comes back as the service. A rejected or interrupted update changes nothing, and
  the next start shows the page again; so does closing Arcadia on the page.
- `--install` accepts an older exe over a newer one: it checks only the expected hash, no version (T297-9 proves it on
  a real PC).
