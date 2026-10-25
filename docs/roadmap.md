# Roadmap

Open work is tracked as **GitHub issues** (label `roadmap`, next up: `prio:next`).
Roadmap: https://github.com/jc-sw-git/Arcadia/issues/26 – below it one epic per area (`type:epic`)
as a sub-issue; the tasks hang as sub-issues on their epic. Features are `type:story`
and start with a story line, bugs `bug`, technical work `type:tech-debt`.
Manual tests per iteration and the course of an iteration: `docs/testplans/`.

Designs: `docs/designs/` (PNG + sources), canvas: https://claude.ai/artifact/8A9qxc67RcsuFmavaqCAuN

## Iteration themes

Iterations 2–4 each have a theme instead of a single epic (owner, 2026-10-25); the GitHub milestones
carry the prefix `I2`, `I3`, `I4`. A theme can span several epics and tech-debt issues.

- **I2:** reduce tech debt (slimmer code, more modularisation), polish SISR / Steam Controller support
  and the general Steam integration.
- **I3:** SteamGridDB integration and more, polished settings.
- **I4:** new launchers, focusing on the new Epic launcher.

Milestone descriptions on GitHub are the source for the exact scope.

## Refinement

Before code is written, owner and Claude walk through the most likely scenarios together
(skill `.claude/skills/refinement`). The result then lives in the issue (owner, 2026-10-14):

- **Per issue, in its own thread** (owner, 2026-10-25): every issue is refined on its own first. Issues that
  refine and ship better together are grouped only when the owner says so. An epic is refined as a whole
  (and sliced into issues) only on the owner's request.
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
