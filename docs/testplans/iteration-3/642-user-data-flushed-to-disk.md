# #642 Own titles and tags can be lost after a power cut

**Status:** Automated: T642-1 · Open for the owner: none

- [x] **T642-1** The shared write helper (own titles and tags, artwork and metadata caches) flushes the temporary file
  to the disk before the rename replaces the old file; the old file is still whole until then. A power cut can't be
  tested by hand. · E2E: no (a power cut can't be triggered)

Automated tests:

| Test cases | Tests |
|---|---|
| T642-1 | `CacheFilesTests.Writing_has_the_content_flushed_before_it_replaces_the_old_file` |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T642-1 | 2026-11-18 | Claude | PR branch, Linux, `dotnet test` filtered | ✅ (78 tests of the cache and user data classes) | – |
