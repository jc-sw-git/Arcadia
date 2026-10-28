# Arcadia — working notes for Claude

Arcadia is a game launcher that unites all other launchers: one tab per launcher, Big Picture
mode fully controllable by gamepad (incl. Steam Controller), desktop mode by mouse/keyboard.
The repo was formerly "SteamButler" (Rust); that code lives in `legacy/steambutler-rs/` as a
reference for porting (binary VDF codec, scanner ideas, architecture notes).

- **Designs (source of truth for UI):** https://claude.ai/artifact/8A9qxc67RcsuFmavaqCAuN
  (artboards: library, detail page, search, artwork modal, Spotify, launcher settings,
  localization, appearance, Big Picture settings, desktop mode). Read it before building UI.
- **Open work:** GitHub issues (label `roadmap`). One epic per area (`type:epic`)
  → tasks, all linked as GitHub sub-issues. Add new issues as a sub-issue of the matching epic
  (`gh api -X POST repos/jc-sw-git/Arcadia/issues/<epic>/sub_issues -F sub_issue_id=<issue id, not number>`).
  Features are `type:story` and start with a `> **Story:**` line. See `docs/roadmap.md`.
- **Refinement first:** before implementing an issue (own thread per issue unless the owner groups issues), refine
  it with the owner using the skill `.claude/skills/refinement`; rules in `docs/roadmap.md` → Refinement.
- **Language:** everything on GitHub is English (public repo): code, docs, issues, PRs, commits, branches,
  labels, milestones, release notes, comments. Chat with the owner is German; UI texts too, for now.

## Build, test, run

```bash
dotnet build Arcadia.slnx
dotnet test Arcadia.slnx
dotnet run --project src/Arcadia.App -- --demo        # demo libraries for all launchers
```

- .NET 10 SDK. `Directory.Build.props` sets `TreatWarningsAsErrors` — keep the build warning-free.
- A running Arcadia locks `bin/` → close it before building: `Get-Process Arcadia | % { $_.CloseMainWindow() }`
  (CloseMainWindow, not Stop-Process: Arcadia must shut SISR down cleanly).
- Logs: `%APPDATA%\Arcadia\logs\arcadia.log` (keys, gamepad buttons, `Bild:` press to frame, SISR start/stop),
  `sisr.log` (SISR's own log, overwritten per start). Settings: `%APPDATA%\Arcadia\settings.json`.
- Crashes before the first log line (e.g. XAML/style errors) only show in the Windows event log:
  `Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=(Get-Date).AddMinutes(-5)} | ? ProviderName -match '.NET Runtime'`.

## Releases

Windows x64 only, installer and self-update with Velopack (`tools/release/Pack.ps1 -Version x.y.z`); run the workflow
"Tag release" on `main` to release (cloud sessions can't push tags). `VelopackApp.Build()…Run()` must stay the first thing in `Program.Main`. Versioning (owner's
rule): SemVer, 0.x until 1.0; minor adds only non-breaking features, patch only fixes bugs (plugins are planned).
Update flow, uninstall, CI artifacts: `docs/releases.md`.

## Verifying UI changes

Tests cover Core/Launchers/ControllerSupport logic and, in `Arcadia.App.Tests`, the core flows through the view models
(acceptance tests, no rendering). For UI changes, run the app and
look at it — see the skill `.claude/skills/arcadia-verify/SKILL.md` (Windows and Linux; its pre-push checklist is a must) and
the scripts in `tools/dev/` (screenshot with key presses, foreground tracing, XInput probe, Steam shortcuts).
The owner often tests manually at the same time — say before driving the app with keys.

For bugs that aren't obvious from the code, or a fix that didn't hold, use the skill
`.claude/skills/diagnose` (a check that goes red on the bug comes before any hypothesis).

## Definition of Done

A change (PR) is done when all of this holds. Every feature iteration ends with a code review
issue (e.g. #66) that checks the whole milestone again, then an acceptance review of the test plans
(drift, deferred and end-only cases); this list keeps both small. Larger review findings become `type:tech-debt` issues at
the start of the next feature iteration. Process and test plan format: `docs/testplans/README.md`.

- **Builds and tests:** `dotnet build` without warnings, `dotnet test` green, CI green on all platforms.
  New logic in Core/Launchers/Input/ControllerSupport comes with tests, error paths included.
- **Robust:** no exception can end Arcadia. No unguarded `async void` or fire-and-forget task;
  unexpected errors are logged with stack trace to `arcadia.log` and shown as a toast.
  Network and process waits have a timeout.
- **Secure:** no user- or file-supplied string goes unchecked into a process start, path or URI;
  downloads are verified; no secrets or personal data in logs; admin rights only where unavoidable.
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
  `docs/testplans/iteration-<n>/` (IDs `T<issue>-<n>`, each marked `E2E: yes/no`) and records
  the test runs done during implementation with their real environment. If nothing new needs a
  manual test (e.g. a refactoring of features already covered by tests), the issue's section
  says so in one line with the reason. The `Testplan` CI check fails without the section.
- **Tested before merge:** all of the issue's tests (unit, integration, an acceptance test per `E2E: yes` case) are in
  the same PR before the owner tests; then the owner runs the issue's whole test plan file; feedback
  is fixed in one batch in the same thread (details: `docs/testplans/README.md` → Before the merge).
- **Performant:** no blocking work on the UI thread; code on a hot path (start, scan, library,
  input, running in the background beside a game) stays within the budgets below, measured when touched.

Budgets (Release build, owner's PC): start to usable library < 2 s with 1,000 games; gamepad
input to visible focus move < 50 ms; idle CPU < 1 % with gamepad polling; < 400 MB RAM with
5,000 games; a rescan never blocks the UI; no measurable cost to a running game.

**When criteria conflict**, the higher one wins: 1. security and privacy, 2. robustness and data
safety, 3. controller first, 4. performance, 5. readability and maintainability, 6. modularity and
scalability. Performance beats readability only when a measurement breaks a budget. A conflict
decided on purpose is named in the PR (and in the review issue).

**Iterations:** milestone "Iteration N": issues, each with all its tests → code review → acceptance review →
retrospective of the processes (own issue, keep, change or drop each) → release; then iteration N+1. There is no separate
acceptance iteration (owner 2026-10-25; "Abnahme Iteration 1" was the last). Details: `docs/testplans/README.md`.

## Layout

| Project | Content |
|---|---|
| `src/Arcadia.Launchers.Abstractions` | What a launcher is made of (`ILauncherIntegration` …); the future plugin interface |
| `src/Arcadia.Core` | Launcher registry and selection, library, search, settings, running games, keyboard layouts, paging |
| `src/Arcadia.Launchers` | `BuiltInLaunchers` (the one place a launcher is registered), Steam, Epic, Demo, game start |
| `src/Arcadia.Input` | Controllers via SDL3, gestures, mouse mode, `SendInput`, Steam desktop input filter |
| `src/Arcadia.ControllerSupport` | Steam Controller Support (SISR, usbip-win2) |
| `src/Arcadia.App` | Avalonia 12 UI (MVVM), `Theme/Theme.axaml`, `MainViewModel` (routing between pages) and the pages (`IPage`) |
| `tests/Arcadia.Core.Tests` | xUnit |
| `tests/Arcadia.App.Tests` | Acceptance tests (xUnit v3, Avalonia headless): `ArcadiaDriver` presses buttons on `MainViewModel`, `ArcadiaWindow` clicks and looks at the real window; rules in `docs/testplans/README.md` |

Which class does what: `docs/architecture/` (read it, or grep, rather than guessing). Big Picture UI is laid out at
1920×1080 inside a `Viewbox`. Focus/navigation is done in the view models (`MainViewModel.Handle(UiAction)` routes to the page on top), not with
Avalonia focus: buttons are `Focusable=False`, keys are handled in a tunnelling `KeyDown` handler.

## Rules that are easy to get wrong

**Product decisions** (agreed with the owner; don't change without asking) are in `docs/product/`, one file per
area. Read the file of the area you touch before changing it, and add new decisions there, not here:

| Area | File |
|---|---|
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

**Avalonia 12:**
- A locally set property (e.g. `Fill="…"`, `Stroke="…"`) beats any style — state colours must
  come entirely from styles (`Classes.x="{Binding …}"` + `Style Selector="….x"`).
- `RenderTransform` can't be animated with key frames → give the element its own
  `<RotateTransform/>` and animate `RotateTransform.Angle` (see `Path.spinner`).
- `SystemDecorations` is now `WindowDecorations`. Box shadows of `Button.plain` go on the
  template part: `Button.x /template/ Border#Chrome`.
- Fluent's `Button` and `ItemsControl` clip to their bounds, so an outer focus ring (BoxShadow) is cut off;
  `Theme.axaml` sets `ClipToBounds=False` on both. A `UserControl` clips too; set it on the one whose edge a ring reaches. A focus ring also needs room in clipping parents
  (`ScrollViewer` padding). With several BoxShadows, the later one paints on top.
- Compiled bindings: every `DataTemplate` needs `x:DataType`; reach the window's view model
  from inside a template with `$parent[Window].((vm:MainViewModel)DataContext)?.Prop`.
- A binding path through a property that can be null (`Detail.X`, `FocusedGame.X`, a `DataContext` not set yet)
  logs a binding error each time, `FallbackValue` or not. Write `Detail?.X` and give a value type a
  `TargetNullValue` (`IsVisible="{Binding Detail?.IsFromLibrary, TargetNullValue=False}"`). `XamlBindingTests` checks
  both on every `.axaml` against the App's properties declared nullable (`T? Name`), so declare nullable what can be null.

**Launcher and controller findings** (verified on the owner's PC; read the matching file before
touching that code, and add new findings there, not here):
- Steam library, installs, updates, account, running games, `shortcuts.vdf`: `docs/launchers/steam/` (README first, then the topic file)
- Epic launcher, catalog, install monitor, playtime: `docs/launchers/epic.md`
- Steam Controller Support (SISR, usbip-win2, focus handling): `docs/controller-support/` (README first, then the topic file)

## Context economy

Keep context small, every step resends it (owner, 2026-10-23):
- `CLAUDE.md` holds only what nearly every task needs. Everything else (a product decision, a finding, a
  workflow detail) goes into the topic file under `docs/` and at most gets a pointer here. Limits: `CLAUDE.md`
  15 KB, every other doc and skill 10 KB (test plan files exempt); the `Doc sizes` CI check enforces them. A file
  that would grow past its limit is cut into topics in the same PR (a folder with a `README.md` index, like
  `docs/controller-support/`), not trimmed of facts and not given a higher limit.
- Read only the docs of the area you touch (tables above); grep large files instead of reading them whole.
  The test plan is one file per issue: read only that issue's file.
- Keep tool output short: `dotnet build Arcadia.slnx -v q -clp:ErrorsOnly`, `dotnet test Arcadia.slnx -v q`
  (or `--filter` for the tests you changed), logs with `grep`/`Select-String` or `tail`, not whole.
- Screenshots only where a UI change needs looking at, and not repeated for unchanged screens.
- One issue (or group of issues that belong together) per thread; after the PR is merged, new work starts in a new thread.

## Git

- Commit and push to feature branches freely; never push to `main`. Changes reach `main` only through a PR
  that the owner reviews and merges.
- Branch names: `{prefix}/{issue-title}-{issue-number}` in kebab case, e.g. `fix/steam-scanner-crash-42`; without
  an issue, drop the number. Prefixes: `feat` (new or changed feature behaviour), `fix` (bug fix), `ci` (CI only),
  `test` (tests only), `doc` (docs only, agent skills included), `td` (any other product code change, i.e. tech
  debt). Rename generated names such as `claude/project-thread-…` before the first push.
- **Never** end commit messages with a `Co-Authored-By` trailer.
- Commit messages: imperative subject, body explains why.
- At most two open UI PRs at a time (the owner tests them one after the other). After each merge to `main`, check
  yourself that your open PR is still mergeable and resolve conflicts before the owner finds them.
- Threads work as personas (`docs/personas/`): a fresh quality engineer thread reviews and adds the acceptance
  tests to every code PR; Codex (`AGENTS.md`) reviews only the whole iteration.
- Opening a PR needs no OK (tell the owner the link). Link it to its issue, close it when all its PRs are merged
  (`docs/process/issues-and-prs.md`). Merges, releases, comments only on the owner's word.
- License: GPL-3.0-only. Third-party notes in `THIRD-PARTY-NOTICES.md`.
