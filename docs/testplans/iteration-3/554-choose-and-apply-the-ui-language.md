# #554 Choose and apply the UI language, with English

Part 2 of #14; scenarios 1 to 9 and the failure cases there. The cases were derived from the specification before any
code; the quality engineer checks them again in the review.

**Status:** Automated: T554-1 to T554-18 · QE: – · Open for the owner: T554-20, T554-21, T554-22, T554-23

Every case runs with the gamepad alone unless it says otherwise; keys follow `docs/product/controls.md` (WASD,
Space = A, Esc = B). "System language" is the Windows display language, faked in the tests; "region" is the regional
format, set apart from it. Languages offered in the row: those whose texts are complete; Français and Español join with
their texts in #555/#556.

**The row "Sprache"**

- [ ] **T554-1** Settings, "Lokalisierung", group SPRACHE: the row "Sprache" shows one box per value in this order,
  "Systemsprache · English · Deutsch" ("Français", "Español" follow in #555/#556), each language in its own language and
  only "Systemsprache" in the current one ("System language" in English); the value in use is highlighted. Below it,
  indented one step, "Sprache anwenden". ◀ ▶ wrap at both ends; B goes back to where the settings were opened. · E2E: yes
- [ ] **T554-2** "Systemsprache", system language German (de-DE, de-AT) or English (en-US, en-GB): Arcadia is in that
  language; the info panel of "Sprache" names the system language in use, in the UI language. · E2E: yes
- [ ] **T554-3** "Systemsprache", system language Italian (it-IT): Arcadia is in English; the same info message says
  the system language is Italian, isn't supported and English is used instead, in English. An unsupported language
  never shows German (today's neutral texts). · E2E: yes
- [ ] **T554-4** The system language decides, not the region: display language English with region German gives an
  English UI with "1.234" and German dates; display language German with region US gives a German UI with "1,234".
  · E2E: yes
- [ ] **T554-5** ◀ ▶ through all values of "Sprache": nothing restarts, the UI stays in the language in use, the
  choice is saved at once (a restart of the driver reads it back). · E2E: yes
- [ ] **T554-6** "Sprache anwenden" is active only while the chosen language differs from the one in use, compared by
  the language that results: German in use via "Systemsprache" (de-DE), choosing "Deutsch" keeps it greyed, "English"
  makes it active and its subtitle and info say Arcadia restarts, back to "Systemsprache" greys it again. With
  "Systemsprache" on it-IT (English in use), "English" keeps it greyed. A greyed row takes the focus, A does nothing.
  · E2E: yes
- [ ] **T554-7** A on "Sprache anwenden" (German in use, "English" chosen): the language is saved before the restart;
  Arcadia restarts with the same start arguments (e.g. `--demo`) and opens the settings, category "Lokalisierung",
  focus on "Sprache", in English, in Big Picture; started in desktop mode, it comes back in desktop mode. B then goes
  to the library. · E2E: yes
- [ ] **T554-8** A game runs, a game is starting, the driver installation runs or the Arcadia update runs: "Sprache
  anwenden" is greyed out, its subtitle and the blue note at the top of its info say why (for a game: Arcadia can
  restart only after the game ends). When it ends the row becomes active again on the open page, without leaving it.
  · E2E: yes
- [ ] **T554-9** Artworks or metadata reload in the background: "Sprache anwenden" stays active and its info says the
  reload stops; A restarts, and the reload doesn't go on after the restart. · E2E: yes
- [ ] **T554-10** The info panel of "Sprache" says that metadata stay in the language they were loaded in until
  "Alle Metadaten neu laden" (category "Datenquellen"), where the data exist in that language. · E2E: yes
- [ ] **T554-11** First start with fresh settings: no language step; de-DE gives German, en-US English, it-IT English
  on every first-start page. · E2E: yes
- [ ] **T554-12** Mouse: a click on a value box or an arrow of "Sprache" chooses as ◀ ▶ do, a click on an active
  "Sprache anwenden" restarts, a click on a greyed one does nothing. · E2E: no (window: the click's target is the
  drawn layout)

**Failure cases**

- [ ] **T554-13** `settings.json` with an unknown language ("xx"), a number, `null` or a broken file: Arcadia starts in
  the system language, the row shows "Systemsprache", `arcadia.log` has one line naming the setting (not the whole
  file), no toast. · E2E: yes
- [ ] **T554-14** Arcadia ends right after A on "Sprache anwenden", before the restart (crash): the next start is in
  the new language, on the library. · E2E: yes
- [ ] **T554-15** The restart fails (Arcadia can't end): an error toast, `arcadia.log` has the stack trace, the old
  language stays in this session, "Sprache anwenden" stays active for a retry; the saved choice holds, so the next
  start uses it. · E2E: yes
- [ ] **T554-16** A text missing in English (or a later language) shows the English text, never the German one and
  never the key; one log line per key. · E2E: yes

**Texts**

- [ ] **T554-17** Every language has every key of every `*Texts.resx` with the same placeholders as German: a test
  goes red on a missing key and on a renamed or missing placeholder. · E2E: yes
- [ ] **T554-18** `ButtonGlyphTextTests` per language: LB, RB, LT, RT, L3, R3, LS, RS and D-Pad written out fail in
  every language, A, B, X, Y only in German; each entry of the exception list has a reason. `check-ui-texts.sh` fails
  on a forbidden English spelling from the English glossary. · E2E: yes
- [ ] **T554-19** Every page, dialog and settings category of the guard page list (`Guards/GuardPages.cs`) in
  English, plus the first start, toasts and the status modal: nothing cut off, no German left. · E2E: no (window:
  screenshots of the drawn layout)
- [ ] **T554-20** **Owner:** Read the English texts (the screenshots of T554-19 and the list in the PR): wording,
  tone and the glossary fit. · E2E: no (wording)

**On the owner's PC**

- [ ] **T554-21** **Owner:** Windows in German, "Systemsprache": the info of "Sprache" names German. Set the Windows
  display language to English (sign out and in), start Arcadia: it is English and names English; set it back.
  · E2E: no (real Windows language settings)
- [ ] **T554-22** **Owner:** Big Picture, gamepad only: choose "English", A on "Sprache anwenden". Arcadia closes
  cleanly and comes back in English on "Sprache" within a few seconds, in front, with no second Arcadia and no
  "already running" message; the Steam Controller works in Arcadia and in a game started afterwards. Back to
  "Deutsch" the same way. · E2E: no (real process restart, real controller)
- [ ] **T554-23** **Owner:** Desktop mode with the mouse: the same switch with clicks only; Arcadia comes back in
  desktop mode, same window size and place. · E2E: no (real process restart, real window)
- [ ] **T554-24** On the installed release candidate, after an update to it: "Sprache anwenden" restarts the
  installed version just updated to, not the old one. · E2E: no (installed Arcadia and its update) · RC

Automated tests:

| Test cases | Tests |
|---|---|
| T554-1, T554-5, T554-6, T554-12 | `UiLanguageSettingsTests` (App): rows, order, wrapping, saving, when "Sprache anwenden" is active, clicks |
| T554-2, T554-3, T554-11 | `UiLanguageSettingsTests.System_language_is_used_…`, `The_info_names_…`; `UiLanguagesTests` (Core) |
| T554-4 | `LocalizationSettingsTests.The_UI_language_comes_from_the_display_language_…`, `With_an_English_US_region_…` (App) |
| T554-7, T554-14, T554-15 | `UiLanguageSettingsTests.Apply_restarts_…`: saved before the restart, the new start on "Sprache", a failed restart |
| T554-8, T554-9 | `UiLanguageSettingsTests.Apply_waits_for_a_running_game_…`, `Apply_waits_for_the_driver_installation_…` |
| T554-10 | `UiLanguageSettingsTests.The_info_names_the_system_language_…` |
| T554-13 | `LocalizationSettingsTests.An_unknown_language_is_the_system_language_…` (Core) |
| T554-16, T554-17 | `UiTextTests`, `TranslationsTests` (Core), `EveryUiTextTests` (App, de-DE, de-AT, en-US, en-GB) |
| T554-18 | `ButtonGlyphTextTests` (Core), `tools/ci/check-ui-texts.sh` |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T554-1 to T554-18 (automated) | 2026-11-13 | Implementer | Debug, Linux cloud container, headless Avalonia | green | – |
| Full test run | 2026-11-13 | Implementer | Debug, Linux cloud container | `WindowLifetimeTests` red: the new subscriptions for "Sprache anwenden" were never removed | unsubscribed in `Dispose`; green |
| T554-7 (real restart) | 2026-11-13 | Implementer | Debug, Linux cloud container, Xvfb, `--demo --fullscreen` | restarted in English on "Sprache", `--fullscreen` carried; focus first jumped to the row under the resting pointer | first pointer event only sets where the pointer rests; focus stays on "Sprache" |
| T554-19 (English screenshots of library, detail, search, first start, settings) | 2026-11-13 | Implementer | Debug, Linux cloud container, `--demo` | German literals left in key hints and views | moved into the resources, guard test extended (`UiTextsStayInResourcesTests`) |
