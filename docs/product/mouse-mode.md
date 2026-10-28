# Product decisions: Mouse mode

How Steam's desktop layout and the foreground are detected technically: `docs/controller-support/desktop-layout.md`.

## Switching it (#64)

- Back + Start held (1–9 s, default 3) switches it, also with Arcadia in the background; one mode for all pads; never
  saved (Arcadia starts normal). Its buttons are listed in `MouseModeRows.HelpRows`; in it Arcadia ignores gamepad
  navigation.
- View and Menu therefore act on release, or once held alone 0.2 s (so ≡ held still opens details), and not at all
  when pressed together (owner, 2026-10-19).
- "Mausmodus" is one choice of three, so only one way decides (owner, 2026-10-20):
  - "Aus": all off, the other values kept.
  - "Von Hand": Back + Start; "Mausmodus beenden, wenn Arcadia im Vordergrund ist" optionally ends it when Arcadia
    comes to the front.
  - "Automatisch": on when Arcadia loses the focus to a non-game, off when Arcadia or a game comes to the front;
    Back + Start overrides it until Arcadia was in front again (e.g. for a game Arcadia doesn't know).
- "Beenden des Mausmodus in Spielen erzwingen" (only "Von Hand"): "Nein", "Ja" (in a library game in front Back + Start
  doesn't switch it on and a game coming to the front ends it) or "Individuell": a default "Erlaubt"/"Deaktiviert",
  and "Individueller Mausmodus" in each game's preview and detail page: "Standard (erlaubt)" or "Standard
  (deaktiviert)", naming the default, then "Erlaubt", "Deaktiviert" (owner, 2026-10-20 and 2026-10-21).
- "Bei System-Dialogfenstern einschalten" (under "Mausmodus", for "Von Hand" and "Automatisch", greyed out with "Aus")
  switches it on while a dialog opened from the controller is open (#138).
- A launcher's web page opened by controller (#280, e.g. Steam's sign-in) switches the mode on with its keyboard open,
  also with "Mausmodus: Aus", as the system dialogs of #138 do; after the page it goes off again if it came on for it,
  and the keyboard closes if the page opened it (owner, 2026-10-27).
- Exceptions per process are planned for later.

## "Zurück zu Arcadia"

Its own setting: View held alone (1–9 s, default 1; no "at once", since a tap of ⧉ moves the mouse mode's keyboard,
#110) brings Arcadia back from the background, with or without the mouse mode, with a box whose frame fills (owner,
2026-10-20).

## The mouse mode's on-screen keyboard (#110)

- Y opens it in front of other windows: its own topmost window that never takes the focus and goes back on top
  whenever another window comes to the front. The Start menu and Windows' other shell flyouts still cover it; only
  signed programs under Program Files get above them. Characters go out as Unicode with `SendInput`, ⌫ ↹ ↵ and the
  arrows as keys. B hides it (layout menu first), the mouse mode goes on.
- While it is open: D-pad focus, A types, X erases (held: repeats), Y space, LB shift (also on ↹ ↵ and the arrows) and
  RB AltGr as in the search, ≡ Enter, R3 layout menu, ⧉ tapped moves it to the other screen edge (saved), ⧉ held =
  "Zurück zu Arcadia" and ⧉ + ≡ as always, both close it; triggers and sticks stay the mouse.
- Laid out like the real keyboard (`docs/product/on-screen-keyboard.md`; right block: layout, edge, hide, arrows). On the
  foreground window's monitor, centred at the last edge unless the caret Windows knows would lie under it; scales like
  the Big Picture layout. Shares the layout last chosen with the search.
- Closes when a game comes to the front or the mouse mode ends. With Arcadia's search in front, Y doesn't open a second
  keyboard: the search's takes the mouse mode's buttons (≡ = ↵ starts the hit) until B, the search closes or Arcadia
  leaves the front (owner, 2026-10-22).

## Steam's desktop layout

"Steams Desktop-Belegung ignorieren" (on; in the settings group "Steam", #169, independent of the mouse mode, owner
2026-10-20) drops simulated mouse and keys from other programs while a controller is in use; beside a library game
without the mouse mode only while ⧉ + ≡ are held, and then only keys.

## What is in front

- "Arcadia in front" is the foreground window's process with no other program's window covering Arcadia whole
  (`MouseModeHost.IsArcadiaActive`), not Avalonia's activation, which got out of step beside games.
- A game in front is a process from a library game's folder, or one whose path can't be read (anti-cheat) that
  started after Arcadia last started a game (owner, 2026-10-20).
- The log names what is in front only as Arcadia, „Spiel“, „kein Spiel“ or „kein Fenster“, never the program:
  which programs someone uses is private (owner, 2026-10-23, review #66).
