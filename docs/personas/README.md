# Personas

Every thread works as one persona for one step of the workflow. A persona is not a character: it is what a
thread reads, what it delivers, what it doesn't do and when it is done. Reading only what the step needs keeps
the thread small and focused.

| Persona | File | Step |
|---|---|---|
| Product manager | [product-manager.md](product-manager.md) | Refinement of stories |
| Software architect | [architect.md](architect.md) | Refinement of tech debt, spikes, CI; implementation; decision proposals |
| Designer | [designer.md](designer.md) | Screen designs |
| Quality engineer / code reviewer | [quality-engineer.md](quality-engineer.md) | Code review and acceptance tests of a PR; decision reviews |

An issue goes refinement (product manager for a story, architect for tech debt, spikes, CI; a designer when a screen
is new or changes visibly) → implementation (architect) → review and acceptance tests (quality engineer, in the same
PR) → the owner's manual test, whose feedback and the open review findings are fixed in one batch. Codex reviews
only the whole iteration (`AGENTS.md`).

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

A thread that finds work for another persona hands it over the same way; it doesn't take over that work itself.
