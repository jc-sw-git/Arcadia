# Contributing to Arcadia

Thanks for helping. Arcadia is a Windows game launcher that unites the other launchers, built
with .NET 10 and Avalonia. The working rules live in [`CLAUDE.md`](CLAUDE.md) (written for AI
agents, but it is the one place for humans too); this page is the short version.

## Before you start

- Look for a matching issue first. Larger changes start with a short discussion in the issue,
  so the behaviour is agreed before the code is written.
- UI texts are German for now; code, comments and commit messages are English.

## Build and test

```bash
dotnet build Arcadia.slnx
dotnet test Arcadia.slnx
dotnet run --project src/Arcadia.App -- --demo   # demo libraries for all launchers
```

The build treats warnings as errors.

## Pull requests

- Branch names: `{prefix}/{issue-title}-{issue-number}` in kebab case, prefix one of `feat`,
  `fix`, `ci`, `test`, `doc`, `td` (see `CLAUDE.md` → Git).
- A PR is done when it meets the Definition of Done in `CLAUDE.md`: warning-free build, green
  tests (new logic comes with tests), every flow works with a gamepad alone, colours only in
  `Theme/Theme.axaml`, launcher code only in its launcher folder.
- A PR that closes an issue adds that issue's manual tests to `docs/testplans/` (the `Testplan`
  check fails without it; format in `docs/testplans/README.md`).
- Keep PR descriptions short: what was wrong before, what changes, what you checked.
- No personal data in code, tests or logs: use made-up account ids, names and paths in test data.

## License

Arcadia is GPL-3.0-only. By contributing you agree that your contribution is licensed under it.
