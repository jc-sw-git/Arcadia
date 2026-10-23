# Product decisions: Mouse mode

Agreed with the owner; don't change without asking. Read this before changing this area
(index in `CLAUDE.md` → Product decisions).

- Mouse mode (#64): Back + Start held (1–9 s, default 3) switches it, also with Arcadia in the background; one mode for all
  pads; never saved (Arcadia starts normal). Its buttons are listed in `MouseModeRows.HelpRows`; in it
  Arcadia ignores gamepad navigation. View and Menu therefore act on release, or once held alone 0.2 s (so ≡ held still
  opens details), and not at all when pressed together (owner, 2026-10-19). "Mausmodus" is one choice of three, so only one way
  decides (owner, 2026-10-20): "Aus" (all off, the other values kept), "Von Hand" (Back + Start; "Mausmodus beenden, wenn Arcadia im Vordergrund ist"
  optionally ends it when Arcadia comes to the front) or "Automatisch" (on when Arcadia loses the focus to a non-game, off when Arcadia or a game comes to
  the front; Back + Start overrides it until Arcadia was in front again, e.g. for a game Arcadia doesn't know).
  "Beenden des Mausmodus in Spielen erzwingen" (only "Von Hand"): "Nein", "Ja" (in a library game in front Back + Start
  doesn't switch it on and a game coming to the front ends it) or "Individuell" (a default "Erlaubt"/"Deaktiviert", and
  "Individueller Mausmodus" in each game's preview and detail page: "Standard (erlaubt)" or "Standard (deaktiviert)",
  naming the default, then "Erlaubt", "Deaktiviert") (owner, 2026-10-20 and 2026-10-21). "Zurück zu Arcadia" is its own setting: View held alone (1–9 s, default 1; no "at once", since a tap of ⧉ moves the mouse mode's keyboard, #110) brings
  Arcadia back from the background, with or without the mouse mode, with a box whose frame fills (owner, 2026-10-20).
  Exceptions per process are planned for later. Mouse mode's on-screen keyboard (#110): Y opens it in front of other
  windows (own topmost window that never takes the focus and goes back on top whenever another window comes to the
  front; the Start menu and Windows' other shell flyouts still cover it, only signed programs under Program Files get above them; characters go out as Unicode with `SendInput`, ⌫ ↹ ↵ and the
  arrows as keys), B hides it (layout menu first), the mouse mode goes on. While it is open: D-pad focus, A types,
  X erases (held: repeats), Y space, LB shift (also on ↹ ↵ and the arrows) and RB AltGr as in the search, ≡ Enter,
  R3 layout menu, ⧉ tapped moves it to the other screen edge (saved), ⧉ held = "Zurück zu Arcadia" and ⧉ + ≡ as
  always, both close it; triggers and sticks stay the mouse. Laid out like the real keyboard (DE, UK, FR, ES ISO
  with the tall Enter, US ANSI), caps lock a blank key the focus skips, no Ctrl/Alt/Win; right block: layout,
  edge, hide, arrows. On the foreground window's monitor, centred at the last edge unless the caret Windows knows
  would lie under it; scales like the Big Picture layout. Shares the layout last chosen with the search. Closes
  when a game comes to the front or the mouse mode ends. With Arcadia's search in front, Y doesn't open a second
  keyboard: the search's takes the mouse mode's buttons (≡ = ↵ starts the hit) until B, the search closes or Arcadia
  leaves the front (owner, 2026-10-22). "Steams Desktop-Belegung ignorieren" (on; its own box, independent of
  the mouse mode, owner 2026-10-20) drops simulated mouse and keys from other programs while a controller is in use; beside a library game without the mouse mode only while ⧉ + ≡ are
  held, and then only keys (`SteamDesktopInputFilter`: low-level hooks only while a controller is connected and the
  option needs them, set anew every 30 s because Windows drops a slow hook silently). "Arcadia in front" is the
  foreground window's process and no other program's window covering Arcadia whole (`MouseModeHost.IsArcadiaActive`),
  not Avalonia's activation, which got out of step beside games. A game in front is a process from a library game's
  folder, or one whose path can't be read (anti-cheat) that started after Arcadia last started a game (owner, 2026-10-20).
- The log names what is in front only as Arcadia, „Spiel“, „kein Spiel“ or „kein Fenster“, never the program:
  which programs someone uses is private (owner, 2026-10-23, review #66).
