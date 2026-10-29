# Linking PRs to issues and closing issues

Every PR shows up under its issue's **Development** box, and an issue is closed as soon as all PRs it needs are
merged, also when they merge into a feature branch. Closing that way needs no extra OK and no comment.

GitHub links a PR to an issue only through a closing keyword (`Closes #N`) in the body of a PR into `main`, or by
hand in the issue's sidebar (Development → gear), which only the owner can do: there is no REST endpoint, and cloud
threads can't use GraphQL. A keyword in a PR into a feature branch links nothing.

- **Only or last PR of an issue, into `main`:** `Closes #N` in the body, one line per issue.
- **One part of several, into `main`:** `Part of #N` while open (a keyword would close the issue on merge). After
  the merge, the thread adds `Closes #N` to the merged PR's body so it is linked too.
- **Into a feature branch:** `Part of #N` in the body, and the PR message tells the owner which issue to link it to
  by hand. When the last PR of an issue is merged into the branch, the thread closes the issue (completed, no
  comment). The integration PR from the feature branch into `main` lists `Closes #N` for every issue done in the
  branch, so they are all linked to it.
- **After every merge:** the thread checks whether all PRs of its issue are merged and closes it if so. Manual cases
  that wait for a later test (e.g. the owner's full test of a feature branch) don't keep it open; they are listed in
  the iteration's acceptance review.
