# #347 Remove the stale device entries Arcadia's emulated devices leave in Windows

Pan removes the non-present device entries below usbip-win2's host once nothing emulates: after an emulation ended,
once after Pan starts, and at Pan's uninstall. Findings: [pan-stale-devices.md](../../controller-support/pan-stale-devices.md).
Counting the entries by hand: `Run-Check-347.ps1` (survey only: answer its first step, then close it) or Device Manager,
View → "Show hidden devices", the greyed entries below "USB Host Controller - usbip-win2" … `ROOT\USB\0000`.

- [x] **T347-1** usbip-win2's host is recognised by its exact hardware id: `Run-Check-347-hostid.ps1` says "OK", the
      host `ROOT\USB\0000` lists `ROOT\USBIP_WIN2\UDE`. Real Valve entries (puck, controller by cable, bootloader) and
      entries below other hosts stay. · E2E: no (real device tree; Pan `StaleDevicesTests` cover the classification)
- [x] **T347-2** Install this build's Pan (the owner's PC has the leftovers of earlier sessions). Within a few seconds
      after Pan starts, `pan.log` says "Stale device entries (Pan started): n found, n removed, 0 failed, 0 kept"; the
      survey then counts 0 non-present entries below usbip-win2, the same number of real Valve entries as before, and
      Arcadia connected to Pan without "Pan antwortet nicht". · E2E: yes (Pan `GuardianStaleDevicesTests`)
- [x] **T347-3** Three sessions in a row (Arcadia with Steam Controller on, Steam started, a game on "Arcadia" with
      the Xbox pad, Arcadia closed): after each, `pan.log` has "Stale device entries (emulation stopped …)" or
      "(teardown, …)", and the survey never counts more than one session's entries (≤ 22); after the next Pan start 0.
      The copy and the pad work in every session, Steam keeps the controller's settings. · E2E: yes (Pan
      `GuardianStaleDevicesTests`)
- [x] **T347-4** End Arcadia hard (Task Manager) during an emulation: the teardown runs, then the cleanup; `pan.log`
      names no instance id, a failure only as "Stale entries not removed: n × 28DE:1304: CONFIGRET 0x…". · E2E: yes (Pan
      `GuardianStaleDevicesTests`, `StaleDevicesTests`)
- [x] **T347-5** Start Arcadia right after closing it (within 2 s, while Pan cleans up): the pad comes after a short
      "Pan räumt noch auf" without "Emulation angehalten"; `arcadia.log` counts no pad loss. · E2E: yes (Pan
      `GuardianStaleDevicesTests`; the client's wait without a loss is `PanClient.BeingShownWait`, covered by Core
      `PanClientTests`)
- [x] **T347-6** Pan's uninstall (settings → Pan entfernen, with drivers): `pan.log`/the uninstall log has "Stale
      device entries (uninstall)" before the drivers go; afterwards Device Manager shows no greyed copy entries.
      · E2E: no (real uninstall; Pan `UninstallOrderTests` cover the order and that `RestoreIncomplete` skips it)
- [x] **T347-7** No cleanup while a Valve bootloader is connected (a firmware update in Steam), while a real controller
      is still hidden, or on Pan's stop (switch Pan off during an emulation): `pan.log` then has no "Stale device
      entries" line, or "not removed (…): a Valve bootloader is connected". · E2E: yes (Pan `GuardianStaleDevicesTests`)
- Not automated on purpose (decision 11): a device another program plugs in through usbip-win2 during the ~2 s of a
  cleanup may lose a HID collection until it is replugged.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| `Run-Check-347.ps1` (A1–A4) | 2026-10-31 | owner | spike script, owner's PC | ✅ A1, A3, A4; ❌ A2 (`CM_Uninstall_DevNode` removes present devnodes) | Decision 11: only while nothing emulates, presence re-checked |
| T347-1 to T347-7 (Pan `StaleDevicesTests`, `GuardianStaleDevicesTests`, `UninstallOrderTests`), all App, Core and Pan tests | 2026-10-31 | Claude | PR branch, Linux cloud container | ✅ (each new test ❌ once with its part taken out: no wait for the cleanup, no gate on emulation/journal/stop, no child check, no presence re-check, host not required present, "contains USBIP", uninstall step skipped) | – |
| T347-1 (`Run-Check-347-hostid.ps1`, reads only) | 2026-10-31 | owner, read by Claude | check script, owner's PC | ✅ `ROOT\USB\0000` has exactly `ROOT\USBIP_WIN2\UDE`; exact id and "contains USBIP" find the same 48 stale entries | – |
| T347-2 to T347-4: several sessions with play, one hard end (Task Manager), `pan.log` read by Claude, then `Run-Check-347-hostid.ps1` | 2026-10-31 | owner | PR #349 at 6c13702, owner's PC | ✅ Pan start: 52 removed; after each planned end 26, after the hard end 3; none after a teardown left incomplete (detach failed), caught up at the next; check afterwards: 0 left. ❌ minor: the first hard end's cleanup ran 0.1 s after the detach and found nothing yet | Cleanup after an emulation waits 2 s (`StaleCleanupSettle`) |
| Rework: QE bug (stop waits unbounded), settle, grouped failure log; new Pan tests `An_emulation_started_while_the_cleanup_waits_to_settle…`, `A_stop_waits_only_so_long…`, `A_stop_ends_a_cleanup_that_waits_to_settle…`; all App, Core and Pan tests | 2026-10-31 | Claude | PR branch merged with `feat/replace-sisr` 72d449e, Linux cloud container | ✅ (each new test ❌ once: settling cleanup blocks the start; stop wait unbounded; settle not cancelled) | – |
| T347-5 to T347-7 | 2026-10-31 | owner | PR #349 at df0ce79, owner's PC | ✅ accepted by the owner | – |
