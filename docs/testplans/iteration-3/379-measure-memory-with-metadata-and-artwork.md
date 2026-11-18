# #379 Measure Arcadia's memory once metadata and artwork are loaded

No new test cases: a measurement, no behaviour changes; touches T636-6, T637-6, T637-7. The figures are in
[`docs/architecture/performance.md`](../../architecture/performance.md).

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| Private working set, real library (905 games), after the artwork and metadata run, 5 min idle, after paging | 2026-11-18 | Claude with the owner | v0.3.0-rc.1, Release, installed, Windows 11, owner's PC | ❌ 885 → 967 → 1,017 MB (budget 400 MB) | #636 |
| Resident set, `--demo --demo-games 1000` and `5000`, 10 s to 5 min | 2026-11-18 | Claude | `main` bc74a0c, Release, Linux container (Xvfb) | ❌ 899 → 916 MB (1,039 games), 1,113 → 1,118 MB (5,039 games) | #636 |
| Private working set, real library, start and 3 minutes | 2026-11-18 | Claude with the owner | v0.3.0-rc.2, Release, installed, Windows 11, owner's PC | ❌ 177 MB after the start, 630 → 694 MB within 2 minutes with Arcadia not in front (about 435 MB in front) | #680, #685 |
| Private working set, `--demo --demo-games 5000 --data-folder`, 1 minute | 2026-11-18 | Claude with the owner | v0.3.0-rc.2, Release, installed, Windows 11, owner's PC | ❌ 154 MB after the start, 640 MB after 1 minute with Arcadia not in front | #680, #685 |
| Comparison with other launchers | 2026-11-18 | Claude | – | not measured; no figure is given | – |
