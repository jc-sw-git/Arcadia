# Steam gets a copy of the Steam Controller (#242)

While Arcadia emulates, Pan hides the real puck from every program but Arcadia ([pan-hiding.md](pan-hiding.md)) and
Steam gets a VIIPER device with the puck's descriptors that Arcadia feeds from the raw puck
([steam-controller-copy-windows.md](steam-controller-copy-windows.md)). Without the pinned VIIPER build, or off Windows,
the App passes `NoSteamControllerCopy` (`CanCopy` false): Pan hides nothing and only Arcadia's Xbox pad works.

## Lifecycle (`PanEmulation`)

- One emulation generation per run, under Pan's watch: VIIPER runs (`IVirtualXboxPads.PrepareAsync`, so Pan takes its
  `viiper.exe` at the registration), `EmulationStarted`, `Hide(generation)`, the copy attaches, then Arcadia's Xbox
  pad comes and goes inside it as games need. Undone in reverse: pad and copy, `Unhide`, `EmulationStopped`.
- Hide once at the start (or when the puck arrives) and show at the end: switching a game's mode or starting a game
  never makes the controller drop out.
- No hiding, no emulation: a hide that fails leaves the controller as it is and is tried again after 10 s; a copy
  that doesn't attach ends the emulation, so Pan shows the puck again. `HideAsync` waits up to 25 s (the puck
  restarts, ~2.5 s) and can't be cancelled once sent: a half-done hide would leave the puck hidden with nothing in
  its place. A rejected hide is undone by the client at once.
- Every tick asks Pan `CheckHiding(generation)`; `HidingLost` (cloaking off, the client off the allow list, an
  instance no longer hidden) ends pad and copy at once, with the notices "Pan: Controller-Wächter pausiert …" and,
  once it runs again, "Pan: Controller-Wächter läuft wieder".
- More than 3 failures in 5 minutes (copy lost or not attaching, the pad giving up) stop it: "Emulation angehalten"
  until "Erneut versuchen" or "Controller wiederherstellen".
- A teardown by Pan (Arcadia hung) is acknowledged and a new generation starts in the same tick, at most once per
  tick (a Pan that ends every new generation at once would restart the puck in a loop).
- A pad or copy that fails to go leaves the puck hidden: `EmulationStopped` without `Unhide`, so Pan's teardown ends
  the forwarding (`viiper.exe`) before it shows the puck.

## Consent before every hide (`SteamControllerTakeover`)

A hide restarts the puck and would break off a firmware update Steam runs on the real one, and from outside nobody can
tell whether one runs. So with Steam running Arcadia asks ("Steam Controller und Puck übernehmen?") and hides nothing
until "Ja" is held 3 s; without Steam it takes over at once (checked again right before the hide: a Steam started
meanwhile turns it into the question).

- The consent holds while the emulation runs without a gap (a puck plugged in meanwhile is taken over too: Steam never
  saw it); every gap (paused, Pan's teardown, a hide that didn't hold, switched off) asks again. "Nein" (held 1 s) and
  a release for a firmware update hold until Arcadia's next start or the settings row "… übernehmen" (held 3 s, the
  press is the consent). Meanwhile nothing is emulated, Arcadia's Xbox pad included.
- The open question follows what is connected (title, chips, warning), as SDL reports the wireless Steam Controller
  only after the puck at Arcadia's start; a change stops a running hold on "Ja".
- Arcadia asks only once Pan can hide (`CheckHiding` first): a question the hide can't follow would come back every
  tick and teach the player to hold "Ja" by reflex.
- Short gap mid-game: while a library game stays in front, Steam's update window can't be, so a gap under 10 s with
  the same game in front at every check (every tick, at most 5 s apart) is taken over again without asking, checked
  once more right before the hide. A missed check (Arcadia hung) asks. Open risk: an update offered in Steam's
  in-game overlay leaves the game in front.
- A pad still being unplugged stays in the session until it is gone, so the end shows the puck only after it.

## Who gets input (`SteamControllerCopyRules`)

- The copy passes input when a game on "Steam-Eingabe" runs, or no game runs and Steam itself is in front
  (`steam.exe`, `steamwebhelper.exe`; checked at most every 250 ms). Never while Arcadia is in front or the mouse mode
  is on. Arcadia's pad passes input for a game on "Arcadia", but not while Steam is in front with no game running.
- At rest Steam reads a controller at rest: buttons cleared except Steam (`0x10000`) and "…" (`0x10`), triggers,
  sticks, trackpads and gyro zeroed; sequence, IMU timestamp, accelerometer and orientation stay. Battery (`0x43`)
  and wireless state (`0x46`, `0x79`) always pass. Arcadia's combo ⧉ + ≡ never reaches Steam (`ComboWithholding`).

## What Steam may send the puck (`SteamControllerCopyFilter`)

No bricked Steam Controllers under any circumstances: an allow list, everything else dropped.

| Kind | Types | Passes |
|---|---|---|
| Feature, report id 1 or 2, type at byte 1 | `0x81 0x82 0x83 0x85 0x87 0x8E 0xAE 0xB4 0xC1` | always |
| Haptic features | `0x8F 0xEA 0xEB` | only while the copy passes input |
| Haptic outputs | report ids `0x80`–`0x85` | only while the copy passes input |
| Firmware | `0xB7 0xB8 0xB9` | never; update traffic (below) |
| Anything else (`FACTORY_RESET` `0x86`, `SET_SERIAL_NUMBER` `0xA9`, `ENABLE_PAIRING` `0xAD`, radio and dongle `0xAF`–`0xB3`, unknown) | — | never |

- Reads (GET_REPORT) always pass: they change nothing. Each dropped type is logged once, with its name and length,
  never its payload.
- Every drop but the routine ones (`0xA3 0xDC 0xE2 0xED 0xF3`, haptics while the copy rests) raises `UpdateTraffic`:
  Steam's real update path for the Triton is unverified (spike #295), so anything unknown counts.
  `FirmwareAttemptWatch` then brings Arcadia to the front with "Steam will die Firmware aktualisieren": after 5 s
  without traffic a 25 s countdown runs (new traffic starts it anew); only then can "Controller freigeben" or
  "Arcadia beenden" (held 1 s) be chosen, as releasing the real puck mid-attempt could hand it the rest of the update.
  "Steam hat das Update abgebrochen" (held 3 s) is the player's own way out of a misread; "Schließen" keeps the
  takeover. While the attempt runs, Pan holds the puck hidden even if Arcadia ends unexpectedly
  ([pan-firmware-hold.md](pan-firmware-hold.md)). `arcadia.log` names every message that counted and how the attempt
  ended. No bootloader type is known; it would be dropped and counted all the same.
- What Windows' class drivers say to any device (HID idle and protocol, CDC line coding and control line state) is
  housekeeping: answered, never passed on, never counted (`SteamReportKind.Housekeeping`). Other CDC requests (a
  break, an encapsulated command), vendor and unknown control requests count.

The settings rows, the greyed mode and the dialogs: `docs/product/settings.md`, `docs/product/controls.md`. Off
Windows the emulation's state is `Unsupported`: nothing is emulated and the mode doesn't show.
