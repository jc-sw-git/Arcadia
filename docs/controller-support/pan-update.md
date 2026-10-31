# Pan: the update at start (#297, #326)

Arcadia runs only with its own Pan: compatible = exactly the bundled Pan, no version range. The update is announced
and runs only when the player confirms; declined, Pan is switched off and Arcadia leaves the old Pan alone.

- Before the window shows, `ControllerSupportService.CheckPanAtStart` compares the SHA-256 of the installed and the
  bundled exe (`PanCompatibilityCheck`; the log line "Pan geprüft in … ms" times it). A newer Pan after an Arcadia
  downgrade counts as different too; an unreadable exe or a service without one counts as different. Two small files,
  so it stays within the start budget when nothing is due.
- Not installed: no check, no page. A build without a bundled Pan (`NotBundled`, a Debug build that skipped it)
  checks nothing. Different: `IsPanUpdateDue`; Pan's emulation waits meanwhile, since the update ends whatever the old
  Pan runs.
- **Pan on:** the page "Neue Pan-Version" (`PanUpdatePageViewModel`, `MainViewModel.PanUpdate`) covers everything but
  Pan's own dialogs and says Windows asks next. [A] "Aktualisieren" starts the update, [B] "Ohne Pan weiter" declines.
  Nothing below the page takes a press; while a Windows prompt is up or the countdown runs, every press is ignored.
- **Pan off:** no page. The switch "Controller-Wächter (Pan)" reads "Neue Pan-Version verfügbar" with the blue i and
  its info panel starts with a blue box in bold; switching it on runs the update first (`PanRows`), and only a confirmed update
  switches Pan on. Declined or failed, it stays off with the toast "… bleibt aus" (a failure's error in the panel).
- Pan answers first: [A] starts the update, and [B] the switch-off, only once Pan answered its first hello (`PanClient.FirstAttempt`, at
  most 5 s; its teardown of an earlier Arcadia may still run) and the emulation ticked once; an open firmware notice
  is closed first, one dialog at a time (`MainViewModel.PanAnsweredAtStart`, `StartPanUpdate`). [A] pressed earlier shows "Pan wird aktualisiert" meanwhile, [B] "Pan wird beendet" without a countdown.
- While the update runs, Arcadia doesn't talk to Pan (`ControllerSupportService.IsPanUpdating`, `PanClient.WhyLeftAlone`):
  `--install` stops the old service and waits up to 90 s for it to end, and a reconnecting client would start it again
  (#354). This holds until the update succeeded or, declined or failed, until `SwitchPanOffForUpdate` switched Pan off.
- Only Pan: `--install --expect-hash` without driver installers, so no download (works offline), no HidHide setup, no
  puck restart; Steam may run and a running Steam game doesn't block it. One UAC prompt, which needs mouse or keyboard
  until #247. Confirmed: library, toast "Pan: aktualisiert".
- **Declined or failed** ([B], the prompt declined, Pan's exit code; details in `pan.log`):
  `SwitchPanOffForUpdate` sets `PanEnabled = false` (and the error for the switch's panel), so `MayTalkToPan` is false
  and `PanClient` (`Wanted`) drops its connection and doesn't reconnect while the update is due and Pan off. Left
  without a client, the old Pan undoes what it holds with its own code, whatever protocol it speaks, and ends itself
  after 30 s ([pan.md](pan.md)). The page shows "Pan wird beendet" with a 45 s countdown (the text names the usual
  30 s, the ring "noch höchstens" as the upper bound) and opens the library as soon as the service has stopped
  (`IsPanRunning`), with the toast naming why Pan is off.
- **Old Pan still running after 45 s** (its undo failed and it keeps retrying): stage 1 "Controller freigeben",
  [A] "Controller wiederherstellen" runs the bundled Pan's `--restore` (one prompt; it reads an older Pan's journal,
  `JournalTests`), then the library; [B] ends Arcadia. Declined or failed: stage 2 "Controller von Hand freigeben"
  with the steps in HidHide's Configuration Client ([pan-hiding.md](pan-hiding.md), last resort), [A] "Erneut prüfen"
  runs the countdown again, [B] ends Arcadia. "Checked" means the service ended; Arcadia doesn't ask HidHide itself.
- Pan's `--install` stages the new exe as `Pan.exe.new` and verifies it first, then stops (= full teardown) and
  replaces the old service; if the file swap fails or the new Pan can't be created, configured or started, a half-made
  service goes and the old `Pan.exe` comes back as the service. A rejected or interrupted update changes nothing, and
  the next start with Pan on shows the page again. `--install` accepts an older exe over a newer one: it checks only
  the expected hash, no version. The `.old` backup is removed best-effort after the new service runs.
