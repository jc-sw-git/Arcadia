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
- Puck or cable (#345): the copy is of the controller by cable when it is plugged in, else of the puck
  (`ISteamControllerHold.Connected`). A controller on the puck stays there when the cable is plugged in (it charges);
  only one plugged in switched off is `1302`. When the controller changes devices (cable unplugged: it goes to the
  puck; switched off and plugged in by cable) the copy follows: the old copy goes, Pan hides once more with Arcadia
  letting go (so the new device is restarted before the copy opens it), then the new copy attaches. A move is no
  copy failure; a hide refused then ends the emulation, so Steam gets the real controller. For up to 10 s
  (`FollowWait`) only the new device is copied, as Pan's restart takes it away for a moment. The puck stays hidden
  while the cable is copied, so a second controller on the puck is hidden without a copy (two controllers: #342).
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

## Firmware updates

Arcadia takes Steam Controller and puck over without asking: while the copy runs, no update Steam starts reaches the
hardware. The notice, "Controller freigeben" and Pan's bootloader check: [steam-controller-firmware.md](steam-controller-firmware.md).

- A pad still being unplugged stays in the session until it is gone, so the end shows the puck only after it.

## Who gets input (`SteamControllerCopyRules`)

- The copy passes input when a game on "Steam-Eingabe" runs, or Steam itself is in front while no game or only
  games on "Arcadia" run (`steam.exe`, `steamwebhelper.exe`; `ForegroundOwner`, looked up when the foreground
  process changes). With "Steams Desktop-Steuerung zulassen" on
  (#315) it also passes while Arcadia is in front, the mouse mode is on or no game runs: all Steam Controller input
  goes to the copy except to a game on "Arcadia" (then the Xbox pad). With the setting off, never while Arcadia is in
  front or the mouse mode is on. Arcadia's pad passes input only while a game on "Arcadia" is in front (#357), never together with the copy.
  The setting is read on every report; `SteamControllerCopyGate` logs each change ("Kopie für Steam: …").
- At rest Steam reads a controller at rest: buttons cleared except Steam (`0x10000`) and "…" (`0x10`), triggers,
  sticks, trackpads and gyro zeroed; sequence, IMU timestamp, accelerometer and orientation stay. Battery (`0x43`)
  and wireless state (`0x46`, `0x79`) always pass. Arcadia's combo ⧉ + ≡ reaches Steam only while the setting is on
  (`ComboWithholding`, `SteamControllerCopyRules.CombosKeptFromSteam`).

## What Steam may send the puck (`SteamControllerCopyFilter`)

No bricked Steam Controllers under any circumstances: an allow list, everything else dropped.

| Kind | Types | Passes |
|---|---|---|
| Feature, report id 1 or 2, type at byte 1 | `0x81 0x82 0x83 0x85 0x87 0x8E 0x9F 0xAE 0xB4 0xC1` | always |
| Haptic features | `0x8F 0xEA 0xEB` | only while the copy passes input |
| Haptic outputs | report ids `0x80`–`0x85` | only while the copy passes input |
| Firmware | `0x90 0xB7 0xB8 0xB9` | never; raises the firmware notice |
| Anything else (`FACTORY_RESET` `0x86`, `SET_SERIAL_NUMBER` `0xA9`, `ENABLE_PAIRING` `0xAD`, radio and dongle `0xAF`–`0xB3`, unknown) | — | never |

- Reads (GET_REPORT) always pass: they change nothing. Each dropped type is logged once, with its name and length,
  never its payload.
- Only firmware messages raise `UpdateTraffic` ([steam-controller-firmware.md](steam-controller-firmware.md)); every
  other drop is logged and nothing more.
- What Windows' class drivers say to any device (HID idle and protocol, CDC line coding and control line state) is
  housekeeping: answered, never passed on (`SteamReportKind.Housekeeping`). Other CDC requests (a break, an
  encapsulated command), vendor and unknown control requests are refused.

The settings rows, the greyed mode and the dialogs: `docs/product/settings-controller.md`, `docs/product/controls.md`. Off
Windows the emulation's state is `Unsupported`: nothing is emulated and the mode doesn't show.
