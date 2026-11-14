# Architecture: keyboards and region formats in `Arcadia.App`

The rest of the App: [app.md](app.md); the product rules: `docs/product/on-screen-keyboard.md`, `docs/product/localization.md`.

- `Keyboard/` (#110): `OnScreenKeyboardViewModel` (view `Views/OnScreenKeyboardView`), `BoardKeyViewModel`,
  `KeyboardLevels` (shift/AltGr), `LayoutMenuViewModel`, `IKeyboardTarget` (what it types into; its user greys out or
  leaves out keys, `BoardKeyState`), `WindowInFrontTarget` (the mouse mode's), `SharedKeyboard` (taken in turn, #477).
- `Input/KeyPosition`, `KeyLabels`, `WindowsKeyboardLayout` (#588): letter keys count by place; hints and `KeyTip` tooltips show the active layout's letter.
- `Services/Formatting`: numbers and dates by region; `DatePattern` cuts a region's pattern field by field,
  `RegionDatePatterns` checks the cuts once (#592).
