# Product decisions: the detail page's layout

Index: [README.md](README.md).

- On the game's artwork beside its cover: title, then one button row "Spielen", "Zu Favoriten", "In … öffnen" and
  a quiet note under "Spielen" (update, installation, soundtrack) that the focus never takes, on a soft dark cloud that
  fades out at every edge so it reads over a bright image (#541). A long title shrinks to half
  its size before it wraps (two lines, then "…"), so the button row stays in place (#531). The page always opens on
  "Spielen" with "Übersicht" open. Under the title the info line (#493): genres · developer · year, cut with "…" in one
  line, and the first age rating as a badge: the system region's board (#557; Windows' "Land oder Region": Germany and
  Austria USK, USA and Canada ESRB, everywhere else PEGI), then USK before PEGI before ESRB, the same order in "Infos"; a missing part leaves no "·", none at all no line.
  A rating badge's border has the board's colour for that rating (#541): USK 0 white, 6 yellow, 12 green, 16 blue,
  18 red; PEGI 3/7 green, 12/16 orange, 18 red; ESRB, which has no colours, E/E10+ green, T orange, M/AO red.
- Below, a panel with a rail of areas, each with a value line: "Übersicht", "Infos", "Controller" (Steam Controller mode,
  "Individueller Mausmodus") and "Verwalten" ("Artwork ändern", "Tags", "Titel ändern", "Titel zurücksetzen", "Deinstallieren …"). ▼ from any button goes to the
  rail, ▲ on the first area back to "Spielen"; a focused area shows its content at once, A goes into its rows, B back
  to the rail. An area A enters shows A's glyph inside its focus ring, so ◀ ▶ aren't tried (#530). ◀ ▶ change a value row at once, both ways.
- "Übersicht" (#500): left the quick selection, up to 5 rows of the other areas in the order of the areas, each acting
  as its row (a greyed one says why right under the fifth entry's place, there is no info box), and set apart
  "Übersicht anpassen", centred between the fifth entry's place and the panel's lower edge; with fewer entries both keep
  the places they have with five (#541);
  right the title again and the game's facts, which take no focus. ▲ ▼ walk the entries and "Übersicht anpassen", round at the ends; it
  keeps the focus when the carousel turns, an entry past the next game's last goes to its last. Above the facts the
  short description (4 lines, then "…"; without one the start of the full one; with none at all here and in "Infos"
  „Es liegt keine Beschreibung zu dem Spiel vor.“), at the foot the first 3 tags and "+n".
- "Infos" (#493) has no rows: the full description on the left, every field with its source on the right (genres,
  developer, publisher, release date, each age rating, its source named the same way for every rating, #541),
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
