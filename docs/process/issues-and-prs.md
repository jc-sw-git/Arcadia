# Linking PRs to issues and closing issues

Owner, 2026-10-28: every PR shows up under its issue's **Development** box, and an issue is closed as soon as all
PRs it needs are merged, also when they merge into a feature branch (e.g. `feat/replace-sisr`). Closing that way
needs no extra OK and no comment.

## What links a PR (checked 2026-10-28)

GitHub links a PR to an issue in two ways only:

- **A closing keyword (`Closes #N`) in the PR body, but only for PRs into `main`** (the default branch). The link
  appears at once and the issue closes on merge. Adding the keyword to an already merged PR's
  body still links it (done for #246 → #118, an issue that was already closed; GitHub closes issues only at
  the merge itself).
- **By hand in the issue's sidebar** (Development → gear → pick the PR). This works for any PR, but there is no
  REST endpoint for it, and cloud threads can't use GraphQL, so only the owner can do it.

A keyword in a PR into a feature branch links nothing (#292 had `Closes #156`; #156 showed no PR).

## Rules

- **Only or last PR of an issue, into `main`:** `Closes #N` in the body, one line per issue.
- **One part of several, into `main`:** `Part of #N` while open (a keyword would close the issue on merge).
  After the merge, the thread adds `Closes #N` to the merged PR's body so it is linked too.
- **Into a feature branch:** `Part of #N` in the body. The thread tells the owner in its PR message which issue
  to link the PR to by hand. When the last PR of an issue is merged into the branch, the thread closes the issue
  (completed, no comment). The integration PR from the feature branch into `main` lists `Closes #N` for every
  issue done in the branch, so they are all linked to it.
- **After every merge:** the thread checks whether all PRs of its issue are merged, and if so the issue is closed
  (by the keyword or by hand). An issue's manual cases that wait for a later test (e.g. the owner's full test of a
  feature branch) don't keep it open; they are listed in the iteration's acceptance review.
