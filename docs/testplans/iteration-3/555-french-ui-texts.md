# #555 French UI texts

Part 3 of #14; the language scenarios of #14 hold for French as #554 tests them for English. The cases were derived
from the issue before any code; the quality engineer checks them again in the review.

**Status:** Automated: T555-1 to T555-5 · QE: T555-6, T555-7 · Open for the owner: T555-8

Every case runs with the gamepad alone unless it says otherwise; keys follow `docs/product/controls.md` (WASD,
Space = A, Esc = B). "System language" is the Windows display language, faked in the tests.

- [x] **T555-1** Settings, "Lokalisierung", row "Sprache": the values are "Systemsprache · English · Français ·
  Deutsch" in this order ("Español" follows with #556), "Français" in French whatever the UI language; ◀ ▶ wrap at both
  ends; an unknown value in `settings.json` still counts as "Systemsprache". · E2E: yes
- [x] **T555-2** System language French (fr-FR, fr-CA, fr-BE, fr-CH), "Systemsprache": Arcadia is in French, every
  text of every resource file comes from the French translation; the info of "Sprache" names the system language.
  · E2E: yes
- [x] **T555-3** "Français" chosen (German in use): "Sprache anwenden" is active; A restarts into French on the row
  "Sprache". On a French system, "Systemsprache" and "Français" count as the same language: "Sprache anwenden" stays
  greyed. · E2E: yes
- [x] **T555-4** Every German text has its French one with the same placeholders; a text French lacks would show in
  English; the size unit is "Go", the number keeps the region's format. · E2E: yes
- [x] **T555-5** The French texts keep the glossary and typography of `docs/product/ui-texts-french.md`: « » with a
  no-break space, a no-break space before : ; ! ?, ’, no "émuler", "p. ex.", "redémarrer Windows"; controller
  buttons as glyphs (CI check `UI texts`, `ButtonGlyphTextTests`). · E2E: no (CI check of the resource files)
- [x] **T555-6** Every page in French at 1920 × 1080 (library, detail page, menus, search, filter, settings categories,
  first start, dialogs): no text cut off or running into its neighbour; longer French texts wrap or fit. · E2E: no
  (window)
- [x] **T555-7** Independent review of the French texts by the quality engineer: meaning matches the German, the
  glossary terms are used consistently, labels quoted in a text match the label they name. · E2E: no (language review)
- [ ] **T555-8** **Owner:** Windows display language French (or "Français" applied): click through library, detail page
  and settings once; anything that reads oddly or is still German or English goes into the PR. · E2E: no (real PC)

Automated tests:

| Test cases | Tests |
|---|---|
| T555-1 | `UiLanguageSettingsTests` (row values, ◀ ▶), `UiLanguagesTests` (Core), `LocalizationSettingsTests` (Core) |
| T555-2 | `FrenchUiTests`, `EveryUiTextTests` |
| T555-3 | `FrenchUiTests`, `UiLanguageSettingsTests` |
| T555-4 | `TranslationsTests` (Core), `UiTextTests` (Core, the English fallback), `FrenchUiTests` |
| T555-5 | `tools/ci/check-ui-texts.sh` with its fixtures in `tools/ci/tests/ui-texts/`, `ButtonGlyphTextTests` (Core) |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T555-1 to T555-5 | 2026-11-14 | Claude | branch feat/french-ui-texts-555, Linux container, `dotnet test` | ✅ | – |
| T555-6 | 2026-11-14 | Claude | branch feat/french-ui-texts-555, Debug build, Xvfb 1920 × 1080, `--demo`, "Français" set | ✅ nothing cut off; "Lanceur" → "Lanceurs", "Trié par" → "Tri :", size unit "Go" | fixed in this PR |
| T555-6 | 2026-11-14 | Claude (QE) | branch feat/french-ui-texts-555 @ 3cb7fc4, Release build, Xvfb 1920 × 1080, `--demo` and `--demo --first-run`, "Français" set; library, game menu, view, filter, detail page (4 areas), search (empty, no hit, hits), settings (5 categories), tags, new tag with on-screen keyboard, change artwork, uninstall question, first start (2 steps) | ✅ no French-only cut-off; two cut-offs seen are the same in German | – |
| T555-7 | 2026-11-14 | Claude (QE) | all `*.fr.resx` side by side with German, glossary and quoted-label check by script | ❌ 5 findings (agreement, one meaning, key badges, backspace key) | fixed in this PR, see Review |
| T555-7 | 2026-11-14 | Claude | the 5 findings fixed (texts, `KeyEnter`/`KeyEscape` badges), Xvfb screenshots of hint bar, settings button and "Durée de maintien" in French; language, keyboard and mouse mode tests green | ✅ | – |
