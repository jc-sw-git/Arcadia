# Product decisions: Game start, detail page and game actions

- A starts a game on release; holding A for 1 s opens the detail page (a ring around the preview fills from
  0.2 s on, letting go before 1 s cancels). With the mouse a click picks a cover, a double click starts it and
  holding the left button 1 s opens the details with the same ring. The context menu and the detail page offer the same actions
  (`GameActionViewModel.For`); the detail page has no menu (#499):
  - On the game's artwork beside its cover: title, then one button row "Spielen", "Zu Favoriten", "In … öffnen" and
    a quiet note beside it (update, installation, soundtrack) that the focus never takes. The page always opens on
    "Spielen" with "Übersicht" open. Under the title the info line (#493): genres · developer · year, cut with "…" in one
    line, and the first age rating as a badge (USK before PEGI before ESRB); a missing part leaves no "·", none at all no line.
  - Below, a panel with a rail of areas, each with a value line: "Übersicht", "Infos", "Controller" (Steam Controller mode,
    "Individueller Mausmodus") and "Verwalten" ("Artwork ändern", "Tags", "Titel ändern", "Titel zurücksetzen", "Deinstallieren …"). ▼ from any button goes to the
    rail, ▲ on the first area back to "Spielen"; a focused area shows its content at once, A goes into its rows, B back
    to the rail. An area A enters shows A's glyph inside its focus ring, so ◀ ▶ aren't tried (#530). ◀ ▶ change a value row at once, both ways.
  - "Übersicht" (#500): left the quick selection, up to 5 rows of the other areas in the order of the areas, each acting
    as its row (a greyed one says why below the entries, there is no info box), and set apart "Übersicht anpassen";
    right the game's facts, which take no focus. ▲ ▼ walk the entries and "Übersicht anpassen", round at the ends; it
    keeps the focus when the carousel turns, an entry past the next game's last goes to its last. Above the facts the
    short description (4 lines, then "…"; without one the start of the full one), at the foot the first 3 tags and "+n".
  - "Infos" (#493) has no rows: the full description on the left, every field with its source on the right (genres,
    developer, publisher, release date, each age rating; a USK only from Steam's `steam_germany` says "laut Steam"),
    then the tags. It is not entered (no glyph, A does nothing): while it has the
    focus in the rail, RS scrolls both columns together, and so does the wheel over them (#530). Without any metadata it
    says there is nothing yet. It is never a quick selection entry.
    Sources show only here. Metadata arriving while the page is open shows at once, the focus stays.
  - "Übersicht anpassen" is a dialog, not a game action (not in the context menu): every row of the other areas by
    area in two columns, ▲ ▼ walk a column, ◀ ▶ go to the other column at the same height or the nearest entry there
    (#530), A ticks or unticks and holds at once, "n von 5", with 5 the others greyed and passed over. Below, "Für alle
    Spiele übernehmen" asks in place ("Abbrechen" first) and stays open after "Ersetzen". A game shows its own selection,
    else the one for all games, else the defaults (every entry there is); "Ersetzen" drops every own selection in the
    same save and holds for games added later. So a new default entry reaches only games never changed while nothing was
    applied to all. Stored by stable ids in `settings.json`; an unknown id drops out, an unreadable selection gives the
    defaults with a log line. There is no "Standard wiederherstellen".
  - A row that doesn't apply now is greyed out but never hidden and still takes the focus, so an area keeps its rows;
    the info box beside the rows explains the focused row and says why it is greyed. A greyed button of the row is
    skipped and says why in its tooltip.
  - Value rows show their value in the settings' chip. The info box, the same as the settings' info panel (#506): the
    blue note (why the row is greyed, or what an action does instead) at the very top, then the row's name, one box per
    value saying what it does (the current one highlighted, following ◀ ▶ at once), then the rest of the text.
- The detail page is a carousel through the tab's games in the library's sort order, across pages and
  wrapping at the ends, held LT/RT included: the shown game's cover in front, the games before and after it in stacks to
  the left and right that lead into the depth (two covers each with five games or more, one with two to four; with two
  games the other on both sides; one: no stacks). LT/RT turn it, LB/RB switch the tab and stay on the detail page
  (first game), L3 re-sorts with the shown game kept; all of them keep the open area and the focused button, area or
  row. Held LT/RT turn only the covers, the page follows on release with a jump: a hold jumps at its first turn and at its
  last, never in between (#530). Opened from a tab, R3 (G, the funnel beside the sort at the foot) opens the tab's
  filter over the page, the same as in the library (#530); the carousel then turns through the hits only. A shown game
  that drops out gives way to the next hit after it in the order before; with no hit left the page closes to the
  library. From the search R3 does nothing. The wheel turns only over the background; over the panel it scrolls what is under it
  and never turns (#530). A turn on its own lets title and content jump
  in; a turn during that jump or as quickly after it (LT/RT tapped, the wheel spun) stops it and shows the game at once,
  so nothing is replayed after the last press (#506). B from the buttons or the rail returns to the
  library page of the game shown last. X opens the search over it; B in the search returns to it (#127).
- Launcher actions (open in the launcher, uninstall) are `LaunchTarget`s on `Game`;
  a null one is shown disabled with the reason, or for uninstall opens the launcher instead.
- If an update is available, A still says "Spielen" and starts the game; the launcher installs the update
  first. The update shows as an icon, and the detail page's note says that the launcher may update first. Where the launcher's install monitor tells (Steam), the icon and the note appear and go live within a poll,
  without a rescan and without a toast (#288). Launchers have no update-only command: Steam ignores `steam://install` for
  installed games and `steam://validate` verifies every file first; comparable products offer none either.
- What A says depends on the entry's type (#141): "Spielen" for a game, "Tüfteln" for software and
  tools, "Abrocken" for a soundtrack; the same text on the button, in the context menu and in the button hint. Not
  installed it says "Installieren". While software or a tool runs, A says "Weiter tüfteln" instead of "Zurück ins
  Spiel". A soundtrack is handed to its launcher, so it gets no "Startet" or "Läuft" state: Steam
  plays it and, a moment later, also shows the soundtrack's page, as "In Steam öffnen" does. Its detail page notes "Steam spielt den Soundtrack ab." beside the buttons.
- After "Installieren" the game shows "Bitte in … bestätigen" (an Epic engine: "Bitte in … starten") for 1 minute,
  then "Installieren" again: launchers don't report a cancelled install dialog, and a later confirm still shows its
  progress. A on a waiting game opens the launcher's install dialog again and restarts the minute (#343).
- "Artwork ändern" (#490, prototype in #13) works on the open game at once, nothing to confirm or cancel: A uses the
  focused tile, also the one in use (it becomes set by hand); Y gives only the open tab back to Arcadia's choice. An own
  file stays offered under "Eigene Datei" after another choice, until X replaces it; it is cropped centred like every
  image. A file that isn't a readable PNG, JPEG or WebP of at most 20 MB is refused with a warning.
- "Titel ändern" (#496) opens the on-screen keyboard over the page, prefilled with the shown title; ↵, Enter or ≡ take it,
  B or Esc keep the old one. Spaces around it are dropped; an empty title or the automatic one (the launcher's, or a
  provider's correction) clears the own title. The own title shows everywhere at once (library, search, sorting, the
  carousel's order) and outlives rescans, "Alle Metadaten neu laden" and the game's absence from the library. Like
  every game action also in the context menu; from there the focus follows the game to its new place in the sort.
  "Titel zurücksetzen" is greyed without an own title. While the game has one, ⧉ (R) shows and hides "Original-Titel:
  <automatic title>" under the title in place of the info line (so a two-line title keeps the button row in place), the
  hint "[⧉] Original-Titel" only then (R3 is the filter there, as in the library, #530); every page shown anew (another game, the page left) starts without the line.
  While `user-data.json` is unreadable both rows are greyed with the reason, the header has a warning, and its entry
  opens "Eigene Titel" in "Datenquellen", which says why (damaged, locked, from a newer Arcadia) and what to do.
- "Tags" (#495) opens the tag window of the game; own tags show before the providers' tags
  ([own-tags.md](settings-data-sources/own-tags.md)).
