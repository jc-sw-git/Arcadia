# #642 Own titles and tags can be lost after a power cut

**Status:** Automated: T642-1 · QE: none · Open for the owner: none

- [x] **T642-1** Own titles and tags are flushed to the disk before the rename replaces the old file (`WriteDurably`).
  The artwork and metadata caches are not: they can be fetched again, and a flush per file slowed the Windows test
  runs. A power cut can't be tested by hand. · E2E: no (a power cut can't be triggered)

Automated tests:

| Test cases | Tests |
|---|---|
| T642-1 | `CacheFilesTests.A_durable_write_flushes_the_new_file_to_the_disk_and_not_only_to_the_cache`, `UserDataStoreTests.A_change_is_flushed_to_the_disk_before_the_file_is_replaced` |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T642-1 | 2026-11-18 | Claude | PR branch, Linux, `dotnet test` filtered | ✅ (77 tests of the cache and user data classes) | – |
| T642-1 | 2026-11-18 | Claude | as above, with `Flush()` put back in the helper | ❌ red as intended (the test sees no flush to disk) | – |
| T642-1 | 2026-11-18 | Claude | PR branch, CI Windows, flush for every cache file | ❌ `DetailMetadataTests.While_1000_slow_store_…` failed twice (no first request in 10 s) | Flush only for own titles and tags; caches as before |
| T642-1 | 2026-11-18 | Claude | PR branch, Linux, `Flush()` put back in the helper | ❌ both flush tests red as intended; green with the fix (79 cache and user data tests) | – |
