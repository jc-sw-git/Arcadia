---
name: refinement
description: Joint refinement with the owner before work starts on an issue or epic — walk the likely scenarios, edge cases and open decisions in rounds of questions until nothing is left assumed. Use at the start of every issue (own thread per issue unless the owner groups issues; an epic only on request), or when the owner says "refinement" or "grill".
---

# Refinement

Adapted from `grilling` and `to-tickets` in [mattpocock/skills](https://github.com/mattpocock/skills) (MIT).
When it applies (per issue; epics on request) is set in `docs/roadmap.md` → "Refinement".

## 1. Read before asking

Read the issue or epic with its comments and sub-issues, the matching designs, and the code it touches.
Facts are your job: never ask the owner what a file, log, issue or the code can tell you. Decisions are
the owner's.

## 2. Ask in rounds

Map the work as a tree of decisions: each one opens the decisions that depend on it. A round asks the
whole **frontier**, every question whose prerequisites are settled. A question that depends on another
open question of the same round waits for the next round.

Cover, where they apply: the most likely player scenarios (controller first, B goes back), what happens
on errors and with missing launchers or data, what is explicitly out of scope, and which DoD criteria
are tricky for this work. Skip what `CLAUDE.md`, `docs/product/` or an earlier decision already settles.

Ask in German (chat), numbered, each with your recommendation, worded so "ja" accepts it:

```
**F1 – <title>:** <question, with options if any>
➡️ Empfehlung: <answer>
```

Keep rounds short (about five questions). After each answer, recompute the frontier.

**Done when** the frontier is empty and the owner confirms the shared understanding. Don't start
implementing before that.

## 3. Write the result into the issue

Update the issue body in English (it is the source of truth, not the chat): scenarios, decisions with date,
out of scope, and acceptance criteria as checkboxes. These become the test plan cases later.

## 4. Epic refinement (only on request): slice into issues

For an epic, after step 3 cut the agreed scope into issues:

- Each issue is a **vertical slice**: a self-contained increment that pays into the epic and can be
  tested on its own. Prefactoring comes first as its own issue.
- Guideline: the context an issue needs (code to read, docs, logs, discussion) stays under about
  **150k tokens**. More is a sign to split it further. Not a hard limit.
- Show the slicing as a numbered list (title, what it delivers, blocked by) and let the owner adjust
  it, then create the issues as sub-issues of the epic (see `CLAUDE.md` → Open work).
