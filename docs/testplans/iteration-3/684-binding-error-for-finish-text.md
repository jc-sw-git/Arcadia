# #684 The log shows binding errors for the button hint's "FinishText"

**Status:** Automated: T684-1 · QE: T684-2 · Open for the owner: none

- [x] **T684-1** Every page, dialog and settings category of the guards' page list is opened and walked: Avalonia logs no `[Binding]` warning on any of them · E2E: yes
- [x] **T684-2** Release build with `--demo`, the settings with every category, the first start's step "Datenquellen" (`--first-run` or a new data folder) and the search opened: `arcadia.log` has no `[Binding]` line, and on the first start's step the footer's ≡ hint reads "Überspringen" and "Fertig" once a key is entered · E2E: no (real log and the visible footer)

Automated tests:

| Test cases | Tests |
|---|---|
| T684-1 | `Guards/NoBindingErrorsTests` (a guard: runs on `main`) |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T684-1 (before the fix) | 2026-11-18 | Claude | `main` 1277ef1, Debug, headless, Linux cloud container | ❌ `Selected.FinishText … Value is null` on every settings category, the launcher page, the driver installation and the own tags | fixed |
| T684-1 | 2026-11-18 | Claude | PR branch, Debug, headless, Linux cloud container | ✅ | – |
| T684-2 | 2026-11-18 | Claude (QE) | PR branch f9ef4a2, Release, headless/xvfb, Linux cloud container | ✅ no `[Binding]` line in `arcadia.log`; the first start's footer shows "≡ Überspringen" (the state "Fertig" was not looked at) | – |

The player saw nothing: the ≡ hint is hidden outside the first start, only the log filled up.
