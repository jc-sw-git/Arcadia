# #402 Pan off by default; "Treiber installieren" also on a fresh install

A fresh install has none of Pan's parts, so Pan ("Controller-Wächter (Pan)") is off, shows no warnings, and offers one
button "Treiber installieren". VIIPER is no longer in the installer: that button fetches it from the Arcadia fork
(pinned, SHA-256) with usbip-win2, HidHide and Pan. Needed for the manual cases: Windows, a PC without usbip-win2,
HidHide, Pan and Arcadia (or after "Treiber entfernen"), the installer of this build.

Automated: `EmulationSetupTests` (T402-1, T402-2), `ControllerSupportTests` (the default and a saved choice).

- [ ] **T402-1** Install Arcadia freshly, start it, Einstellungen → Controller: "Controller-Wächter (Pan)" is off and shows
      the button "Treiber installieren"; no warning rows, no triangle in the header. · E2E: yes
- [ ] **T402-2** [A] on "Treiber installieren": one UAC prompt, afterwards Pan is on and runs; `viiper.exe` lies next to
      `Arcadia.exe` (the Arcadia fork's build, not the original). · E2E: no (real drivers and download)
- [ ] **T402-3** An installation that had Pan on (or off) before the update keeps that choice after the update. After the
      update `viiper.exe` is gone: with Pan on, the row "VIIPER fehlt" → "Installieren" brings it back. · E2E: no (needs
      an older installer)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T402-1, T402-2 (automated) | 2026-11-02 | Claude | PR branch, Debug, Linux cloud container, acceptance tests with faked parts; each seen red with the old default (Pan on), whole suite green (Core 2356, App 521, Pan 348) | ✅ | – |
