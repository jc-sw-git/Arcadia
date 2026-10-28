# Roadmap

Open work is tracked as **GitHub issues** (label `roadmap`, next up: `prio:next`).
One epic per area (`type:epic`); the tasks hang as sub-issues on their epic. Features are `type:story`
and start with a story line, bugs `bug`, technical work `type:tech-debt`.
Manual tests per iteration: `docs/testplans/`.

Designs: `docs/designs/` (PNG + sources), canvas: https://claude.ai/artifact/8A9qxc67RcsuFmavaqCAuN

## Iterations

From Iteration 2 each iteration has a theme instead of a single epic (owner, 2026-10-25): the GitHub milestones
`I2`, `I3`, `I4` … name it in their title, their description is the scope. A theme can span several epics and
tech-debt issues. How an iteration runs: `docs/process/iteration.md`.

## Refinement

Before code is written, owner and Claude walk through the most likely scenarios together
(skill `.claude/skills/refinement`). The result then lives in the issue (owner, 2026-10-14):

- **Per issue, in its own thread** (owner, 2026-10-25): every issue is refined on its own first. Issues that
  refine and ship better together are grouped only when the owner says so. An epic is refined as a whole
  (and sliced into issues) only on the owner's request.
- **Who leads** (owner, 2026-10-26): the product manager for stories, the architect for tech debt, spikes and
  CI (`docs/personas/`).
- Every issue is a self-contained increment that pays into the epic.
- Guideline: the context an issue needs (code, docs, logs, discussion) stays below about
  150k tokens. More is a sign to split the issue further; not a hard limit.
- Every refinement ends with a pass over failure cases (unknown state, crash, restart, interrupted
  halfway) and a list of assumptions only the owner's PC can prove, each checked by a script before
  implementation (owner, 2026-10-28; details in the skill).
- An issue cut this way is implemented without a new refinement; the thread settles open questions
  briefly with the owner instead of starting a new round of questions (retro Iteration 1, #150).
