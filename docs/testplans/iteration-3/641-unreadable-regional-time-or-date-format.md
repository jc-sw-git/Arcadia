# #641 A regional time or date format .NET cannot format stops Arcadia at start

**Status:** Automated: T641-1, T641-2, T641-3 · QE: none · Open for the owner: none

- [x] **T641-1** A time or short date pattern .NET can't format (unterminated quote, trailing backslash) shows the invariant time or date everywhere (header clock, running-since time, date and time, release date); nothing throws · E2E: yes
- [x] **T641-2** Arcadia builds its composition and the header under es-ES, pt-BR, ar-SA, th-TH, fa-IR, ja-JP and de-CH with each "Uhrzeit" setting, and with a broken time and short date pattern · E2E: yes
- [x] **T641-3** An unexpected error while Arcadia builds its window gives a window with the path of the log and "Schließen" (Enter, Esc) instead of Arcadia vanishing; the stack trace is in `arcadia.log` · E2E: no (window; a build error can't be forced in the running app, the window is checked headless)

Automated tests:

| Test cases | Tests |
|---|---|
| T641-1 | `UnreadableRegionFormatTests` (broken time pattern, broken short date pattern) |
| T641-2 | `UnreadableRegionFormatTests` (tricky regions, broken patterns through `ArcadiaDriver`) |
| T641-3 | `UnreadableRegionFormatTests.An_error_at_start_shows_a_window_…` |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T641-1 to T641-3 | 2026-11-18 | Claude | PR branch, Release, Linux cloud container | ✅ the T641-1 and T641-2 tests failed before the fix; `tools/ci/run-tests.sh 4`: all passed. `--demo` run in xvfb starts and logs "gestartet". The Windows regional settings themselves were not run; a broken pattern is faked through a cloned culture | – |
