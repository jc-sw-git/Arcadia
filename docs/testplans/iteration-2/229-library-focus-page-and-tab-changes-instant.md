# #229 Make focus, page and tab changes in the library feel instant

**The measurement:** every key or button press writes one line to `arcadia.log` once the first frame after it is laid
out and handed to the renderer (`Services/InputLatency`):

```
Bild: NextTab nach 16.3 ms (Warten 0.1, Verarbeitung 2.4)
```

`nach` is the whole time from the press to that frame; `Warten` the part before the UI thread got to the press (gamepad:
from the poll thread), `Verarbeitung` the view models' share; the rest is binding, layout and render. The render thread
and the screen add a few milliseconds no app can see. Read it with
`Select-String "Bild:" $env:APPDATA\Arcadia\logs\arcadia.log | Select -Last 30`. Focus moves are `Left`/`Right`/`Up`/`Down`,
pages `NextPage`/`PreviousPage`, tabs `NextTab`/`PreviousTab` or `Launcher n` (number keys). The first press after the start
is slower (code compiled on first use). ⧉ and ≡ act only on release or after 0.2 s held alone (#64); their time still
counts from the press, as the user waited from there. Several quick presses can share one frame, so a line says when its
press was shown, not that each step between was drawn on its own.

**The fix:** a page or tab change built every cover's controls again (25 to 100 covers, each a button with badges, paths
and a progress bar). The grid now draws `LibraryViewModel.Slots`, places that stay and only take the new tiles, as the
search's hits already did, so the controls are bound again instead of built again. A page or tab change now zooms the
focused cover in like a focus move (the controls stay, so the zoom's transition runs).

- [ ] **T229-1** Turn pages (RT/LT) and tabs (RB/LB) in a tab with more than one page: the same covers show the new games,
  the last page shows only its games (no empty places, no extra row), the focus ring sits on the first cover · E2E: yes
  (`LibrarySpeedTests.Turning_a_page_or_tab_binds_the_same_covers_to_the_new_games`)
- [ ] **T229-2** Every press with keys and with the controller writes its `Bild:` line · E2E: yes
  (`LibrarySpeedTests.Every_key_and_button_press_logs_its_time_to_the_frame_that_shows_it`); ⧉/≡ tapped or held count
  from the press, pressed together they log nothing (`…View_and_Menu_count_their_time_from_the_press_not_from_when_they_act`)
- [ ] **T229-3** Measure on the owner's PC (Release) with `--demo --demo-games 2400` (Xbox tab ~1,040 games), `5000` and the
  real Steam library: 20 × right, 10 × down, 10 × RT, 12 × RB; before (first commit of the PR, measurement only) and after.
  Focus move < 50 ms; page and tab change noticeably faster; feels instant when switching quickly between large tabs, also
  when held (key repeat) with the log on ·
  E2E: no (real GPU, screen and the owner's feel)

Unchanged behaviour: the transition tests (T155-*, T208-*, T209-*, T210-*) and every other App test stay green with their
expectations unchanged; only `ArcadiaWindow.Tile` finds a cover by its place instead of its tile.

**Numbers in the cloud container** (Release, Linux, Xvfb with software rendering, keyboard via xdotool, 25 per page; average,
max in brackets, ms):

| Library | Build | Focus right | Focus down | Page (RT) | Tab (RB) |
|---|---|---|---|---|---|
| `--demo-games 2400` | before | 7.8 (57) | 3.0 (9) | 106.8 (169) | 105.1 (166) |
| `--demo-games 2400` | after | 9.3 (67) | 3.9 (18) | 16.5 (21) | 16.3 (37) |
| `--demo-games 5000` | after | 8.1 (72) | 4.5 (31) | 15.7 (20) | 19.5 (32) |

The max of a focus move is the first press (compiled on first use). Focus moves were already within budget there; what
the owner felt on focus moves may be #227 (the install folder checked on the UI thread) or the PC's renderer: T229-3 shows it.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T229-1, T229-2, all App and Core tests | 2026-10-26 | Claude | PR branch, Linux cloud container, Avalonia headless | ✅ (each also ❌ once with the code broken on purpose, see the PR) | – |
| T229-1 looked at, measurement above | 2026-10-26 | Claude | PR branch, Release, Linux, Xvfb, `--demo` / `--demo-games 2400`/`5000` | ✅ | T229-3 on the owner's PC |
| T229-2 with ⧉/≡ (Codex review), merged with main (#230), all App and Core tests | 2026-10-26 | Claude | PR branch, Linux cloud container, Avalonia headless | ✅ (the new case also ❌ once with the press time taken on acting) | – |
