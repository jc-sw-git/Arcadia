# #556 Spanish UI texts

Part 4 of #14; the language row and its restart are #554's cases, which hold for Spanish unchanged. The cases were
derived from the issue's scope before the texts were written; the quality engineer checks them again in the review.

**Status:** Automated: T556-1 to T556-4 · QE: T556-5, T556-6 (passed) · Open for the owner: T556-7

Every case runs with the gamepad alone unless it says otherwise; keys follow `docs/product/controls.md`.

**Language**

- [x] **T556-1** "Systemsprache" on a Spanish Windows (es-ES, es-MX): Arcadia is in Spanish; "Español" chosen on a German
  one: Spanish after "Sprache anwenden". The row "Sprache" shows "Systemsprache · English · Français · Español ·
  Deutsch". · E2E: yes
- [x] **T556-2** In Spanish, the info of "Idioma" names an unsupported system language in lowercase, as Spanish writes
  it ("El idioma del sistema es italiano …"); German and English keep the capital. · E2E: yes

**Texts**

- [x] **T556-3** Every key of every `*Texts.resx` exists in `*Texts.es.resx` with the German placeholders, and the
  Spanish satellite assemblies are found for es-ES and es-MX. · E2E: yes
- [x] **T556-4** `ButtonGlyphTextTests` checks the Spanish files (LB … D-Pad fail written out, “a” and “y” stay
  words); `check-ui-texts.sh` fails on the Spanish glossary's forbidden spellings (straight or English quotes outside
  «…», “?” or “!” without ¿ ¡, *emular*, *p. ej.*, *reiniciar Windows*, *usted*, …) and passes the allowed ones. · E2E: yes
- [x] **T556-5** Every page, dialog and settings category of the guard page list (`Guards/GuardPages.cs`) in Spanish,
  plus the first start, the game menu, toasts and the status modal: nothing cut off that isn't cut in German and
  English too, no German or English left. · E2E: no (window: screenshots of the drawn layout)
- [x] **T556-6** Independent review of the Spanish texts against `docs/product/ui-texts-spanish.md`: wording, tone,
  glossary, ¿ ¡ and «…». · E2E: no (wording)

**On the owner's PC**

- [ ] **T556-7** **Owner:** "Español" chosen and applied: Arcadia restarts in Spanish on "Idioma"; library, detail
  page and settings read naturally to you at a glance (wording itself is T556-6). · E2E: no (real restart on Windows)

## Runs

| Case | Test |
|---|---|
| T556-1 | `UiLanguagesTests` (Core), `UiLanguageSettingsTests.System_language_is_used_…` and the row tests (App) |
| T556-2 | `UiLanguageSettingsTests.The_system_language_is_named_as_the_UI_language_writes_it` |
| T556-3 | `TranslationsTests` (Core), `EveryUiTextTests` (App, es-ES, es-MX) |
| T556-4 | `ButtonGlyphTextTests` (Core), `tools/ci/tests/run.sh` (Spanish fixtures) |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T556-1 to T556-4 (automated) | 2026-11-14 | Implementer | Debug, Linux cloud container, headless Avalonia | green | – |
| T556-5 (library, game menu, view dialog, filter, search, detail page, first start both steps, all settings categories) | 2026-11-14 | Implementer | Debug, Linux cloud container, Xvfb, `--demo`, Spanish | quick selection value “Por defecto (permitido)” cut on the detail page; the row labels there (“Modo del Steam Con…”) are cut in English and German too | “Default” is “Estándar” in Spanish; the label trimming is the quick selection's design |
| T556-5 (all 117 guard page states, headless es-ES, plus real app under Xvfb `--demo` Spanish incl. the Metadatos tab) | 2026-11-14 | QE | Debug, Linux cloud container | findings 1–6 | fixed in this PR |
| T556-6 (all 1,713 texts against `ui-texts-spanish.md`) | 2026-11-14 | QE | resx | findings 1–5, optional 7–9 | fixed in this PR |
| Re-check of findings 1–9 | 2026-11-14 | QE | Debug, Linux cloud container, Xvfb `--demo` Spanish, c3fba60 | all fixed | – |
