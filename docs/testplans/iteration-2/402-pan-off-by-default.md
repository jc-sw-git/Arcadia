# #402 Pan off by default; "Treiber installieren" also on a fresh install

A fresh install has none of Pan's parts, so Pan ("Controller-Wächter (Pan)") is off, shows no warnings, and offers one
button "Treiber installieren". VIIPER is no longer in the installer: that button fetches it from the Arcadia fork
(pinned, SHA-256) with usbip-win2, HidHide and Pan. Needed for the manual cases: Windows, a PC without usbip-win2,
HidHide, Pan and VIIPER (or after "Treiber entfernen"), the installer of this build.

Automated: `EmulationSetupTests` (the fresh install, Pan off, "Treiber installieren" switches Pan on, the update from a version that shipped VIIPER), `ControllerSupportTests` (the default and a saved choice), `ViiperInstallationTests` (the data folder).

- [ ] **T402-1** Install Arcadia freshly, start it, Einstellungen → Controller: "Controller-Wächter (Pan)" is off and shows
      the button "Treiber installieren"; no warning rows, no triangle in the header. · E2E: yes
- [ ] **T402-2** [A] on "Treiber installieren": one UAC prompt, afterwards Pan is on and runs; `viiper.exe` lies in
      `%APPDATA%\Arcadia\viiper\` (the Arcadia fork's build, not the original). · E2E: no (real drivers and download)
- [ ] **T402-3** Update from `0.2.0-rc.1` (it shipped `viiper.exe` in the program folder) to this build, Pan on: after
      the update the row "VIIPER fehlt" → "Installieren" fetches it without a UAC prompt; Pan off: nothing warns, the
      switch stays off. The saved choice of Pan stays as it was. · E2E: no (needs the older installer)
- [ ] **T402-4** Update from a build with this change to a later one, after "Treiber installieren" ran:
      `%APPDATA%\Arcadia\viiper\viiper.exe` is still there and Pan runs without a new install (the update replaces
      only the program folder). · E2E: no (needs a later installer)
- [ ] **T402-5** Uninstall Arcadia, answer "Treiber für die Controller-Emulation auch entfernen?" with Ja: the list
      names VIIPER (about 13 MB), `%APPDATA%\Arcadia\viiper` is gone afterwards. · E2E: no (real uninstall)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T402-1 to T402-3 (automated) | 2026-11-02 | Claude | PR branch, Debug, Linux cloud container, acceptance tests with faked parts; each seen red with the old default (Pan on), whole suite green (Core 2356, App 521, Pan 348) | ✅ | – |
| T402-2, T402-3 (QE acceptance tests) | 2026-11-02 | Claude (QE) | PR branch at 78f26a0, Debug, Linux cloud container, faked parts; each seen red once | ❌ T402-2: Pan stays off after "Treiber installieren"; T402-3 ✅ | Fixed: the install switches Pan on |
