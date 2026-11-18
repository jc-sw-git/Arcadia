# #632 Flaky: MetadataServiceTests cleanup fails on Windows CI while a cache write is open

**Status:** no new cases; T547-3 keeps its meaning and stays automated.
**Open for the owner:** nothing.

No new manual test cases: the failure was in the test's cleanup, not in Arcadia; touches T547-3.

`MetadataServiceTests.Dispose` deleted the test folder while a cache write's temporary file could still be open
(Windows refuses that). The cleanup now retries the delete for up to 2.5 s.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| `MetadataServiceTests` (26 tests) | 2026-11-18 | implementation | PR branch, Debug, Linux cloud container | ✅ 26 passed (the race only shows on Windows; the full Windows run is CI's) | – |
