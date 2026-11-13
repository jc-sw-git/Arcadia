# Product decisions: own tags (#495)

Own tags group games the player's way (later the filter uses them). They are global across launchers, stored in
`user-data.json` beside the own titles, kept for games absent from the library, and never changed by a provider.

- **Names:** trimmed; empty creates or renames nothing; at most 40 characters (the keyboard greys typing beyond);
  control characters refused. Equal ignoring case, the first spelling stays. Alphabetical everywhere (German, ignoring
  case). A tag without games stays ("0 Spiele"); only Y deletes a tag.
- **Tag window** ("Tags" on the detail page under "Verwalten" and in the context menu, also a quick-selection entry):
  head "Tags · <game>", "Neuer Tag" first, then every tag with a tick where the game has it. A ticks or unticks at once;
  "Neuer Tag" writes a name, made and ticked for the game (an existing name ticks that one). Y deletes after asking,
  B closes back to where it was opened. No renaming here.
- **Tag list** ("Eigene Tags (N)" in "Datenquellen", group "EIGENE DATEN"): "Neuer Tag" first (made without games; an
  existing name takes the focus), then "<Name> · <n> Spiele". A shows the tag's games alphabetically by shown title
  with their launcher ("Kein Spiel hat diesen Tag." without); A on a game opens its detail page, LT/RT turn through the
  tag's games only, the game's launcher tab is not opened; B back to the games on that game. X renames (a change of
  case only needs no question; another tag's name asks to merge, which keeps that tag's name and every game once);
  Y deletes after asking; B back to "Datenquellen" on the row.
- **Counts** count games in the library; a game outside it keeps its tags, delete and rename reach it too.
- **Questions:** the confirmation dialog, focus on "Abbrechen", B = no: „<Name>“ löschen? with how many games lose
  it; Mit „<Name>“ zusammenführen?
- **Detail page:** "Übersicht" shows own tags before the providers' (3, then "+n"); "Infos" lists them under
  "EIGENE TAGS" above the providers' tags. A provider tag of an own tag's name shows once, as the own one.
- **Unreadable `user-data.json`:** no own tags anywhere; "Tags" and "Eigene Tags" are greyed with the note that own
  tags and titles can't be read; read again at the next start.
