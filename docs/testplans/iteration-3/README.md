# Test plan Iteration 3

Milestone "I3: SteamGridDB integration and more, polished settings". Format and rules: [README](../README.md). The
ticks are set by the owner's full test before the merge.

**Acceptance review:** release candidate –, by –, on –.

One file per issue. A PR that closes an issue adds its file and lists it here; whoever tests an issue reads only
its file.

| File | Issue | Title | Test cases |
|---|---|---|---|
| [370](370-guard-the-remaining-unguarded-paths.md) | #370 | Guard the remaining unguarded paths | T370-1 to T370-6 |
| [376](376-ci-hygiene.md) | #376 | CI hygiene: job timeouts, SHA-pinned actions, Dependabot, concurrency, split the App tests | none (CI only) |
| [413](413-show-the-progress-of-pans-driver-installation.md) | #413 | Show the progress of Pan's driver installation | T413-1 to T413-29 |
| [415](415-move-controller-and-pan-block-out-of-mainviewmodel.md) | #415 | Move the controller and Pan block out of MainViewModel | none (refactoring) |
| [418](418-explain-back-pan-install-and-removal.md) | #418 | Explain-back: Pan install and removal | none (doc fixes) |
| [420](420-split-launchersettingsviewmodel-by-concern.md) | #420 | Split LauncherSettingsViewModel by concern | none (refactoring) |
| [422](422-pan-finishes-interrupted-uninstall.md) | #422 | Pan finishes an interrupted uninstall and removes itself without Arcadia | T422-1 to T422-12 |
| [434](434-pan-service-test-race.md) | #434 | Pan service test fails when a Pan process exits while it is checked | none (test race, automated test only) |

## Release candidate tests (restart or installation)

Cases that need a Windows restart or an Arcadia installation, tested on the release candidate, outside the 30 cases
of the acceptance review.

| Issue | Test cases |
|---|---|
| #422 | T422-4, T422-8, T422-11, T422-1, T422-3, T422-9, T422-12 (in this order, one PC with Pan, the drivers and the puck) |

## Exploratory test

Charter: –. Time box: –. Findings: –.

## Acceptance tests

| Test cases | Tests |
|---|---|
