# Starting and stopping SISR

Verified on the owner's PC. Overview and the other files: [README](README.md).

- SISR may only run while Steam is ready: `steam` and `steamwebhelper` run, a user is logged in
  (`ActiveProcess\ActiveUser`) and the client finished that login: the latest `Starting login` in
  `logs/steamui_login.txt` is followed by `SetLoginState: Success` (`SteamLoginLog`). `ActiveUser` is set as
  soon as the server accepts the login; on 2026-10-16 Steam then hung at "loading user data" for many
  starts in a row and SISR started anyway. In those starts `controller.txt` stops after
  `wireless_unknown because Data is flowing` (no `reserving XInput slot`); the controller came back only
  after a Windows restart, cause unknown.
  Untested: whether offline mode writes `Success`. Started earlier, SISR shows its own full-screen "Steam is not running"
  window, or reads no marker (it reads the logged-in user's `shortcuts.vdf` once at start) and asks to add
  another one. So `StartSisr` skips without a ready Steam or without the marker.
- Two Steam signals: `IsSteamReady` (above) gates starting SISR; `IsSteamUp` (processes + `ActiveUser`, no
  login check) decides whether SISR is held, quit gracefully or killed, so a login that hangs or repeats
  doesn't stop a running SISR. If Steam stays up without finishing its login for 30 s
  (`SteamLoginGrace`), the settings warn "Steam meldet sich noch an" with "Steam neu starten"; Arcadia
  restarts Steam itself (`RestartSteamAsync`: stop SISR, close Steam, start it, wait 60 s, start SISR),
  also for the CEF fix, because SISR's own restart left SISR on a Steam that was going away.
- The marker adds an entry to the account's Steam library, so with several accounts (userdata folders) the
  user chooses (owner, 2026-10-16): "SISR installieren" (and "Anlegen") first asks "Steam-Eintrag für SISR
  anlegen?" with every account in `config/loginusers.vdf` (userdata folders only without that file: removed
  accounts keep theirs), the last logged-in one first and ticked; cancelling ends the install. With
  one account it is written without asking. Without a choice it goes to the logged-in account, else the only
  one, else the most recent one in `config/loginusers.vdf` (`MostRecent`, then `Timestamp`). Without any
  account "SISR installieren" asks to log in to Steam once, before it closes Steam or installs anything. An account without the marker shows "Steam-Eintrag
  für SISR fehlt" once it is logged in.
- SISR starts the `viiper.exe` next to it and kills it on quit; killing SISR's process tree takes it along.
  VIIPER attaches its virtual controllers through the usbip-win2 driver from `localhost:3241` and has no
  detach: the driver drops a device when VIIPER's connection closes. After every SISR stop Arcadia still
  ends a leftover VIIPER from SISR's folder and runs `usbip detach -p N` for each `usbip port` entry from
  `usbip://localhost:3241/` (`CleanUpViiperAsync`). Why: on 2026-10-16 Steam hung at "loading user data"
  after SISR had been killed, and only a Windows restart helped (a stale device was suspected, not proven;
  `usbip port` was empty after a later kill). `usbip port` on the owner's PC while SISR runs (2026-10-17):
  `Port 01: device in use at Full Speed(12Mbps)`, then `Microsoft Corp. : Xbox360 Controller (045e:028e)` and
  `-> usbip://localhost:3241/1-1`. VIIPER always attaches from `localhost` (hard-coded in its auto-attach,
  `autoattach_windows.go`), and SISR starts its bundled VIIPER without arguments, so on port 3241 unless
  `VIIPER_USB_ADDR` is set (VIIPER `internal/server/usb/config.go`, SISR `input/viiperbridge.go`, read
  2026-10-17; a different port only means nothing is detached). Nothing is detached while another SISR or a
  viiper.exe from elsewhere runs: if that VIIPER holds 3241, SISR's own can't start and SISR uses the foreign
  one, so its devices share the address; on another port the skip is merely cautious (its port isn't known). So the address picks VIIPER's devices; a
  port remembered at SISR's start would miss devices attached later (controller reconnects) or after an
  Arcadia restart, and usbip reuses freed port numbers.
- Exiting Arcadia (#100, `ControllerSupportService.ShutdownAsync`): the shutdown cancels what waits (SISR coming up,
  Steam starting, a download under the gate; `_shutdown`), asks SISR to quit within 3 s and kills it after that.
  Before the fix SISR was only stopped politely: a SISR that was still `Starting` (API not up) never answered
  `quit`, `SisrHost.StopAsync` waited its 5 s before killing, and the gate wait of 2 s came on top, so the
  6 s that `App` waited ran out and Arcadia exited with SISR still running. Found by reading the code and
  reproduced with a fake SISR that ignores quit (`Exiting_while_SISR_starts_…`, red before the fix); not yet
  seen on the owner's PC (T100-1). `SisrHost.StopAsync` now also kills at once when the quit request wasn't accepted.
- The usbip-win2 installer runs with administrator rights, so it must be signed by the right publisher (#29,
  `InstallerSignature`, `UsbipRelease.Publisher`): before `runas`, `WinVerifyTrust` (chain, expiry, revocation, via
  Windows) must succeed and the signer certificate's subject must match organization `Cloudyne Systems (Scheibling
  Consulting AB)`, country `SE` and registration number (`SERIALNUMBER`) `559171-6567`. No thumbprint or serial
  number: they change with each renewal. Checked by the owner on the 0.9.8.1 installer on 2026-10-21 (`Get-AuthenticodeSignature`:
  Status Valid, issuer GlobalSign GCC R45 EV CodeSigning CA 2020, valid until 2027-05-03). A mismatch, an unsigned
  file or an offline revocation check aborts with a message and the link to the manual install; nothing starts. The
  file stays open for reading without write access from the check until the process runs, so it can't be swapped in
  between. The SHA-256 from the release API stays: it only shows that the download is what GitHub lists. When the
  publisher changes its certificate holder, installation stops working until `UsbipRelease.Publisher` is updated.
  The SISR zip (no admin rights) is not signature-checked.
- While SISR or VIIPER start or stop, in the background too, the settings take only B
  (`ControllerSupportService.IsSettling`, owner 2026-10-16). A Steam that runs but never finishes its login
  doesn't lock them, so switching off stays possible.
- SISR's own cleanup on quit opens `steam://forceinputappid/0` (SISR source, `cli/cmd/sisr/sisr.go`),
  which starts a closed Steam. That is why closing Steam (and switching off afterwards) started Steam again
  (owner, 2026-10-16). Unless Steam is ready Arcadia kills SISR instead of quitting it, also while Steam
  shuts down (steam.exe outlives the logout by a few seconds; quitting SISR then restarted Steam); `WatchAsync` does that
  within 2 s of Steam closing, the tick starts SISR again once Steam is ready (not counted as a crash), and
  the settings show "Steam läuft nicht" with "Steam starten".
- Arcadia does SISR's first-run setup itself so SISR never shows a window: `.initial_setup_done` next to
  `SISR.exe` (SISR's setup window shows while it is missing), the CEF flag, and the "SISR Marker" shortcut
  (`SISR.exe`, launch options `--marker`), written into `shortcuts.vdf` while "SISR installieren" has Steam
  closed. A missing marker counts as not set up ("SISR installieren", or "Anlegen" when on, both through
  the same prompts). Switching on starts Steam and waits up to 60 s (`SteamStartTimeout`); if
  Steam doesn't come up the setting is off again and the settings show "Steam startet nicht" with
  "Erneut versuchen"; if Steam runs but never finished its login, "Steam meldet sich nicht an" with "Steam
  neu starten", which switches on after the restart. Arcadia still doesn't start Steam on its own when it starts with the option on.
- SISR's WebView2 data lands in `%APPDATA%\SISR.exe`, VIIPER's key file in `%APPDATA%\VIIPER`; neither goes
  with SISR's own uninstaller (seen 2026-10-16). `ControllerSupportRemoval.DataDirectories` removes both.
