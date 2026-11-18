# #638 "Launcher-Daten" switched off asks no store

**Status:** Automated: T638-1, T638-2, T638-3 · Open for the owner: none

- [x] **T638-1** With "Launcher-Daten" off a scan sends no store request · E2E: yes
- [x] **T638-2** With it off "Alle Metadaten neu laden" sends no store request · E2E: yes
- [x] **T638-3** Switching it off during a run stops the requests; switching it on asks only the games still missing data · E2E: yes

Automated tests: `MetadataServiceTests` (`With_launcher_data_off_…`, `Switching_launcher_data_off_during_a_run_…`), each counting the fake store's requests.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T638-1 to T638-3 | 2026-11-18 | Claude | PR branch, Debug, Linux cloud container | ✅ new tests red without the fix, green with it; `dotnet build` without warnings, `dotnet test` all passed | – |
