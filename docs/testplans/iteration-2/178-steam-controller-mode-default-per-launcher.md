# #178 Choose the Steam Controller mode default per launcher

Every launcher's page gets the group "Controller" with "Steam Controller" ("Arcadia" / "Steam-Eingabe"): the mode its
games on "Standard" get. Per game three values, "Standard (…)", "Arcadia", "Steam-Eingabe". "Alle Spiele auf die
Vorauswahl setzen" puts a launcher's games back on "Standard". The link "Controller-Einstellungen für Steam" (#169) is
gone. Decisions: issue #178 and [launchers.md](../../product/launchers.md); designs T178Launcher, T178Spiel.

- [x] **T178-1** Einstellungen → Launcher → Steam: below "Bibliothek" the group "CONTROLLER" with "Steam Controller",
      value "Steam-Eingabe", subtitle "Vorauswahl für die Spiele dieses Launchers"; the info panel explains it, shows
      the two mode cards and "Ab Werk: Steam-Eingabe, weil Spiele aus Steam Steams Belegungen nutzen. Gilt ab dem
      nächsten Spielstart." Epic Games (and every other launcher): the same rows, value "Arcadia", "Ab Werk: Arcadia.".
      ◀ ▶ switch the value; the footer names ◀ ▶ "Ändern". · E2E: yes
- [x] **T178-2** Steam Controller connected. Set a game of Epic Games to "Arcadia" in its details, then Epic Games'
      default to "Steam-Eingabe": back in the library the other Epic games show "Standard (Steam-Eingabe)", that one
      still "Arcadia", Steam's games are unchanged. A game on "Standard" started now gets no Xbox pad of Arcadia's
      (Steam's configuration applies); the one on "Arcadia" gets the pad. A game that already ran keeps its mode until
      it ends. · E2E: yes
- [x] **T178-3** Detail page of a Steam game, focus on "Steam Controller": [A] goes "Standard (Steam-Eingabe)" →
      "Arcadia" → "Steam-Eingabe" → "Standard (Steam-Eingabe)", the note names the next value. ≡, Steam's default to
      "Arcadia", ≡ back: the button reads "Standard (Arcadia)". The preview's value (click) and the context menu (⧉)
      cycle the same way; while the game runs, "Erst beim nächsten Start". · E2E: yes
- [x] **T178-4** Two Steam games with their own mode: Steam's page shows, indented below "Steam Controller", "Alle
      Spiele auf die Vorauswahl setzen" with "2 Spiele haben einen eigenen Modus" and the pill "Zurücksetzen"; its info
      panel lists both games with their mode. [A]: the question "Alle Spiele auf die Vorauswahl setzen?" names the games
      and the mode they get; [B] closes it, nothing changes. [A], [A]: the row disappears, the focus is on "Steam
      Controller", no toast; both games show "Standard (…)" again, also after a restart. Without own choices the row
      isn't there. · E2E: yes
- [x] **T178-5** Controller → "Controller-Wächter (Pan)" off, then a launcher's page: both rows greyed out,
      subtitle "Aus – erst mit dem Controller-Wächter (Pan)", the info panel says where to switch Pan on; ▲ ▼ reach
      them, ◀ ▶ and [A] change nothing, no "Ändern" in the footer. Pan on again: both change as before, the values are
      the old ones. Works without a Steam Controller connected too. Where Pan can't run (Linux), Pan switched on: the same,
      subtitle "Auf diesem System noch nicht verfügbar", no hint to switch Pan on. · E2E: yes
- [x] **T178-6** Steam's page no longer has "Controller-Einstellungen für Steam"; "Steam Controller" is its last row
      (without own choices). · E2E: yes
- [x] **T178-7** Settings survive a restart (default and own choices). With the `settings.json` of a build before this
      one, where games had a mode different from the old default: after the update those games show "Arcadia" or
      "Steam-Eingabe" (their own choice), all others "Standard (…)", and every game starts with the same mode as before.
      · E2E: yes
- [x] **T178-8** An inactive launcher's page (and one that isn't installed) shows both rows and they can be changed
      with Pan on; the value is kept. · E2E: yes
- [x] **T178-9** Steam's default set to "Arcadia", a Steam game not in Arcadia's library started directly in Steam:
      it gets Arcadia's Xbox pad (`arcadia.log`); a game with its own choice keeps it. · E2E: yes
- [x] **T178-10** Mouse only: a click on ◀ / ▶ of "Steam Controller" switches it (greyed out with Pan off: nothing);
      a click on "Zurücksetzen" asks; in the question "Zurücksetzen" and "Abbrechen" are clicked, a click beside it or
      on ✕ cancels; with the mouse the buttons name the keys (Leertaste, Esc), not [A]/[B]. · E2E: yes
- [x] **T178-11** Controller → info panel of "Controller-Wächter (Pan)": under "STEAM CONTROLLER" the text says the
      mode is chosen per launcher on its page and changed per game in its details (no fixed "Vorausgewählt ist …").
      · E2E: no (a text to read)
- [ ] **T178-12** Factory reset (#146) resets the launchers' defaults and the games' own choices. · E2E: no (#146 isn't
      built yet; both live in `settings.json` like every setting it resets, so it is checked with #146)

T169-4 and T169-5 (the link on Steam's page) no longer apply: the link went with this issue; their tests were removed.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T178-1 to T178-10 | 2026-10-29 | Claude | PR branch, Debug, Linux cloud container (.NET 10, Avalonia headless) | ✅ `LauncherControllerModeTests`, `Window/LauncherControllerModeViewTests`; each was red once on a deliberate break (listed in the PR) | On the owner's PC |
| T178-5 | 2026-10-29 | Claude | same, Xvfb, `--demo` | ✅ Pan is unsupported on Linux: both rows greyed out, reachable, subtitle "Aus – erst mit dem Controller-Wächter (Pan)" (since the QE review "Auf diesem System noch nicht verfügbar"), no "Ändern" in the footer | With Pan on only on the owner's PC |
| T178-3, T178-4 | 2026-10-29 | Claude | same, Xvfb, `--demo`, Pan forced on by a local patch (not committed) | ✅ [A] in the game menu sets an own mode; reset row with count and info list, question with keys Leertaste/Esc, after "Zurücksetzen" the row is gone | On the owner's PC with Pan really on |
| T178-3, T178-4, T178-5, T178-10 | 2026-10-29 | QE thread, then Claude | PR#319 review round, Linux cloud container, Avalonia headless | ✅ 5 tests added from the QE review (preview and ⧉ cycle, question singular and over five, Pan unsupported, clicks with Pan off), each red once on the break named in the review | – |
| T178-10 | 2026-10-29 | Claude | same, Avalonia headless | ✅ the arrows beside "Steam Controller" are disabled with Pan off and enabled with Pan on; red once with `Allows` always true | – |
| T178-1 to T178-11 | 2026-10-30 | owner | PR#319, owner's PC, Windows | ✅ passed | T178-12 open until #146 (acceptance review #222) |
