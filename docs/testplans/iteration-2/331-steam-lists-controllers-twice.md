# #331 Steam lists the Steam Controller and Arcadia's Xbox pad only once

usbip-win2 attaches a device whose connection ended without a detach again by itself (~30 s later, then growing
delays), asking for the same server port and bus id. VIIPER handed the next pad or copy the lowest free bus, so the
retry attached it a second time and Steam listed both twice. Arcadia now gives every pad and copy a bus id of its own
(`ViiperBusIds`, `docs/controller-support/xbox-pad.md`). Needed: Windows, a build of this PR, Pan set up, usbip-win2,
the puck with one Steam Controller (on), Steam running.

Automated: `ViiperBusIdsTests` (T331-2). The plug itself (`ViiperXboxPads.PlugAsync`, `AddRawDeviceAsync`) has no test
seam without a VIIPER API fake; T331-3 covers it against the real VIIPER.

- [x] **T331-1** Pan on, copy running; Pan off and on again, "Ja" held, wait 90 s (`/mnt/project-files/checks/331/`,
      `Run-Check-331.ps1`). `usbip port` lists no bus id twice, and Steam's "Verbundene Controller" shows exactly one
      Steam Controller and at most one Xbox 360 Controller. · E2E: no (usbip-win2 driver, real puck, Steam)
- [x] **T331-2** Bus ids handed out to many plugs, also at the same time, are all different and never 0. · E2E: no
      (unit test)
- [x] **T331-3** Real VIIPER (fork at the pinned commit): a pad added, its bus removed, the next pad added; a usbip
      import of the removed pad's bus id (what the driver's retry sends) gets no device. · E2E: no (probe against
      the server, `checks/331/probe331-main.go`)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| Diagnosis | 2026-10-30 | Claude (owner's PC, read only) | Windows, usbip-win2 0.9.8.1, Debug build of `fix/takeover-dialog-misses-steam-controller-325`, Pan on | ❌ `usbip port`: bus ids 1-1 and 2-1 each imported twice (`localhost` and `127.0.0.1`); duplicates arrived 30–35 s after the previous generation's devices were removed | cause found |
| T331-3 | 2026-10-30 | Claude | Linux, VIIPER fork `cab6c09` built from source, auto-attach off, probe with Arcadia's call order | ❌ before: second pad got `1-1` again, stale import attached it; ✅ with own bus ids: second pad `2-1`, stale import refused | – |
| T331-2 | 2026-10-30 | Claude | Linux, `dotnet test` | ✅ | – |
| T331-1 | 2026-10-30 | owner | Windows, owner's PC, build of PR #333, usbip-win2 0.9.8.1, Pan, Steam Controller with puck, Steam | ✅ | – |
