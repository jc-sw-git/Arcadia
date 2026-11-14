# #574 Raise 13 px running texts to the design rule's minimum sizes

**Status:** Automated: T574-2 · QE: T574-1 · Open for the owner: –

- [ ] **T574-1** In the settings' info panels the footnote (e.g. "Controller", a launcher's controller mode) and the mode
  cards' text and default note read at 16 px; the on-screen keyboard's layout menu (R3) shows each layout's kind at 15 px;
  only captions in capitals, badges, chips and glyph letters stay below 14 px; the panels and the menu still fit at
  1920 × 1080. · E2E: no (window)
- [x] **T574-2** The larger texts cut nothing of Arcadia's own in any language (T587-1). · E2E: yes

Automated tests:

| Test cases | Tests |
|---|---|
| T574-2 | `Guards.NoLabelCutTests.No_text_of_Arcadias_own_is_cut_off_on_any_page_dialog_or_settings_category` (window guard, runs on `main`) |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T574-2 | 2026-11-14 | Claude | Debug, Linux cloud container, headless window | ✅ | – |
