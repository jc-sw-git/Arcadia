# #377 Small findings from the Iteration 2 review

No new test cases: latent faults without a known effect on a player, covered by unit tests (the Windows-only service rollback, secured data folder and usbip-win2 version read by build and review only); touches T238-11, T214-9 and T304-6.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| `dotnet test` Core and Pan: Core 3,270, Pan 444 | 2026-11-13 | Claude | Linux cloud container, .NET 10 | ✅ | – |
| `tools/ci/run-tests.sh 4` after a Release build: Core 3,271, Pan 444, App shards 238 + 229 + 225 + 210 | 2026-11-13 | Claude | same | ✅ | – |
| Demo library under Xvfb: covers without artwork in their hues, the monogram toned down: placeholders as before, now from `Theme.axaml` | 2026-11-13 | Claude | same, `tools/dev/screenshot-arcadia.sh` | ✅ | – |
| QE review: Release build, `dotnet test`: Pan 444, Core 3,271, App 947; no warnings | 2026-11-13 | Claude (QE) | Linux cloud container, .NET 10, PR head `fc8e53c` | ✅ | – |
| QE own run: demo library and A on a demo game under Xvfb: placeholder hues and toned-down monogram as before; no error in `arcadia.log` | 2026-11-13 | Claude (QE) | same, `tools/dev/screenshot-arcadia.sh` | ✅ | – |
| QE findings fixed: `StatusModalTests` (stalled update), `DirectLauncherClientTests` (refused target), App tests on installs and demo starts | 2026-11-13 | Claude | Linux cloud container, .NET 10 | ✅ | – |
