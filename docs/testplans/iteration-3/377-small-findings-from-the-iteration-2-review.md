# #377 Small findings from the Iteration 2 review

No manual case: each item is a latent fault without a known effect on a player today (as the issue says), so there is
nothing a player could see change. Every item with logic has unit tests; the Windows-only parts (Pan's service
rollback, the secured data folder, usbip-win2's version) are checked by build and review only, as their seams need a
real Windows service. Touches T238-11 (latency summary), T214-9 (settings file) and T304-6 (usbip teardown).

## Test runs

| What | Date | Who | Environment | Result | Notes |
|---|---|---|---|---|---|
| `dotnet test` Core and Pan | 2026-11-13 | Claude | Linux cloud container, .NET 10 | pass | Core 3,270, Pan 444 (before the last three items) |
