# #270 Remove the bundled Pan.exe that stays behind after uninstalling with the drivers

`Pan.exe --uninstall` from Arcadia's folder hands off to a verified copy in `%ProgramFiles%\Arcadia\Pan-Uninstall` and
exits, so Velopack deletes `%LocalAppData%\Arcadia` completely; the copy removes itself at the next boot. How it works:
`docs/controller-support/pan-setup.md` (Hand-off). Needed: a build of this PR installed with the Velopack installer,
Pan and the drivers installed, `pan.log` (`%ProgramData%\Arcadia\Pan`, readable by users). The check script
`Run-Check-270.ps1` (project files, `checks/270/`) records the file lists for T270-1 … T270-3 step by step.

Automated: `UninstallHandOffTests` (T270-4 … T270-6); the Windows CI runner's `ServiceTests` runs `--uninstall` from
outside Program Files, waits for the hand-off copy and checks the service is gone and the started Pan.exe still there.

- [ ] **T270-1** Uninstall Arcadia (Windows Settings → Apps), "Treiber … auch entfernen?" answered yes, UAC confirmed:
      right after the uninstaller closes, `%LocalAppData%\Arcadia` is gone or empty (A1: with the build before this PR
      exactly `current\Pan\Pan.exe` stayed); within a few minutes the drivers are gone (usbip-win2 and HidHide missing
      from Apps), `Pan-Uninstall\Pan.exe` no longer runs, `pan.log` is gone with the data folder. A2: the copy kept running
      after the uninstaller closed (it was not ended by Velopack). · E2E: no (real Velopack uninstall, UAC, real drivers);
      deferred to the acceptance review (needs an installer of the branch; owner, 2026-10-28)
- [ ] **T270-2** After T270-1 or T270-3, restart Windows: `%ProgramFiles%\Arcadia\Pan-Uninstall` and the then empty
      `%ProgramFiles%\Arcadia` are gone. · E2E: no (reboot); deferred to the acceptance review (as T270-1)
- [ ] **T270-3** Same as T270-1 with the drivers question answered no: Pan's service and `%ProgramFiles%\Arcadia\Pan` are
      gone, usbip-win2 and HidHide stay installed, nothing is left in `%LocalAppData%\Arcadia`. · E2E: no (as T270-1); deferred to the acceptance review (as T270-1)
- [ ] **T270-4** `Pan.exe --uninstall` started by hand, elevated, from Arcadia's folder (`%LocalAppData%\Arcadia\current\Pan`)
      while Arcadia stays installed: the prompt returns at once with "The uninstall goes on in …", the service goes, the
      bundled `Pan\Pan.exe` is still there; `pan.log` shows "Handing the uninstall off to …". · E2E: yes (Windows CI,
      `ServiceTests`; the decision in `UninstallHandOffTests`)
- [ ] **T270-5** The copy fails (folder not creatable, copy not startable): the uninstall runs in place and `pan.log`
      names the reason ("Uninstalling in place, …"). · E2E: no (unit tests `UninstallHandOffTests`; a failing copy can't
      be forced on a real PC without breaking Program Files)
- [ ] **T270-6** A leftover `Pan-Uninstall` (no reboot since the last uninstall) is deleted by the next `--install` or
      `--uninstall`; the copy itself doesn't try to delete its own folder. · E2E: no (unit tests `UninstallHandOffTests`)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T270-4 … T270-6 | 2026-10-28 | Claude | PR branch, unit tests on Linux (cloud container); `ServiceTests` only on the Windows CI runner | ✅ unit tests | Windows CI and owner's PC open |
