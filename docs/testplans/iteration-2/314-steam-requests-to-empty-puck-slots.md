# #314 Steam's refused requests to the puck no longer flood the log

With the copy running, Steam asks the puck's empty wireless slots every few seconds (`GET_STRING_ATTRIBUTE` and the
GET_REPORT for its answer), and the real puck refuses them, as it would without the copy (spike #239, see
`docs/controller-support/steam-controller-copy-windows.md`). `arcadia.log` now gets one line per kind of refusal
(call, interface, report id, message, error) instead of one per refusal, and the counts when the copy ends. Needed:
Windows, a build of this PR, Pan set up, the puck with one Steam Controller (on, connected), Steam installed.

Automated: `SteamControllerCopyBridgeTests` (T314-1: once per kind; another message, error, slot or report id gets its
own line and count; a failed GET_REPORT names only a message that reached the puck; no summary without refusals).

- [ ] **T314-1** Pan on, Steam running, wait one minute, then close Arcadia. `arcadia.log` has at most a few lines
      "Steam-Controller-Kopie: … an den Puck fehlgeschlagen", each naming interface, report and message, not a pair
      every few seconds, and at the end one line "vom Puck abgelehnt: …" with the counts. The interfaces named are
      slots without a controller (not the one the controller is on). · E2E: no (needs the real puck; the logic is unit tested)
- [ ] **T314-2** In the same run Steam works with the controller as usual (Steam button, controller list, battery). · E2E: no

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T314-1 | 2026-10-29 | Claude | PR branch, Debug, Linux cloud container, unit tests with a fake puck. Each test red once: "once per kind" by logging every refusal; "another message or error" by keying without error or message (QE); "another slot or report" by keying without interface or report id; "names only a message that reached the puck" by remembering dropped messages too; "nothing refused" by logging an empty summary (QE) | ✅ | owner's PC open |
