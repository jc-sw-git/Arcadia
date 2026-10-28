# Steam gets a copy of the Steam Controller (#242)

Builds on spike #239 ([emulated-steam-controller.md](emulated-steam-controller.md)) and Pan (#243,
[pan-hiding.md](pan-hiding.md)). Part A (logic, settings, tests) is in place; the copy itself needs the VIIPER fork
(part B: Go device type, no IN repeat, a CI-built pinned `viiper.exe`) and its Windows side (part C: raw HID reader
and writer for the puck, the VIIPER client, SDL ignoring the copy). Until then the App passes `NoSteamControllerCopy`
(`CanCopy` false): Pan hides nothing and Arcadia's Xbox pad works as in #213.

## Lifecycle (`PanEmulation`)

- One emulation generation per run, under Pan's watch: VIIPER runs (`IVirtualXboxPads.PrepareAsync`, so Pan takes
  its `viiper.exe` at the registration), `EmulationStarted`, `Hide(generation)`, the copy attaches, then Arcadia's
  Xbox pad comes and goes inside it as games need. Undone in reverse: pad and copy, `Unhide`, `EmulationStopped`.
- Hide once at the start (or when the puck arrives) and show at the end (owner's decisions 1, 5, 6): switching a game's
  mode or starting a game never makes the controller drop out.
- No hiding, no emulation: a hide that fails (puck didn't restart, HidHide changed nothing) leaves the controller as
  it is and is tried again after 10 s; a copy that doesn't attach ends the emulation, so Pan shows the puck again.
- Every tick asks Pan `CheckHiding(generation)`. Pan keeps what it hid for that generation (image path, instances) and
  answers `HidingLost` (22) when cloaking is off, its client left the allow list or an instance is no longer hidden
  (HidHide's configuration client, an uninstall). Arcadia then ends pad and copy at once, notices "Pan:
  Controller-Wächter pausiert. Die echten Controller werden nicht mehr versteckt." and, once it runs again, "Pan:
  Controller-Wächter läuft wieder".
- More than 3 failures in 5 minutes (copy lost or not attaching, the pad giving up) stop it: "Emulation angehalten"
  until "Erneut versuchen" or "Controller wiederherstellen".
- A teardown by Pan (Arcadia hung) is acknowledged and a new generation starts in the same tick, at most once per tick
  (a Pan that ends every new generation at once would restart the puck in a loop).
- A pad or copy that fails to go leaves the puck hidden: `EmulationStopped` without `Unhide`, so Pan's teardown ends
  the forwarding (`viiper.exe`) before it shows the puck.
- Firmware guard before every hide (QE review of #286): no automatic hide while Steam is in front, or within 3 minutes
  (`FirmwareGrace`) after Steam was in front while it could read the real puck (paused, off, no puck yet). A hide
  restarts the puck and would break off an update Steam started on the real one. Meanwhile the row "Pan wartet auf
  Steam" (info) shows and nothing is emulated, Arcadia's Xbox pad included.
- `HideAsync` waits up to 25 s (the puck restarts, ~2.5 s) and can't be cancelled once sent: a half-done hide would
  leave the puck hidden with nothing in its place. A rejected hide is undone by the client at once.

## Who gets input (`SteamControllerCopyRules`)

- The copy passes input when a game on "Steam-Eingabe" runs, or no game runs and Steam itself is in front
  (`steam.exe`, `steamwebhelper.exe`; checked at most every 250 ms). Never while Arcadia is in front or the mouse mode
  is on. Arcadia's pad passes input for a game on "Arcadia", but not while Steam is in front with no game running.
- At rest Steam reads a controller at rest: buttons cleared except Steam (`0x10000`) and "…" (`0x10`), triggers,
  sticks, trackpads and gyro zeroed; sequence, IMU timestamp, accelerometer and orientation stay. Battery (`0x43`)
  and wireless state (`0x46`, `0x79`) always pass. Arcadia's combo ⧉ + ≡ never reaches Steam (`ComboWithholding`).

## What Steam may send the puck (`SteamControllerCopyFilter`)

Decision 8 (owner: "no bricked Steam Controllers under any circumstances"): an allow list, everything else dropped.

| Kind | Types | Passes |
|---|---|---|
| Feature, report id 1 or 2, type at byte 1 | `0x81 0x82 0x83 0x85 0x87 0x8E 0xAE 0xB4 0xC1` | always |
| Haptic features | `0x8F 0xEA 0xEB` | only while the copy passes input |
| Haptic outputs | report ids `0x80`–`0x85` | only while the copy passes input |
| Firmware | `0xB7 0xB8 0xB9` | never; raises `FirmwareBlocked` once per run |
| Anything else (`FACTORY_RESET` `0x86`, `SET_SERIAL_NUMBER` `0xA9`, `ENABLE_PAIRING` `0xAD`, radio and dongle `0xAF`–`0xB3`, unknown) | — | never |

- Reads (GET_REPORT) always pass: they change nothing.
- Each dropped type is logged once, with its name and length, never its payload.
- `FirmwareBlocked` brings Arcadia to the front with "Firmware-Update gestoppt" (how to update: end Arcadia, run the
  update in Steam, start Arcadia again).
- Open: `0xA3`, which Steam sent on every slot in the spike, has no known name and is dropped; whether Steam misses it
  needs the owner's test with part C. Steam's real firmware path for the Triton is unverified (no update was offered
  in the spike); the dialog fires only for the three known types, unknown ones are dropped anyway. No bootloader type is
  known, so decision 8's "bootloader → dialog" never fires (it is dropped all the same).
- For part C: GET_REPORT always passes, but Arcadia's own SDL sends feature reports to the same puck too, so a feature
  transaction (SET then GET) needs one owner at a time, or Steam may read SDL's answer. The VIIPER fork hands every
  control request (class and vendor too) to `FromSteam`; it never answers or forwards one itself.

## UI (designs, section "#242 Pan")

The group "PAN" in the controller settings (`PanRows`), the greyed mode with the triangle, the header's spinner and
triangle ("Pan läuft nicht"): see `docs/product/settings.md` and `docs/product/controls.md`. Off Windows the
emulation's state is `Unsupported`: nothing is emulated and the mode doesn't show.
