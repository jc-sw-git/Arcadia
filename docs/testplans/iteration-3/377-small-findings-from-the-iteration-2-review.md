# #377 Small findings from the Iteration 2 review

No manual case: each item is a latent fault without a known effect on a player today (as the issue says), so there is
nothing a player could see change. Every item with logic has unit tests; the Windows-only parts (Pan's service
rollback, the secured data folder, usbip-win2's version) are checked by build and review only, as their seams need a
real Windows service. Touches T238-11 (latency summary), T214-9 (settings file) and T304-6 (usbip teardown).

## Test runs

| What | Date | Who | Environment | Result | Notes |
|---|---|---|---|---|---|
| `dotnet test` Core and Pan | 2026-11-13 | Claude | Linux cloud container, .NET 10 | pass | Core 3,270, Pan 444 |
| `tools/ci/run-tests.sh 4` after a Release build | 2026-11-13 | Claude | same | pass | Core 3,271, Pan 444, App shards 238 + 229 + 225 + 210 |
| Demo library under Xvfb: covers without artwork in their hues, the monogram toned down | 2026-11-13 | Claude | same, `tools/dev/screenshot-arcadia.sh` | pass | placeholders as before, now from `Theme.axaml` |
| QE review: Release build, `dotnet test` | 2026-11-13 | Claude (QE) | Linux cloud container, .NET 10, PR head `fc8e53c` | pass | Pan 444, Core 3,271, App 947; no warnings |
| QE own run: demo library and A on a demo game under Xvfb | 2026-11-13 | Claude (QE) | same, `tools/dev/screenshot-arcadia.sh` | pass | placeholder hues and toned-down monogram as before; no error in `arcadia.log` |
