# #568 Artwork and metadata caches live in Velopack's install folder

**Status:** Automated: T568-1 to T568-3b · QE: – · Open for the owner: T568-4

- [x] **T568-1** An old `%LOCALAPPDATA%\Arcadia\artwork` and `…\metadata` move with all files (own images included) to
  `%APPDATA%\Arcadia` at the start; the old folders are gone. · E2E: yes
- [x] **T568-2** If a new cache already exists, its files stay and only files missing there are added. · E2E: yes
- [x] **T568-3** Without old caches the start does nothing and creates no folder. · E2E: yes
- [x] **T568-3b** A cache that cannot be moved (e.g. a file where the folder should go) stays, does not throw and does not stop the other one. · E2E: yes
- [ ] **T568-4** **Owner:** Windows, with a hand-picked cover: run the setup over the installed Arcadia ("overwrite"/repair);
  the cover is still there, `%LOCALAPPDATA%\Arcadia` holds no `artwork` or `metadata`. · E2E: no (real setup)

Automated tests:

| Test cases | Tests |
|---|---|
| T568-1 to T568-3b | `OldCacheLocationTests` |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T568-1 to T568-3b | 2026-11-14 | Claude | Debug, Linux cloud container, `dotnet test` | ✅ | – |
