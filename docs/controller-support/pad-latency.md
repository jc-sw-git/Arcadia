# Latency of the emulated pads (#238 D9)

How a controller's report reaches its emulated pad with the least delay, for every taken-over controller
([every-controller.md](every-controller.md)) and the Steam Controller's Xbox pad ([xbox-pad.md](xbox-pad.md)).

Budget: added delay from the controller's report to the pad's report mean ≤ 1 ms, max ≤ 4 ms; Arcadia beside a game
≤ 1 % CPU of all cores.
- SDL has no call that waits for a report; it reads what arrived when asked. While any pad is plugged
  (`PanEmulation.PadsPlugged` → `IGamepadSource.ReadFast`) the source reads every 1 ms at high thread priority
  (`ReadPacing`), waiting on a high-resolution waitable timer (`PreciseWait`): `Thread.Sleep` waits for the 15.6 ms
  system tick, and Windows 11 ignores a raised timer resolution for a process whose window is hidden behind a game.
  `Polled` (Arcadia's own input, the mouse mode) keeps its 8 ms clock; only `Sampled` comes with every read. No pad:
  back to the 8 ms polls, so the idle budget is untouched.
- `PadForwarder` sends on the reading thread, no channel and no thread hop. A send VIIPER doesn't finish at once goes on
  where it ends; states read meanwhile wait in one slot (the newest wins), so a slow VIIPER never holds up the reads.
- `arcadia.log`, every 10 minutes and at the end: per pad "Verzögerung Lesen → VIIPER" (read to VIIPER having taken
  it: n, mean, p50, p99, max, from 10 µs buckets), and "Abstand der Lesevorgänge" (the most a report waits for its
  read) with Arcadia's CPU share over the same stretch. Added delay ≈ half the read gap + the forwarding delay; the
  bus side (usbip-win2 to the game) is not measured.
