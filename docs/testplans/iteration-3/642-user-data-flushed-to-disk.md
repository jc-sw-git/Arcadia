# #642 Own titles and tags can be lost after a power cut

**Status:** Automated: T642-1 · QE: none · Open for the owner: none

- [x] **T642-1** The shared write helper (own titles and tags, artwork and metadata caches) flushes the temporary file
  to the disk before the rename replaces the old file; the old file is still whole until then. A power cut can't be
  tested by hand. · E2E: no (a power cut can't be triggered)

Automated tests:

| Test cases | Tests |
|---|---|
| T642-1 | `CacheFilesTests.A_write_flushes_the_new_file_to_the_disk_and_not_only_to_the_cache` |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T642-1 | 2026-11-18 | Claude | PR branch, Linux, `dotnet test` filtered | ✅ (77 tests of the cache and user data classes) | – |
| T642-1 | 2026-11-18 | Claude | as above, with `Flush()` put back in the helper | ❌ red as intended (the test sees no flush to disk) | – |
