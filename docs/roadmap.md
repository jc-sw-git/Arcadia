# Roadmap

Open work is tracked as **GitHub issues** (label `roadmap`, next up: `prio:next`).
Roadmap: https://github.com/jc-sw-git/Arcadia/issues/26 – below it one epic per area (`type:epic`)
as a sub-issue; the tasks hang as sub-issues on their epic. Features are `type:story`
and start with a story line, bugs `bug`, technical work `type:tech-debt`.
Manual tests per iteration and the flow from feature to acceptance iteration: `docs/testplans/`.

Designs: `docs/designs/` (PNG + sources), canvas: https://claude.ai/artifact/8A9qxc67RcsuFmavaqCAuN

## Refinement

Before code is written, owner and Claude walk through the most likely scenarios together
(skill `.claude/skills/refinement`). The result then lives in the issue (owner, 2026-10-14):

- **Iteration 1 (rest):** every issue still being worked on is refined on its own first.
- **From Iteration 2:** one refinement per epic that pins down its scope. The result is
  cut into lean issues that hang on the epic as sub-issues.
- Every issue is a self-contained increment that pays into the epic.
- Guideline: the context an issue needs (code, docs, logs, discussion) stays below about
  150k tokens. More is a sign to split the issue further; not a hard limit.
- An issue cut this way is implemented without a new refinement; the thread settles open questions
  briefly with the owner instead of starting a new round of questions (retro Iteration 1, #150).

## Done (groundwork)

- Solution structure: Core, Launchers, Input, ControllerSupport, App, Tests
- Library in the Big Picture layout (1920×1080, scaled to the window)
- Launcher tabs, horizontally scrollable with edge arrows, LB/RB
- Paged grid (10/25/50/100) with the rule "larger only if the smaller size doesn't already show everything"
- Sorting by name, install date, last played, each ascending/descending
- "View" modal (Y), operable by controller
- Preview panel on the left (last played, playtime, size, launcher, location)
- Update marker (icon and hint; A starts the game, the launcher updates first)
- Controller via XInput, keyboard, mouse
- Epic scanner (ported from SteamButler), demo data with `--demo`
- Epic games also start when Epic isn't running (Epic is started in the background with `-silent`)
- Steam Controller support via SISR: installation, start/stop with progress indicator, pause for Steam games,
  override per game, monitoring with warnings and fix button, update check, focus handling
- Settings dialog (F2 / View button) for Steam Controller support
- Reading/writing Steam's `shortcuts.vdf` (checked byte for byte against the real file)
- License GPL-3.0-only, `CLAUDE.md`, verification skill and dev scripts
