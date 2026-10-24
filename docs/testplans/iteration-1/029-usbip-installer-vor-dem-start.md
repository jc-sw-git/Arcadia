# #29 usbip-Installer vor dem Start mit Adminrechten prüfen

- [x] **T29-1** Steam Controller Support auf einem PC ohne usbip-win2 einrichten („SISR installieren“): Der Treiber
      wird installiert, die Windows-Abfrage (UAC) zeigt „Cloudyne Systems (Scheibling Consulting AB)“ als
      Herausgeber. In `%TEMP%\Arcadia` bleibt kein Installer zurück. · E2E: nein (UAC, Kerneltreiber)
- [x] **T29-2** Den Herausgeber in `UsbipRelease.Publisher` testweise ändern (lokaler Build) und den Treiber erneut
      installieren: keine UAC-Abfrage, die Einstellungen zeigen „… nicht von … signiert. Installiere den Treiber
      bitte selbst: https://github.com/vadimgrn/usbip-win2/releases/latest“, SISR bleibt aus. · E2E: nein (UAC)
- [x] **T29-3** Ohne Internetzugang zum Sperrlisten-Server (z. B. OCSP-Hosts von GlobalSign blockiert) den Treiber
      installieren: Die Prüfung schlägt mit dem Windows-Code (`0x80092013`) fehl, es startet nichts, die Meldung
      nennt den Link zur manuellen Installation. · E2E: nein (Netzwerk, Kerneltreiber)

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T29-2 (Logik) | 2026-10-21 | Claude | Branch `td/sisr-shutdown-and-usbip-signature-100-29`, Linux, `dotnet test` (`InstallerSignatureTests`, selbst erzeugte Zertifikate, Fake-`IAuthenticode`) | ✅ nicht signiert, ungültige Signatur, falsche Organisation, Land oder Registriernummer, doppelte Organisation: kein Start; passender Herausgeber startet | `WinVerifyTrust` selbst läuft nur unter Windows: T29-1 bis T29-3 am PC des Owners |
| T29-1 bis T29-3 | 2026-10-24 | Juri | 0.1.0-rc.1, PC des Owners | ✅ | – |
