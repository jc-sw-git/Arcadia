# Personas

Every thread works as one persona for one step of the workflow (owner, 2026-10-26; trial for all of
Iteration 3, compared with Iteration 2 in that iteration's retro). A persona is not a character: it is what a
thread reads, what it delivers, what it doesn't do and when it is done. Reading only what the step needs keeps
the thread small and focused.

| Persona | File | Step |
|---|---|---|
| Product manager | [product-manager.md](product-manager.md) | Refinement of stories |
| Software architect | [architect.md](architect.md) | Refinement of tech debt, spikes, CI; implementation; decision proposals |
| Designer | [designer.md](designer.md) | Screen designs |
| Quality engineer / code reviewer | [quality-engineer.md](quality-engineer.md) | Code review and acceptance tests of a PR; decision reviews |

## Course of an issue

1. **Refinement:** the product manager for a story, the architect for `type:tech-debt`, spikes, CI and pure
   refactorings. A story with a large technical part stays with the product manager, who reads `architect.md`
   at the end and adds "Technical cut and risks" to the issue. If a screen needs a design, the refinement
   asks for a designer; the design flows back into the issue before the refinement ends.
2. **Implementation:** the architect. Code, unit and integration tests, the issue's test plan file, PR.
3. **Review and acceptance tests:** a fresh quality engineer thread, in the same PR.
4. **Owner's manual test:** in the implementation thread, which fixes the owner's feedback and any open
   review findings in one batch (`docs/testplans/README.md` → Before the merge).

Codex reviews only in the acceptance review at the end of the iteration (owner, 2026-10-26), not per PR.

## Decisions

- **Product and design:** the owner decides; personas propose with a recommendation.
- **Technical, during refinement:** the architect proposes, the owner approves.
- **Technical, during implementation:** small and reversible details (class cut, names, test approach) the
  architect decides and names in the PR.
- **Hard to reverse** (plugin interface, new dependency, on-disk format, process model): the architect writes
  a short proposal (options, recommendation, risks) into the issue; a fresh quality engineer thread reviews
  it with the architect's and its own checklist, without adopting the recommendation; both go to the owner.

## Handoffs

A thread hands over through GitHub, never through its chat: the next thread reads the handoff, not the thread
before it. After refinement the issue is the handoff. After implementation the PR description gets a short
section **Handoff**: done, deliberately not done, decisions with reasons, tricky spots, affected test cases.

A thread that finds work for another persona asks the coordinator for a thread, only for the next step of the
course above or for a designer. It doesn't take over that work itself.

## What the coordinator passes on

1. The persona file and the step (refinement, implementation, review, design, decision review).
2. The issue; for review, the PR.
3. Only the area files from the table in `CLAUDE.md` and the issue's test plan file (paths, not read by the
   coordinator).
4. Where the previous handoff is.
5. The result expected, when the thread is done, and who comes next.
6. Model and effort per the owner's overview.
