# Pan: firmware hold (#242)

Owner's decision 2026-10-28 (option A): if Arcadia ends unexpectedly while Steam tries a firmware update through the
copy, Pan's teardown ends the copy but must not show the real puck, which could get the rest of the update. Pan keeps it
hidden until Steam is no longer running. Part of Pan's hiding protocol ([pan-hiding.md](pan-hiding.md)).

Arcadia sends `HoldForFirmware` (10, value 1/0, current generation, journaled before the
reply) while Steam tries an update through the copy. While held and Steam runs, every show is skipped (the retry
waits on) and `Unhide` is rejected `HeldForFirmware`; once Steam is closed Pan clears it and shows. A crash mid-attempt
thus keeps the puck hidden; the next `Hello` (or, during the teardown, the `TeardownAcknowledged` reply) answers
with reason `HeldForFirmware` (23) while Steam runs, and Arcadia opens the firmware dialog with the wait at once.
The lift (value 0) needs no emulation, so the release works even if the new emulation isn't hidden. `--restore`
always shows.

- Arcadia sets the hold when `FirmwareAttemptWatch` starts and lifts it when the attempt ends; the wish counts at once
  (a quit right after the end lifts it in `EndAsync` before `Unhide`), and a hold Pan didn't take is sent again at the
  next tick.
- A hold the journal can't save is rejected (`JournalUnreadable`) but stays in effect while Pan runs; a lift that
  isn't saved is no lift.
- A hold from before Arcadia's start is noted at every tick, whether Pan is switched on in Arcadia or not, once per hold.
