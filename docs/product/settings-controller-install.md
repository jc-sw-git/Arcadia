# Product decisions: Installing the drivers (settings, category "Controller")

The category itself: [settings-controller.md](settings-controller.md). How Pan installs: `docs/controller-support/pan-setup.md`.

## Questions before the install

- "Treiber installieren" asks "Treiber installieren?" first: it names the third-party parts, the one Windows prompt
  and that a restart of the PC is recommended afterwards; below, Steam closing, Arcadia's restart after HidHide, and the
  puck's restart only while a Steam Controller or puck is plugged in.
- Every other install that runs HidHide's installer ("Installieren", "Aktualisieren", "Neu installieren") asks
  "HidHide installieren?" first, also without Steam running: Arcadia restarts once afterwards, as
  only a new Arcadia sees what Pan hides; with Steam running the same dialog says Steam closes ("Steam beenden und
  installieren"). After the install Pan hides nothing until Arcadia has restarted, which it does by itself; if that
  fails, the row "Arcadia startet nach der HidHide-Installation neu" (blue i) offers "Arcadia neu starten". No Windows
  restart is needed for this.
- Every question before a run that installs usbip-win2 carries the USB line (blue i): "Während der Installation können
  Maus, Tastatur und Controller kurz nicht reagieren. Das ist normal." A row that asks nothing first relies on the
  dialog's hint.

## The progress dialog (#413, artboard T413Dialog)

- Every driver install from the settings ("Treiber installieren", a row's "Installieren", "Aktualisieren", "Neu
  installieren") shows one modal dialog for the whole run, like "SISR wird entfernt". Not the Pan update, not
  "Controller wiederherstellen", not the uninstall.
- The Windows prompt comes first: declined, nothing else has happened (no VIIPER, Steam keeps running, nothing
  downloaded). Only a run that needs no prompt (VIIPER alone missing) starts without it, with VIIPER's one step.
- Content: the title "Treiber werden installiert", a bar, "Schritt n von m" and the percentage, then this run's steps
  in order, each ✓, running (spinner, row highlighted), pending (muted) or failed (red ✕). Steps that don't run aren't
  listed: Abfrage nach Administratorrechten bestätigen, VIIPER herunterladen und einrichten, Steam beenden, usbip-win2 herunterladen,
  HidHide herunterladen, usbip-win2 installieren ("kann einige Minuten dauern"), HidHide installieren, Pan einrichten,
  Steam starten, Arcadia neu starten. The bar is weighted per step (usbip-win2's install most), downloads fill their
  share by bytes, and it never goes back. When Pan reports no steps Arcadia can read, its steps show as the one step
  "Treiber werden installiert".
- With usbip-win2 in the run, a hint stands for the whole run: "Maus, Tastatur und Controller können kurz nicht
  reagieren" – "Das ist normal, während usbip-win2 installiert wird. Bitte schalte den PC nicht aus, Arcadia meldet
  sich, sobald es weitergeht."
- It can't be closed or cancelled while it runs: no button, no B, the page's hints hide; the footer says "Die
  Installation lässt sich nicht abbrechen." and either "Das Fenster schließt sich von selbst." or, with HidHide in the
  run, "Zum Schluss startet Arcadia einmal neu." A click beside it does nothing.
- Success: it closes by itself, the focus on the row that started it, else on the first row of "Controller"; with
  HidHide it stays at "Arcadia neu starten" until Arcadia restarts. Failure, declined prompt or timeout: the title reads
  "Installation fehlgeschlagen", the failed step is red, the steps after it stay pending (never ✓), the error stands
  below, [Schließen] (A or B) closes it and the focus goes back to the starting row, which offers "Erneut versuchen".
  When Arcadia closed Steam, "Steam starten" runs also after a failure.
- Arcadia ends during a run: Pan finishes on its own; the next start shows no dialog, the rows show what is installed.
