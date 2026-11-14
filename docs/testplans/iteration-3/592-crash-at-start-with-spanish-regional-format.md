# #592 Arcadia crashes at start when the regional format is Spanish

**Status:** Automated: T592-1 to T592-4 · QE: T592-5 · Open for the owner: T592-6

- [x] **T592-1** In every region .NET knows, the header date, a running game's day and month and a news entry's date
  show without an error, the first two without the year, the last without the weekday. · E2E: yes
- [x] **T592-2** The Windows patterns of Spanish, Portuguese, Catalan, Russian, Bulgarian, Chinese, Hungarian,
  Lithuanian, German and English work as in T592-1; the header keeps the region's words ("sáb, 14 de nov",
  "11月14日") and drops the year's own word ("г."). · E2E: yes
- [x] **T592-3** A region pattern that can't be read (an open quote, a trailing backslash, a broken day and month
  pattern) shows a readable date instead of an error. · E2E: yes
- [x] **T592-4** Arcadia starts with the region es-ES, es-MX or pt-BR and shows the header date. · E2E: yes
- [x] **T592-5** Linux with `LANG=es_ES.UTF-8`: Arcadia starts with `--demo`, the header shows weekday, day and month in
  Spanish order ("sáb, 14 de nov"). · E2E: no (window)
- [ ] **T592-6** **Owner:** Windows, "Region" → "Regionales Format" Spanisch (Spanien), restart Arcadia: it starts, the
  header shows e.g. "Sa., 14 de Nov."; switch back to Deutsch afterwards. · E2E: no (real Windows region)

Automated tests:

| Test cases | Tests |
|---|---|
| T592-1 to T592-4 | `RegionDatePatternTests` |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T592-1 to T592-4 | 2026-11-14 | Claude | Debug, Linux cloud container, `dotnet test` on `main` before the fix | ❌ as expected (83 of 875 red: es, pt, ca, gl, da, lt, …) | fixed in this PR |
| T592-1 to T592-4 | 2026-11-14 | Claude | Debug, Linux cloud container, `dotnet test` | ✅ | – |
| T592-5 | 2026-11-14 | Claude | Debug, Linux/Xvfb, `LANG=es_ES.UTF-8`, `--demo`, on `main` before the fix | ❌ as expected: ends at start with `FormatException` in `Formatting.ShortDate` | fixed in this PR |
| T592-5 | 2026-11-14 | Claude | Debug, Linux/Xvfb, `LANG=es_ES.UTF-8`, `--demo` | ✅ starts, header "sáb, 14 de nov 8:58" | – |
