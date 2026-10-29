# Pan: firmware hold (#242)

If Arcadia ends unexpectedly while Steam tries a firmware update through the copy, Pan's teardown ends the copy but must
not show the real puck, which could get the rest of the update. Pan keeps it hidden until Steam is no longer running.

- Arcadia sends `HoldForFirmware` (value 1/0, current generation, journaled before the reply) when `FirmwareAttemptWatch`
  starts and lifts it when the attempt ends; the wish counts at once (a quit right after the end lifts it in `EndAsync`
  before `Unhide`), and a hold Pan didn't take is sent again at the next tick.
- While held and Steam runs, every show is skipped (the retry waits on) and `Unhide` is rejected `HeldForFirmware`;
  once Steam is closed Pan clears it and shows. The next `Hello` (or, during the teardown, the `TeardownAcknowledged`
  reply) answers with reason `HeldForFirmware` while Steam runs, and Arcadia opens the firmware dialog with the wait at
  once. The lift needs no emulation, so the release works even if the new emulation isn't hidden. `--restore` always
  shows.
- A hold the journal can't save is rejected (`JournalUnreadable`) but stays in effect while Pan runs; Arcadia sends it
  again every 10 s. A lift that isn't saved is in effect too (a restarted Pan then holds until Steam is closed), and
  Arcadia's release lifts also a hold Pan rejected.
- A hold from before Arcadia's start is noted at every tick, whether Pan is switched on in Arcadia or not, once per hold.
