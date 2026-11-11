# Product decisions: Controllers in games

What a game sees of the controllers, the per-game mode "Arcadia" / "Steam-Eingabe" and Pan's notices. Buttons in
Arcadia itself: [controls.md](controls.md).

## Every controller (#238)

- While "Controller-Wächter (Pan)" is on, Pan hides every controller Arcadia reads from games and Steam, and each one
  reaches games as an emulated pad of Arcadia's, of its own kind where Arcadia has it (DualShock 4, DualSense,
  DualSense Edge, Switch 2 Pro; every other controller an Xbox 360 pad), or the layout the player chose per model in
  "Controller zuordnen" (`settings-controller.md`); a model set to "Nicht übernehmen" reaches games directly, as with Pan off,
  one per controller, in the order they connected. The same rules as for the Steam Controller
  below: Back + Start never reaches the game, ⧉ and ≡ alone a little later (held, as held), rest while Arcadia is in
  front or the mouse mode is on. Unlike the Steam Controller's pad, they also pass while Steam itself is in front: Steam sees
  only them. With "Steams Desktop-Steuerung zulassen" on they pass in front of Arcadia and in the mouse mode too, ⧉ + ≡
  included, as the copy does (`mouse-mode.md`). Guide/PS/Home passes on (Steam's overlay, the Game Bar); a game's rumble reaches the real controller.
- Never both: a controller Pan can't hide reaches games directly and isn't emulated (the settings list it, see
  `settings-controller.md`). An XInput pad (an Xbox pad, 8BitDo in XInput mode) that Steam or a game
  already opened is reconnected by Pan when it is hidden (gone for a few seconds, then emulated); by Bluetooth that
  can't be done, so it is emulated only once the player reconnects it.

## The Steam Controller (#213, #242, #178)

- In games the Steam Controller is an Xbox 360 pad from Arcadia; the game never sees Arcadia's combo Back + Start.
  ⧉ + ≡ counts for switching the mouse mode on only when the second button comes within 100 ms of the first,
  everywhere in Arcadia (ending the mode still takes them in any order). So while the mouse mode is not "Aus", ⧉ and ≡
  reach the game a little later: let go within 100 ms, as a 50 ms tap; held alone longer, as held from then on, ⧉ also
  while it brings Arcadia back. The pad passes input only while a game on "Arcadia" is in front (#357); everywhere
  else (Arcadia, the mouse mode, Steam, a browser, the desktop, no game running) it reports rest. Own hold times and a
  pause key per game: separate issue.
- Per game the Steam Controller mode has three values: "Standard (Arcadia)" / "Standard (Steam-Eingabe)" (follows the
  launcher's default, set on the launcher's page, `docs/product/launchers.md`; factory: Steam games Steam-Eingabe,
  all others Arcadia), "Arcadia" (Arcadia's pad) and "Steam-Eingabe" (no pad of Arcadia's, the game's own Steam
  configuration). [A] or a click cycles Standard → Arcadia → Steam-Eingabe → Standard; a change applies at the game's
  next start. It shows in preview, detail page and context menu whenever a Steam Controller is connected and
  "Controller-Wächter (Pan)" is on; off, or off Windows, it is gone. While Pan's emulation is on but doesn't run (a
  part missing, hiding lost, stopped) it is greyed out at "Steam-Eingabe" with a triangle, yet focusable: [A] (or a
  click) opens the controller settings on the row that says why, [B] goes back (designs T242Library/T242Zustaende).
- While the copy passes input: Steam gets the Steam Controller for a game on "Steam-Eingabe" and while Steam itself
  is in front, also during a game on "Arcadia" (the pad rests then, #357); never while Arcadia is in front or the
  mouse mode is on. Otherwise Steam
  reads a controller at rest except the Steam button and "…" (`SteamControllerCopyRules`).
- Pan's firmware notice (design T242Firmware without its wait, #295): "Steam will die Firmware aktualisieren" comes to
  the front over everything when Steam tries an update through the copy and no library game is in front (then only a
  notice over the game: "Pan: Steam kann die Firmware nicht aktualisieren, solange Arcadia den Steam Controller
  verwaltet. Für das Update nach dem Spiel Arcadia beenden."). Text: "Solange Arcadia Steam Controller und Puck
  verwaltet, kommt kein Firmware-Update bei ihnen an. Steam meldet deshalb einen Fehler. Für das Update gib die
  Controller frei oder beende Arcadia, und starte es dann in Steam erneut." Not clicked away. "Controller freigeben"
  and "Arcadia beenden" act when [A] (or the mouse) is held 1 s, filling while held; "Schließen" (focused first) acts
  on a plain press, and [B] closes it too. Only a fresh press counts. After the release it says "Steam Controller und
  Puck freigegeben", how the update goes on and "Damit Arcadia sie wieder übernimmt: Pan in den
  Controller-Einstellungen aus- und wieder einschalten, oder Arcadia neu starten."; [A] or [B] closes it.
