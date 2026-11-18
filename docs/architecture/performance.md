# Measured performance

What the budgets in `CLAUDE.md` came to when measured, and what was found. Release builds; "owner's PC" is a Windows 11
PC with 905 games from two launchers (Steam signed in, covers and metadata loaded); "Linux" is the cloud container under
Xvfb with `--demo --demo-games N --data-folder <temp>` (made-up images, resident set, not comparable to Windows).
Measure again with the same commands when a hot path changes; the log lines `Start:`, `Bibliothek:`, `Bild:` and
`Speicher: aufgeräumt` carry the figures. The `Bibliothek:` line's memory leaves out most of the process: read the
private working set in the Task Manager (Details → "Arbeitsspeicher (privater Arbeitssatz)").

## Against the budgets

| Budget | Measured | State |
|---|---|---|
| RAM < 400 MB with 5,000 games | owner's PC, rc.1: 885 MB after the artwork and metadata run, 967 MB after 5 min idle, 1,017 MB after paging. rc.2 (905 games): 177 MB right after the start, about 435 MB with Arcadia in front and idle, a trim every minute | over (#685); rc.2 also grew to 694 MB with Arcadia not in front, cause #680 |
| Start to usable library < 2 s with 1,000 games | rc.1: 3.2 s (905 games). rc.2: `Bibliothek im Bild` 1,976 ms cold, 2,384 ms after a Windows restart with Pan running; reading the library takes 0.3 s, the window the rest | borderline (#637) |
| Gamepad input to visible focus < 50 ms | rc.2, page size 128: page turn median 14.9 ms, p95 34 ms, max 61 ms; focus move median 1.7 ms, p95 7 ms; 332 presses of a session: median 4.9 ms, p95 35 ms | holds at p95; single presses reach 61 ms |
| The same with 5,039 demo games, filter "Tags" | median 16.1 ms, p95 46.3 ms; the worst press (543 ms) came 0.7 s after an artwork run had copied 10,078 files | holds at p95, not during an artwork run |
| Idle CPU < 1 % | rc.2 after a restart during the emulation: about 20 % of one core, a loading spinner kept the window drawing (#680, fixed) | over until #680 |
| A rescan never blocks the UI | no blocked UI thread seen in any run | holds |

## Memory

- **Real library (905 games, rc.1):** 885 MB when the artwork and metadata run ended, +82 MB in the next 7 minutes of
  idling, peak 908 MB while it loaded (about 9 minutes for 497 games). Artwork cache on disk 1.0 GB (2,674 files), metadata
  cache 2.4 MB.
- **Why it was high (#636):** Avalonia keeps about 400 bytes per element for every style whose selector might match it;
  813,000 such records sat in the three on-screen keyboards that stay built. Grouping styles by view and giving keys and
  button chrome their own types cut them to 137,000 (`docs/architecture/avalonia.md`). Linux, 5,000 demo games: resident
  set 972 → 251 MB. Linux base before the fix: about 850 MB regardless of the number of games, about 55 MB more per
  1,000 games.
- **What is left (#685):** with 905 games and Arcadia in front, about 435 MB private: about 220 MB managed (115 to 120 MB
  of it Avalonia's style system, 35,000 UI objects with 8.5 style entries each) and about 200 MB native (Skia, textures,
  decoded covers, SDL, the runtime, 115 threads; not yet broken down).
- **Garbage at idle:** the GC doesn't run on its own at idle. `MemoryTrim` collects once after 64 MB of allocation and
  10 s without input, only while Arcadia is in front and no game runs (a pause would stall the pads' forwarding), so
  beside a game memory stays where it was; a collection takes 240 to 280 ms.
- **Comparison with other launchers:** not measured. The memory of other products was not taken on the owner's PC, so
  no figure is given here and none is estimated.

## Start

The start's `Start: …` line gives each step in ms since the process started. rc.2 on the owner's PC, cold: Avalonia
342 ms, settings 417, services 478, window built 746, library read 771, window open 1,969, library on screen 1,976. With
the emulation running (Pan, VIIPER) the window needs 1.2 to 1.4 s from its construction to open.
