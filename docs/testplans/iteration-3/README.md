# Test plan Iteration 3

Milestone "I3: SteamGridDB integration and more, polished settings". Format and rules: [README](../README.md). The
ticks are set by the owner's full test before the merge.

**Acceptance review:** release candidate –, by –, on –.

One file per issue, named `<issue>-<title>.md`; the folder is the index. Whoever tests an issue reads only its
file: its cases, its automated tests and its history. `tools/dev/testplan-index.sh` prints the overview
(file, issue, title, cases open for the owner).

## Release candidate tests (restart or installation)

Cases that need a Windows restart or an Arcadia installation, tested on the release candidate, outside the 30 cases
of the acceptance review.

| Issue | Test cases |
|---|---|
| #422 | T422-4, T422-8, T422-11, T422-1, T422-3, T422-9, T422-12 (in this order, one PC with Pan, the drivers and the puck) |
| #166 | T166-8 (the update from the status modal on the installed release candidate) |
| #423 | T407-1, then T407-2 (uninstall with Ja and UAC accepted; reinstall before restarting); T423-1 with T422-4 |
| #382 | T382-30, then T382-31 (Windows unrestarted in between), T382-32, T382-33, T382-34 (the update end to end) |
| #497 | T497-15 (the first start on a PC or Windows user without Arcadia settings) |

## Exploratory test

Charter: –. Time box: –. Findings: –.
