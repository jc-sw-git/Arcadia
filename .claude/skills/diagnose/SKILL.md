---
name: diagnose
description: Disciplined diagnosis of hard bugs and performance regressions in Arcadia — build a check that goes red on this bug first, then minimise, hypothesise, instrument, fix with a regression test, clean up. Use when a bug report is not obvious from the code, a first fix didn't hold, or something is slow.
---

# Diagnose

Adapted from `diagnosing-bugs` in [mattpocock/skills](https://github.com/mattpocock/skills) (MIT).
Skip a phase only with a stated reason.

**Redact first.** Never show secrets or personal data from logs or files (`localconfig.vdf` holds app
tickets, Epic files hold account ids): write `<REDACTED>` and quote only the lines that carry the signal.

## 1. Build a check that goes red on this bug

This is the skill: with a reliable red/green signal the cause will be found; without one, reading
code is guessing. Options, roughly in this order:

1. **xUnit test** at the seam that reaches the bug (`tests/Arcadia.Core.Tests`).
2. **Replay real data**: copy the owner's log lines or files (`arcadia.log`, `sisr.log`,
   `EpicGamesLauncher.log`, `content_log.txt`, manifests, `catcache.bin`) into a test fixture and run
   the parser or monitor over them. Most launcher bugs end here (see `docs/launchers/`).
3. **Running app**: `--demo` plus the scripts in `tools/dev/` (keys + screenshot, foreground trace,
   XInput probe); see `.claude/skills/arcadia-verify`.
4. **Owner in the loop**, last resort, for what only happens on the owner's PC (Steam Controller,
   SISR, focus, real installs): copy `hitl-loop.template.ps1`, write the steps, have the owner run it
   (or run it through Remote Control) and read the captured answers. Say before driving the app.

Done when there is **one command** you have already run that drives the real code path, asserts the
owner's exact symptom (not "didn't crash"), gives the same verdict every run and takes seconds. If no
such check is possible, say so, list what you tried and ask for an environment or a captured
artifact. No hypotheses before this exists.

## 2. Reproduce and minimise

Confirm the check shows the symptom the owner described, not a nearby one. Then cut inputs, files and
steps one at a time until every remaining part is needed for red.

## 3. Hypothesise

Write 3–5 ranked, falsifiable hypotheses: "If X is the cause, changing Y makes the bug disappear."
Show them to the owner before testing (they often know which one is already ruled out), but don't
block on an answer.

## 4. Instrument

One probe per prediction, one variable at a time. Prefer a debugger or a test assertion; otherwise
targeted logs tagged `[DEBUG-xxxx]` so one grep removes them. For performance: measure a baseline
against the budgets in `CLAUDE.md` first, then bisect.

## 5. Fix with a regression test

Turn the minimised repro into a failing test, watch it fail, fix, watch it pass, re-run the original
check from phase 1. If no seam lets a test reproduce the real pattern, that is a finding: note it in
the PR (candidate for a `type:tech-debt` issue).

## 6. Clean up

- [ ] The phase 1 check is green; the regression test passes (or the missing seam is noted).
- [ ] No `[DEBUG-` left (`git grep "\[DEBUG-"`), throwaway harnesses deleted.
- [ ] The confirmed hypothesis is stated in the commit message, so the next person learns it.
- [ ] Test plan history row with the real environment (DoD).
