# #340 Say on the "Pan wird beendet" page that the old Pan ends after about 30 seconds

After [B] "Ohne Pan weiter" the page "Pan wird beendet" names the usual 30 s in its text and shows "noch höchstens"
above the 45 s countdown (artboard T326F04 in the design artifact). Flow and timings are those of #326
([326-announce-the-pan-update-and-let-the-player-decline-it.md](326-announce-the-pan-update-and-let-the-player-decline-it.md)).
Needed: Windows, a build of this PR, another Pan installed (as for T326-2), the Steam Controller with its puck.

Automated: `Window/PanStoppingViewTests` (T340-1 at 100 % and 200 %, T340-2), `PanUpdateAtStartTests` (text and ring
within T326-2 and T326-6; T340-4 within T326-4, the bold style is the one of #315's note).

- [x] **T340-1** [B] on "Neue Pan-Version": "Pan wird beendet", text "Arcadia wartet, bis das alte Pan deine Controller
      freigegeben hat. Das dauert meist etwa 30 Sekunden." ("30 Sekunden" not split across lines); in the ring from
      top "noch höchstens", the number from 45, "Sekunden", all centred and clear of the ring; the number stays in
      place when it gets one digit. · E2E: yes
- [x] **T340-2** [B] pressed before Pan answered: "Pan wird beendet" with the new text, no ring and no caption.
      · E2E: yes
- [x] **T340-3** At 200 % Windows scaling the text wraps inside the dialog and nothing is clipped; with the real old
      Pan the library comes after about 30 s, as `pan.log` shows ("No client for 30 s: stopping"). · E2E: no (real
      Pan service and Windows scaling)
- [x] **T340-4** Another Pan installed, Pan off: Einstellungen → Controller, info panel of "Controller-Wächter (Pan)":
      the blue box "Schaltest du den Controller-Wächter ein, …" is in bold, as the note on Steam's page. · E2E: yes

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T340-4 | 2026-10-31 | Claude | same branch, Linux container, view model only (style not looked at) | ✅ | – |
| T340-1, T340-2 | 2026-10-31 | Claude | branch `feat/pan-stopping-page-names-the-usual-30-seconds-340`, Linux container, headless window at 100 % and 200 %, fakes; screenshots looked at | ✅ | "30 Sekunden" got a non-breaking space: the first screenshot split it across two lines |
| T340-1 to T340-4 | 2026-10-31 | owner | PR #341, Windows | ✅ | – |
