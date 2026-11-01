# Arcadia — working notes for Claude

Arcadia is a game launcher that unites all other launchers: one tab per launcher, Big Picture
mode fully controllable by gamepad (incl. Steam Controller), desktop mode by mouse/keyboard.

- **Designs (source of truth for UI):** https://claude.ai/artifact/8A9qxc67RcsuFmavaqCAuN
  (artboards: library, detail page, search, artwork modal, Spotify, launcher settings,
  localization, appearance, Big Picture settings, desktop mode). Read it before building UI.
- **Open work:** GitHub issues (label `roadmap`, next up `prio:next`), grouped by area with an `area:*` label (no
  epic issues). Parts cut from a large issue are its sub-issues. Features are `type:story` and start with a
  `> **Story:**` line; bugs `bug`; technical work `type:tech-debt`. Milestone `I<n>` = iteration n, its title names
  the theme, its description the scope.
- **Language:** everything on GitHub is English (public repo): code, docs, issues, PRs, commits, branches,
  labels, milestones, release notes, comments. UI texts are German, for now. Other launcher front ends are never
  named; write "comparable products".

## Build, test, run

```bash
dotnet build Arcadia.slnx
dotnet test Arcadia.slnx
dotnet run --project src/Arcadia.App -- --demo        # demo libraries for all launchers
```

- .NET 10 SDK. `Directory.Build.props` sets `TreatWarningsAsErrors` — keep the build warning-free.
- A running Arcadia locks `bin/` → close it before building: `Get-Process Arcadia | % { $_.CloseMainWindow() }`
  (CloseMainWindow, not Stop-Process: Arcadia must shut down cleanly).
- Logs: `%APPDATA%\Arcadia\logs\arcadia.log` (keys, gamepad buttons, `Bild:` press to frame, Pan and the emulation),
  `viiper.log` (VIIPER's own log). Settings: `%APPDATA%\Arcadia\settings.json`.
- Crashes before the first log line (e.g. XAML/style errors) only show in the Windows event log (command: arcadia-verify skill).

## Releases

`VelopackApp.Build()…Run()` must stay the first thing in `Program.Main`. Packaging, update flow, uninstall, versioning
(SemVer, 0.x until 1.0) and how to release ("Tag release" workflow; cloud sessions can't push tags): `docs/releases.md`.

## Verifying UI changes

Tests cover Core/Launchers/ControllerSupport logic and, in `Arcadia.App.Tests`, the core flows through the view models
(acceptance tests, no rendering). For UI changes, run the app and
look at it — see the skill `.claude/skills/arcadia-verify/SKILL.md` (Windows and Linux; its pre-push checklist is a must) and
the scripts in `tools/dev/` (screenshot with key presses, foreground tracing, XInput probe, Steam shortcuts).
The owner often tests manually at the same time — say before driving the app with keys.

For bugs that aren't obvious from the code, or a fix that didn't hold, use the skill
`.claude/skills/diagnose` (a check that goes red on the bug comes before any hypothesis).

## Definition of Done

A change (PR) is done when all of this holds. The acceptance review at the end of each iteration checks the whole
milestone again; this list keeps it small.

- **Builds and tests:** `dotnet build` without warnings, `dotnet test` green, CI green on all platforms.
  New logic in Core/Launchers/Input/ControllerSupport comes with tests, error paths included.
- **Robust:** no exception can end Arcadia. No unguarded `async void` or fire-and-forget task;
  unexpected errors are logged with stack trace to `arcadia.log` and shown as a toast.
  Network and process waits have a timeout.
- **Secure:** no user- or file-supplied string goes unchecked into a process start, path or URI;
  downloads are verified; no secrets or personal data in logs; admin rights only where unavoidable;
  test data and docs use made-up ids, names and paths, never the owner's.
- **Controller first:** every new flow works with a gamepad alone (no mouse/keyboard), B goes back.
  Anything that can't (e.g. UAC) is named in the PR.
- **Readable:** names say what things are; methods do one thing; comments explain *why*, not *what*.
  A developer new to the code understands the change from the diff and `CLAUDE.md`.
- **Modular:** launcher-specific code stays in its launcher folder; UI and Core don't special-case launchers.
  A launcher is one `ILauncherIntegration`, registered in `BuiltInLaunchers`; built-in launchers use only
  `Arcadia.Launchers.Abstractions`, as a plugin would.
- **Themeable:** no colour, brush or shadow values in views or styles; they are resources in
  `Theme/Theme.axaml`, so a theme (later possibly a plugin) can replace them all in one place.
- **Verified for real:** UI, input, launcher and Steam Controller changes were run and looked at
  (`.claude/skills/arcadia-verify`); what was only checked mechanically is said so.
- **Docs current:** `CLAUDE.md` / `docs/` updated when a fact, rule or workflow changed.
- **Test plan:** a PR that closes an issue adds that issue's manual tests as its own file in
  `docs/testplans/iteration-<n>/` (IDs `T<issue>-<n>`, each marked `E2E: yes/no`) and records the test runs done
  during implementation with their real environment; if nothing new needs a manual test, the file says so in one
  line with the reason. The `Testplan` CI check fails without it.
- **Tested before merge:** all of the issue's tests (unit, integration, an acceptance test per `E2E: yes` case) are in
  the same PR before the owner tests it with the issue's test plan file (`docs/testplans/README.md`).
- **Performant:** no blocking work on the UI thread; code on a hot path (start, scan, library,
  input, running in the background beside a game) stays within the budgets below, measured when touched.

Budgets (Release build, owner's PC): start to usable library < 2 s with 1,000 games; gamepad
input to visible focus move < 50 ms; idle CPU < 1 % with gamepad polling; < 400 MB RAM with
5,000 games; a rescan never blocks the UI; no measurable cost to a running game; latency the controller emulation
adds from a controller's report to its emulated pad: mean ≤ 1 ms, p99 ≤ 2 ms, max ≤ 4 ms
(`docs/controller-support/pad-latency.md`).

**When criteria conflict**, the higher one wins: 1. security and privacy, 2. robustness and data
safety, 3. controller first, 4. performance, 5. readability and maintainability, 6. modularity and
scalability. Performance beats readability only when a measurement breaks a budget. A conflict
decided on purpose is named in the PR (and in the review issue).

## Layout

Projects `src/Arcadia.*` and `tests/`; which project and class does what: `docs/architecture/` (read it, or grep,
rather than guessing). Big Picture UI is laid out at 1920×1080 inside a `Viewbox`. Focus/navigation is done in the
view models (`MainViewModel.Handle(UiAction)` routes to the page on top), not with Avalonia focus: buttons are
`Focusable=False`, keys are handled in a tunnelling `KeyDown` handler.

## Rules that are easy to get wrong

**Product decisions** (agreed with the owner; don't change without asking) are in `docs/product/`: the principles
in its `README.md`, one file per area. Read the file of the area you touch before changing it. A decision that outlives
its issue or PR goes into the file it fits (a new file only when none fits), not here and not only into the issue;
before adding it, look for a contradicting decision there or elsewhere and report one to the owner instead of deciding.
What is needed only to implement an issue or finish a PR stays in that issue or PR.

| Area | File |
|---|---|
| Principles: vision, launchers opt-in, feature parity, controllers alike, platforms, performance, plugins | `docs/product/README.md` |
| Library, page sizes, sorting, header tab strip | `docs/product/library.md` |
| Buttons and keys (gamepad, WASD), button hints, dialogs | `docs/product/controls.md` |
| A / game start, detail page, game actions, updates | `docs/product/detail-page.md` |
| Search | `docs/product/search.md` |
| On-screen keyboard | `docs/product/on-screen-keyboard.md` |
| Mouse mode, "Zurück zu Arcadia", Steam's desktop layout | `docs/product/mouse-mode.md` |
| Settings page, status messages | `docs/product/settings.md` |
| Launchers: activate and order, launcher page, first start | `docs/product/launchers.md` |
| Running games, Arcadia back to the front | `docs/product/running-games.md` |
| UI texts: wording, length, listing new texts in the PR | `docs/product/ui-texts.md` |

**Errors:** a game start goes through `GameLaunchService.TryStartAsync`, which never throws. Work nobody awaits
(timer handlers, `Task`s started from sync code) runs through `AppErrors.Guard`/`.Forget(context)`: unexpected
errors land with stack trace in `arcadia.log` and as a toast. No `_ = SomethingAsync()` in the App.

**Avalonia 12** (styles beating local values, focus rings and clipping, compiled and nullable bindings): read
`docs/architecture/avalonia.md` before changing `.axaml` or styles.

**Launcher and controller findings** (verified on the owner's PC; read the matching file before
touching that code, and add new findings there, not here):
- Steam library, installs, updates, account, running games, `shortcuts.vdf`: `docs/launchers/steam/` (README first, then the topic file)
- Epic launcher, catalog, install monitor, playtime: `docs/launchers/epic.md`
- Steam Controller Support (Pan, VIIPER, usbip-win2, HidHide, removing SISR): `docs/controller-support/` (README first, then the topic file)

## Context economy

Every step of a session resends its whole context, so keep it small:
- `CLAUDE.md` holds only what nearly every task needs. Everything else (a product decision, a finding, a
  workflow detail) goes into the topic file under `docs/` and at most gets a pointer here. Limits: `CLAUDE.md`
  15 KB, every other doc and skill 10 KB (test plan files exempt); the `Doc sizes` CI check enforces them. A file
  that would grow past its limit is cut into topics in the same PR (a folder with a `README.md` index, like
  `docs/controller-support/`), not trimmed of facts and not given a higher limit.
- A doc states the current behaviour and the rules, as short as possible and as detailed as necessary: no dates, no
  who decided, no earlier states, nothing the code already says. A changed fact is updated in place, not appended.
- Read only the docs of the area you touch (tables above); grep large files instead of reading them whole.
  The test plan is one file per issue: read only that issue's file.
- Keep tool output short: `dotnet build Arcadia.slnx -v q -clp:ErrorsOnly`, `dotnet test Arcadia.slnx -v q`
  (or `--filter` for the tests you changed), logs with `grep`/`Select-String` or `tail`, not whole.
- Screenshots only where a UI change needs looking at, and not repeated for unchanged screens.

## Git

- Commit and push to feature branches freely; never push to `main`. Changes reach `main` only through a PR
  that the owner reviews and merges.
- Branch names: `{prefix}/{issue-title}-{issue-number}` in kebab case, e.g. `fix/steam-scanner-crash-42`; without
  an issue, drop the number. Prefixes: `feat` (new or changed feature behaviour), `fix` (bug fix), `ci` (CI only),
  `test` (tests only), `doc` (docs only, agent skills included), `td` (any other product code change, i.e. tech
  debt). Rename generated names such as `claude/project-thread-…` before the first push.
- Commit messages: imperative subject, body explains why.
- After each merge to `main`, check that your open PR is still mergeable and resolve conflicts before the owner
  finds them.
- Opening a PR needs no OK. Link it to its issue, close it when all its PRs are merged
  (`docs/process/issues-and-prs.md`); the PR gets the sections **Handoff** and **Review** (`docs/personas/README.md`).
  Merges, releases, comments only on the owner's word.
- License: GPL-3.0-only. Third-party notes in `THIRD-PARTY-NOTICES.md`.
