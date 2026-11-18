# #640 A key typed with the gamepad can be rebuilt from the log

**Status:** Automated: T640-1

- [x] **T640-1** A secret typed key by key with D-pad and A on the on-screen keyboard leaves no `Gamepad:` line and no `Bild:` line naming a button in `arcadia.log`; outside of typing the presses are still logged. · E2E: yes

Automated tests:

| Test cases | Tests |
|---|---|
| T640-1 | `KeyTypedByGamepadStaysOutOfLogTests` |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T640-1 | 2026-11-18 | Claude | PR branch, Debug, Linux container, headless; red without the fix | ✅ | – |
