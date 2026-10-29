# Product decisions: Launchers: activate, order, launcher page, first start

## Launcher list (#136)

- The settings' first category "Launcher" lists "Aktiv · erscheinen als Tabs" in tab order (key 1 … 0), then "Nicht
  aktiv" (installed first, then not found, greyed out). Saved as `ActiveLaunchers` (order = tabs); without it every
  launcher found is active.
- ◀ ▶ switch "Aktiv": off means not scanned, no tab, no search hits, not watched; on goes to the end of the active
  ones, off to the top of the inactive ones. A click anywhere on the switch's box switches it too (a click elsewhere on
  the row opens its page). Not found can't be switched on; active and no longer found can be switched off.
- Y lifts a row, ▲ ▼ move it, Y drops it (under "Nicht aktiv": off), B cancels; the mouse drags.
- X, or the row "Alle neu einlesen" in a group "Bibliothek" below the launchers (for the mouse alone, its pill shows
  [X] / F), reads all again; A opens the launcher's page (below).
- An active launcher without games or no longer found keeps its tab with an empty state ("Keine Spiele in …" / "…
  nicht gefunden", "Launcher-Einstellungen" and "Neu einlesen" / "Erneut suchen"). With no launcher active the library
  says "Kein Launcher aktiv" the same way, with only "Launcher-Einstellungen" (nothing to read before one is switched
  on).
- Beside the rows two boxes: "Launcher-Reihenfolge" (the tabs as they will be, the library's selected tab not marked)
  and "Launcher-Details" (head, status message, library, location; no repeat of what row and preview
  show).
- An active launcher no longer found is a warning; one without games is information (blue i in its row and note, no
  symbol on the category's tab). "Steam Controller Kompatibilität" has a lamp beside its name while
  on (colours: `docs/product/settings.md`).
- A status message row shows one short line below its title (`HealthIssue.Summary`), the whole text in the info panel;
  descriptions below a row name stay at one, at most two lines. A row's action is a pill as wide as the value chips,
  filled with the accent while focused.

## Launcher page (#137, #138)

- A or a click on a launcher in the list opens its page in place of the rows; B (or a click on "Launcher" above its
  name) goes back to the list with that launcher focused. LT/RT (Q/E, a click on a neighbour in the head) turn to the
  launcher before or after it in the list's order, wrapping.
- Groups "Allgemein": Aktiv, "Name im Tab", "Such-Kürzel", "Logo"; "Bibliothek": "Ordner" and "Neu einlesen" (only
  active and found); "Controller" (below). Steam's page no longer links to "Controller-Einstellungen für Steam" (#169
  went with #178).
- Name and keyword are written with the shared on-screen keyboard below the page (without ↹ and the edge key; the
  search's buttons, ≡/Enter take, B/Esc drop). A keyword's keyboard offers only the keys it may hold (lower-case
  letters and digits). A problem (keyword taken, too long) shows in red while typing; taken anyway, the old value
  stays and a toast says why. Empty means the launcher's own again.
- "Logo" is a button, not a choice: "Hochladen …" opens the file dialog (PNG, SVG, ICO, WebP up to
  2 MB, copied); only while an own logo is used an indented row "Standardlogo" below it offers "Wiederherstellen". In
  the tab a logo is fitted into 24 × 24 px.
- "Neu einlesen" reads only that launcher (also on its empty tab); X and "Alle neu einlesen" in the list read all.
- "Ordner" is only a fallback when Arcadia can't find the launcher (no choice between automatic and own): the folder is
  checked when picked and taken only if the launcher is there, then it is switched on and read. Its dialog opened by
  controller turns the mouse mode on (`docs/product/mouse-mode.md`).

## Software, tools and soundtracks (#141)

- A launcher's switches stand in "Bibliothek" above "Neu einlesen", each default off, acting at once (no rescan) and
  saved.
- Steam: "Werkzeuge einbeziehen" (`Tool`), "Software einbeziehen" (`Application`), "Soundtracks einbeziehen"
  (`Music`); demos are games; runtimes, redistributables and every other type never appear.
- Epic: "Software und Engines einbeziehen" (Unreal Engine, all versions as one entry that starts the newest, and
  launchable add-ons such as Unreal Editor for Fortnite), counted as Werkzeug; Epic's soundtracks (`digitaldownloads`)
  stay out.
- "Neu einlesen" and "Launcher-Details" ("Einträge") show the library's count line (`docs/product/library.md`).

## Steam Controller default (#178)

- Every launcher's page, the same for each, has the group "Controller" with "Steam Controller" (◀ ▶ "Arcadia" /
  "Steam-Eingabe", subtitle "Vorauswahl für die Spiele dieses Launchers"): the mode its games on "Standard" get
  (designs T178Launcher/T178Spiel). Factory value: the launcher's own (`LauncherInfo.GamesUseSteamInput`: Steam
  "Steam-Eingabe", all others "Arcadia"), stored only when it differs (`LauncherPreferences.UsesArcadiaPad`), so no
  launcher is a special case. Per game: `docs/product/controls.md`.
- Below it, indented and only while a game of the launcher's library has its own mode, "Alle Spiele auf die
  Vorauswahl setzen" (pill "Zurücksetzen", subtitle "N Spiele haben einen eigenen Modus", the info panel lists them).
  [A] asks ("Alle Spiele auf die Vorauswahl setzen?", [A] Zurücksetzen, [B] Abbrechen), then every game of the launcher
  is on "Standard", also those no longer in the library, in one write; the row goes, the focus moves to "Steam
  Controller", no toast.
- Both rows change only while "Controller-Wächter (Pan)" is on (also without a Steam Controller connected); with Pan
  off they are greyed out but focusable, subtitle "Aus – erst mit dem Controller-Wächter (Pan)" (where Pan can't run
  yet, e.g. Linux: "Auf diesem System noch nicht verfügbar", as Pan's row), values kept. Also on an inactive or not
  found launcher's page. A change applies at a game's next start; a running game keeps its mode.

## Steam account (#83)

- Steam's page starts with the group "Konto", the row "Steam-Konto"; while Steam is inactive or missing it stays there,
  disabled, and says why (a sign-in stays stored).
- Sign-in on Steam's own login page (QR code with the Steam app, or name and password), as Playnite does it; Arcadia
  never poses as another client such as Steam's phone app (`docs/launchers/steam/account.md`): A on "Anmelden" opens it over the whole window with its address shown like a
  browser's, scaled to the window; B or ✕ closes it and stays on the launcher page; while it shows, no press reaches
  Arcadia below. Opened by controller, the mouse mode comes on for it (`docs/product/mouse-mode.md`).
- Signed out: pill "Anmelden", subtitle "Melde dich an, um vollen Zugriff auf deine Spielebibliothek zu haben". Page
  not loaded: "Steam nicht erreichbar – [A] erneut versuchen". No hint elsewhere while signed out.
- Signed in: the info panel starts with the avatar and name; the row shows the Steam profile name and "Abmelden", which
  asks first and then deletes sign-in and cached list; the uninstalled games leave tab and search at once.
- Another account in the running Steam: the games stay, "Installieren" is disabled with the reason, preview and detail
  page note it (uninstalled: no installs from Arcadia meanwhile; installed: may not start or use the wrong saves), and
  the row has an info (blue i); a closed Steam counts as no account.
- A failed fetch or an expired sign-in is a warning on the row, never a toast; the cached list applies offline.
- Uninstalled entries count in the count line's total, not in the installed bracket; their playtime and last start
  come from Steam's web API, installed ones keep `localconfig.vdf`. All types: uninstalled software, tools and
  soundtracks appear under #141's switches like installed ones, free ones from packages too, as Steam counts them
  (except Proton and runtimes, which Arcadia never shows).

## First start (#139)

- Arcadia never takes a launcher on just because it is installed. While `settings.json` has no `ActiveLaunchers` (also
  after an update from a version without it), a page "Welche Launcher soll Arcadia nutzen?" stands in place of the
  library, pattern "Leiste".
- "Gefunden" switched on with their games, "Nicht gefunden" greyed out and not switchable; ◀ ▶ (and A, a click on the
  switch) switch. The info panel shows above the preview "Launcher-Reihenfolge" as in the launcher settings, below the
  focused launcher (games, location, keyword), "Später änderbar" and "Fertig"; the header has date and clock like
  every page.
- ≡, Enter or a click on "Fertig" save the list; B does nothing. Only on/off is chosen there, order and looks in the
  settings.
- `--demo` skips it (all launchers active, nothing saved); `--first-run` shows it again (with `--demo` the answer holds
  for the session only).
