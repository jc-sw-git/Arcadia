# Product decisions: Windows restart (settings, category "Controller")

The category itself: [settings-controller.md](settings-controller.md). Installing the drivers:
[settings-controller-install.md](settings-controller-install.md).

## One restart row (#382, designs T382F01–F07)

- "Windows-Neustart ausstehend" (blue i, the text names no driver) stands first below the switch, also while the switch
  is off or nothing is installed, whenever a driver change waits for the Windows restart.
- A removal (HidHide or usbip-win2 uninstalled, still half there): "Starte Windows neu, danach kannst du die fehlenden
  Teile installieren." Pan pauses until the restart: nothing is emulated or hidden (what it hid is shown first), the
  switch keeps its state and reads "Pausiert bis zum Neustart von Windows" (grey lamp), no part row shows, nothing can
  be installed ("Treiber installieren" greyed out). After the restart Pan runs again without the player.
- usbip-win2 installed or updated since Windows started: "Starte Windows neu, damit der neue Treiber ganz aktiv ist."
  Pan runs, installs aren't blocked. A removal wins over it.
- The info panel: two paragraphs and the box "NACH DEM NEUSTART" (removal) or "KEINE EILE" (usbip-win2).
- The pending restart is checked fresh at every check of the parts and every 10 s (registry only), so an uninstall
  outside Arcadia pauses Pan also with the settings closed; a failed check counts as none and is logged.

## "Windows neu starten"

- [Windows neu starten] asks "Windows neu starten?" ("Windows schließt dabei alle Programme, auch Arcadia. Speichere
  vorher offene Arbeit in anderen Programmen.", [A] Neu starten, [B] Abbrechen; the footer's [A] names the focused
  button). B or "Abbrechen" closes it, the focus stays on the row.
- Greyed out with "Erst möglich, wenn „…“ beendet ist" while a game runs or starts; a game starting closes the question.
- Windows refuses (policy, another program) or doesn't answer: the toast "Windows lässt den Neustart nicht zu. Ein
  anderes Programm oder eine Einstellung von Windows verhindert ihn. …"; the row stays.
- After the restart nothing starts by itself (no question, no Windows prompt): the rows show what is missing, and one
  "Installieren" or "Treiber installieren" sets up everything missing in one run.
