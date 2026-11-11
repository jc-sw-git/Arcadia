# #480 Move UI texts into resource files

**Status:** Automated: T480-1 to T480-11, T480-15, T480-16 · QE: T480-12, T480-13, T480-14, T480-17 · Open for the owner: none (nothing visible changes)

**Fallback (fill helper)**
- [x] **T480-1** A text with `{game}` and `{launcher}`, filled with both values: the result is the text with both values in place, nothing logged · E2E: no (unit test of the fill helper)
- [x] **T480-2** The same text filled without a value for `{game}`: `{game}` stays visible as written, the other placeholder is filled, no exception; `arcadia.log` gets one line with the text's key and `{game}` · E2E: no (unit test)
- [x] **T480-3** A key that has no text (e.g. a translation that lacks it): the key is shown, no exception; `arcadia.log` gets one line with the key · E2E: no (unit test)
- [x] **T480-4** The same broken text filled 100 times, also from two threads at once: exactly one log line for it; a second broken key gets its own one line; no toast is raised for either · E2E: no (unit test)
- [x] **T480-5** A value that itself contains braces or a placeholder name (a game called `{launcher} Edition` or `Foo {count}`): it is shown literally, not filled again, and nothing is logged · E2E: no (unit test)
- [x] **T480-6** A value passed that the text doesn't use: ignored, nothing logged, no exception (a translation may leave a placeholder out) · E2E: no (unit test)
- [x] **T480-7** The log line of T480-2 and T480-3 holds the key and the placeholder name only, none of the filled values (game names, paths, account names) · E2E: no (unit test)
- [x] **T480-8** In the running app, a page whose text has a missing value (resource replaced by a broken one in the test): the page shows the placeholder name, no toast appears, the next button press is handled as usual · E2E: yes

**Same texts as before**
- [x] **T480-9** Every plural and conditional text (e.g. "1 Spiel · {n} Spiele"): for 0, 1, 2 and 1,000 the result is the same string as on `main` · E2E: no (unit tests per text)
- [x] **T480-10** Numbers inside texts (counts, versions, sizes) come out as on `main` with the current culture `de-DE` and `en-US` · E2E: no (unit test)
- [x] **T480-11** With the UI culture `en-US`, `fr-FR` and invariant, every text is the German one (no culture picks another or an empty resource) · E2E: no (unit test)
- [x] **T480-12** Byte for byte: every German literal the diff removes from `src/` (views, view models, services) has a resource value that is exactly equal, and no resource value is new; placeholders differ only by name (`{0}` → `{game}`) · E2E: no (one-time mechanical check of the diff, QE review)
- [x] **T480-13** Screenshots at 1920×1080 with `--demo`, `main` against the PR: library, detail page, search with the on-screen keyboard, artwork modal, settings (each category), launcher page, a confirmation dialog, a toast, the button hints: same texts (pixel diff or text by text) · E2E: no (real rendering, Linux, `arcadia-verify`)
- [x] **T480-14** Existing tests green with their assertions unchanged; acceptance tests that assert a text read it from the resources, and that is the only change to them · E2E: no (review of the test diff)

**Guard and structure**
- [x] **T480-15** The architecture test goes red on a new German UI literal in an `.axaml` (`Text`, `Content`, `ToolTip.Tip`, …), in a view model, and in a service in Core, ControllerSupport and a launcher folder, also without umlauts (e.g. "Abbrechen") and inside string interpolation; it stays green for log calls, tests, resource keys, file names, URLs and brand names · E2E: no (architecture test, each red run named in the PR)
- [x] **T480-16** Steam's and Epic's texts are in their launcher folder's resource file, none in the App or Core resources; a launcher reaches the fill helper through `Arcadia.Launchers.Abstractions` only (`ArchitectureTests` green) · E2E: no (architecture test)
- [x] **T480-17** A fresh clone builds with plain `dotnet build Arcadia.slnx` on Linux and Windows (no Visual Studio code generator), CI green on all platforms · E2E: no (CI)

Tests: T480-1 to -7, -10, -11 `UiTextTests` (Core); T480-8 `BrokenTextTests`; T480-9 `PluralTextsTests`; T480-15
`UiTextsStayInResourcesTests` (Core); T480-16 `ArchitectureTests`.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T480-12 | 2026-11-11 | Claude | PR branch, Linux cloud container, Roslyn listing of every literal the diff removes against every resource value | ✅ all equal after `{…}` → `{}`; only differences are the conditional texts split into two whole sentences (`GameCountOne` / `GameCountMany` …), each giving the old string | – |
| T480-8 (before the fix) | 2026-11-11 | Claude | PR branch with `UiText.Fill` throwing on a missing value, Debug, Linux cloud container | ❌ the test fails with the exception | fixed |
| T480-1 to T480-11, T480-15 | 2026-11-11 | Claude | PR branch, Release, Linux cloud container (`tools/ci/run-tests.sh 4`) | ✅ Core 2,541, Pan 438, App 637 methods green; the architecture test is red on each sample of T480-15 (code, "Abbrechen", interpolation, axaml attribute, setter, element text) and its scan covers every project in `src/` but Pan | – |
| T480-13 | 2026-11-11 | Claude | `main` 2c419a9 against the PR branch, Debug, `--demo`, Xvfb 1920×1080, Linux cloud container | ✅ pixel diff of library, detail page, game menu, uninstall confirmation, search empty and with no hits, settings Launcher / Controller / Über Arcadia, launcher page, "Alle neu einlesen" toast, Xbox tab: only the clock and the relative time differ | – |
| T480-14 | 2026-11-11 | Claude | PR branch, review of the test diff | ✅ only literals inside `Assert` lines that equal a resource value now read `AppTexts.…` / `ControllerSupportTexts.…` (6 property patterns became `&&` comparisons, as a resource isn't a constant); all green | – |
| T480-16, T480-17 | 2026-11-11 | Claude | PR branch, plain `dotnet build Arcadia.slnx`, Linux cloud container; Windows in CI | ✅ `SteamTexts.resx` / `EpicTexts.resx` / `DemoTexts.resx` in their launcher folders, `ArchitectureTests` green | – |
| T480-12 (QE) | 2026-11-11 | Claude (QE) | PR cad2814, script over the diff: every removed German literal in `src/` against every `*Texts.resx` value after `{…}` → `{}` | ✅ 1,273 literals: all equal, apart from the split conditional texts (checked by hand: search hits, page sizes, firmware tool, "wird bereits …", filter hides all, title list) and texts that stay in code as log lines; no resource value without a removed literal | – |
| T480-1 to T480-11, T480-14, T480-15 (QE) | 2026-11-11 | Claude (QE) | PR cad2814, Release, Linux cloud container (`tools/ci/run-tests.sh 4`) | ✅ Core 2,561, Pan 438, App 652 methods green; test diff: only assertion literals now read from the resources | – |
| T480-13 (QE) | 2026-11-11 | Claude (QE) | `main` f41fb1a against PR cad2814, Debug, `--demo`, Xvfb 1920×1080 | ✅ pixel diff of library, second tab, detail page, game menu, uninstall confirmation, view dialog, search with no hits and its button hints: only the clock and typing timing differ; start with 1,031 games 2.7–2.8 s on both, no `UI-Text` line in `arcadia.log` | – |
