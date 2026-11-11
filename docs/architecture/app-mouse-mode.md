# Architecture: the mouse mode in `Arcadia.App`

The rest of the App: [app.md](app.md); the mouse mode's input in `Arcadia.Input`: [input.md](input.md).

`Services/MouseModeHost` (wiring, overlay, automation), `Services/ForegroundGame` (also what is in front after a game
ended, #94), `Services/GameWindow` ("Zurück ins Spiel"), `Services/ForegroundWindow` (its bounds and caret),
`Services/ForegroundGuard` (Arcadia's window in front of or behind others), `Services/ForegroundOwner` (Steam's window,
by the programs of the launcher that owns Steam Input, or a library game in front, asked on every poll),
`Services/SystemProcessTable` (the PC's processes for `ProcessGames`),
`Views/MouseModeKeyboardWindow` with `Views/MouseModeKeyboardLegend` (the keyboard outside Arcadia, #110),
`Views/MouseModeOverlay` (own non-activating window, clicks pass outside its region) with `Controls/ScreenEdgeFill`.
