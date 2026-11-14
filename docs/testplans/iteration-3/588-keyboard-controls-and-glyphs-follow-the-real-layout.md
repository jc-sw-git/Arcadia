# #588 Keyboard controls and glyphs follow the user's real keyboard layout (AZERTY, QWERTZ, QWERTY)

**Status:** Automated: T588-1 to T588-6 · QE: T588-1, T588-8 · Open for the owner: T588-7

- [x] **T588-1** On AZERTY the keys at the places of W A S D (printed Z Q S D) move the focus, A/E (places Q/E) turn
  pages, ⇧ A/⇧ E turn tabs, the key printed W does nothing; the hints show "ZQSD", "A", "E", "⇧ A". · E2E: yes
- [x] **T588-2** QWERTZ and QWERTY keep today's keys and hints. · E2E: yes
- [x] **T588-3** A layout change refreshes every hint once; the same layout read again changes nothing. · E2E: yes
- [x] **T588-4** Key names (Leertaste, Esc, ⌫) and controller buttons in tooltips (L3, RT, B) stay as they are;
  tooltip letters in brackets follow the layout ("Vorherige Seite (A, Bild ↑ …)" on AZERTY). · E2E: yes
- [x] **T588-5** A key that prints no letter shows its US letter and still acts by its place. · E2E: yes
- [x] **T588-6** The digit row picks launcher tab 1–10 by place, also where it prints & é " … unshifted. · E2E: yes
- [ ] **T588-7** **Owner:** Windows, keyboard hints showing (press a key first): switch to French (AZERTY) with
  Win + Space. The footer and header chips show ZQSD, A/E, ⇧ A/⇧ E at once, without a key press; Z Q S D move, A/E turn
  pages; the tooltip of the search chip reads "Suchen (F)", of the page arrows "(A, …)"; in the search Z types "z".
  Switch back: WASD again. · E2E: no (real Windows input layout)
- [x] **T588-8** Linux/X11 with a French layout: the keys act by place, typing in the search types the layout's
  letters. · E2E: no (window, real X11 layout)

Automated tests:

| Test cases | Tests |
|---|---|
| T588-1 to T588-6 | `RealKeyboardLayoutTests`; the key named for each button still does what the button does: `KeyNamesTests` |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T588-1 to T588-6 | 2026-11-14 | Claude | Debug, Linux cloud container, `dotnet test` | ✅ | – |
| T588-1, T588-8 | 2026-11-14 | Claude | Debug, Linux/Xvfb, `--demo`, `setxkbmap fr`, xdotool keys | ✅ log: z → W → Up, q → A → Left, a → Q → PreviousPage, w → nothing; search typed "zqsd"; hints stay US letters on Linux by design | – |
