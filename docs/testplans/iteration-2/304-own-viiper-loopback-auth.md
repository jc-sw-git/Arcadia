# #304 Run only Arcadia's own VIIPER, on loopback, with Windows-assigned ports and API auth

Arcadia starts its pinned `viiper.exe` on `127.0.0.1:0` for both usbip and API, with a key of its own that VIIPER
requires, checks all of it before any device goes on it, and never uses another program's VIIPER. Pan takes an
emulation only with the usbip port of a held `viiper.exe` and its teardown detaches only that port's devices. How it
works: `docs/controller-support/xbox-pad.md`, `pan.md`, `pan-risks.md`. Needed for the manual cases: Windows, a build
of this PR, usbip-win2 and Pan installed, Steam Controller with puck; T304-1, 3, 9, 11 run with
`/mnt/project-files/checks/304/check-304.zip` (`Run-Check-304.ps1`, no controller needed, not as administrator).

Automated: unit and integration tests `ViiperXboxPadsTests` (start and its checks through a faked host),
`ViiperConnectionTests` (handshake and frames against `Viiper.Client`'s own code), `ViiperListenersTests`,
`UsbipServerTests`, `TeardownTests`, `JournalTests`, `UsbipPortsTests` (Pan side) and `PanEmulationTests` (VIIPER lost).
No case has a UI flow of its own (the visible part is #242's copy and toasts), so none is an `ArcadiaDriver` test.

- [ ] **T304-1** Arcadia's `viiper.exe` listens on exactly two ports, both on `127.0.0.1`, numbers from Windows (not
      3241/3242), nobody else on either number in IPv4 or IPv6; `arcadia.log` says "VIIPER: bereit … usbip
      127.0.0.1:x, API 127.0.0.1:y, Schlüssel geprüft". · E2E: no (unit tests with faked listeners; real TCP table in
      check step 1)
- [ ] **T304-2** A build with an empty pin has no VIIPER at all: no Xbox pad, no copy, no other program's `viiper.exe`
      used. · E2E: no (unit test `NoVirtualXboxPads`)
- [ ] **T304-3** Another program's VIIPER on 3241/3242 beside Arcadia's: the pad comes from Arcadia's port, the other
      one is neither used nor ended, and Pan's teardown leaves its devices alone. · E2E: no (unit tests; real run in
      check step 2)
- [ ] **T304-4** The API needs Arcadia's key: with it VIIPER answers, without it or with a wrong one it refuses; the
      raw stream (the copy) runs encrypted; the key file is gone after the start, the key is in neither `arcadia.log`
      nor `viiper.log`, `%APPDATA%\VIIPER` is untouched. · E2E: no (unit tests against VIIPER's client code; real
      server in check step 1)
- [ ] **T304-5** Fail closed: VIIPER ending at once, not in the job object, a key file that can't be written, a third
      port, another address, another process on a port number, no or two answering ports, a ping without key that
      is answered, a timeout: `viiper.exe` ended, no key file, the start fails with a log line, nothing plugged.
      Another process on a port of the running VIIPER (either family): no new pad or copy, and the next emulation
      starts a new VIIPER. · E2E: no (unit tests, one per failure)
- [ ] **T304-6** Pan journals the usbip port of the held `viiper.exe` per generation; a start without a `viiper.exe` of
      Arcadia's, with a port it doesn't alone serve, or that it serves beyond 127.0.0.1 is refused; a teardown detaches
      only that port's devices and keeps the entry until all detached; a planned stop drops only its own generation's.
      · E2E: no (Pan unit tests)
- [ ] **T304-7** A Pan journal from before #304 with an open emulation detaches what port 3241 served, as that Pan
      did; an idle one gets no port. A journal written now is still read by the Pan from before #304 (same version,
      generation and pending work kept), as the update at start may install it (T297-9). · E2E: no (Pan unit tests)
- [ ] **T304-8** VIIPER lost during the copy (killed in Task Manager): the emulation ends and Pan shows the puck; the next
      start names VIIPER's new port to Pan before any device goes on it, and the copy runs again. · E2E: no (integration
      test with fakes; real kill on the owner's PC)
- [ ] **T304-9** The encrypted raw stream stays within T242-18: p99 of a write under 1 ms. · E2E: no (check step 3)
- [ ] **T304-10** A `viiper.exe` left from Arcadia's own path (e.g. after a hard kill) is ended before the start; a
      `viiper.exe` from another path is not. · E2E: no (unit test)
- [ ] **T304-11** No Windows Firewall prompt at the first start of Arcadia's VIIPER. · E2E: no (check step 1)
- [ ] **T304-12** Regression of #242: with Pan and the Steam Controller, a game sees the Xbox pad, Steam the copy;
      Arcadia ended in Task Manager mid-copy, Pan's teardown shows the puck again and `pan.log` names the detached
      port. · E2E: no (real puck, Steam, Pan)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T304-1 … T304-8, T304-10 (automated) | 2026-10-29 | Claude | PR branch, Debug, Linux cloud container, `dotnet test Arcadia.slnx` (2,775 tests, after merging the base) | ✅ | Windows CI open |
| Red once (mutations) | 2026-10-29 | Claude | same; each seen red: auth flag dropped, key-refusal check skipped, usbip on 0.0.0.0, receive counter unchecked, VIIPER-lost check off, Pan without `ServerOf`, teardown of every local port, server dropped after a failed detach | ✅ | – |
| QE review round (PR #318) | 2026-10-29 | Claude | same; new tests each seen red: no listener re-check before a pad or copy, first of two answering ports taken, running VIIPER reused without its listeners checked, planned stop dropping every server, Pan taking a listener beyond 127.0.0.1, old journal without 3241, journal version raised | ✅ | – |
| T304-4 (real server) | 2026-10-29 | Claude | Linux build of the fork at the pinned commit, `--api.require-local-host-auth`: key answers, no key and wrong key refused, encrypted ping round trip | ✅ | Windows run in check step 1 |
| T304-1, 3, 4, 9, 11 (check-304) | 2026-10-30 | owner | Windows 10.0.26200, owner's PC, build of this PR's pinned VIIPER, usbip-win2 | ✅ only `127.0.0.1`, ports from Windows (never 3241/3242), key answered, no key and wrong key refused, key file gone, key in no log, `%APPDATA%\VIIPER` unchanged, no firewall prompt; pad attached from `127.0.0.1` beside another VIIPER on 3241 and seen in `joy.cpl`; raw stream p99 0.232 ms (p50 0.074, max 0.419) | The script's two NOT OKs were its own: its log grep for "password" matched VIIPER's "invalid password" from the wrong-key test, and an aborted first run left its foreign VIIPER on 3241 so the second could not bind (in the first run it stayed up beside Arcadia's). T304-10's own-path case is covered by its unit test only; probe tool removed |
| T304-12 | 2026-10-30 | owner | Windows, owner's PC, build of this PR, Steam Controller with puck, Pan | ✅ | – |
| T304-8 | 2026-10-30 | owner | same | VIIPER killed: puck and controller shown again, Arcadia reports the emulation ended and starts a new one, asking "… übernehmen" again (a gap) | copy after "Ja" to be confirmed |
