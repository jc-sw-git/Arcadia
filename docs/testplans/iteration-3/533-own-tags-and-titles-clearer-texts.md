# #533 Own tags and titles: clearer texts, the game's tags in the info box, a length limit notice

Derived from the owner's findings in the hand test of #43 (#495, #496) as written in #533. Keys follow
`docs/product/controls.md` (WASD, Space = A, Esc = B). The notice belongs to the name editor, so it holds for "Neuer Tag"
in the tag window and in the tag list and for renaming in the tag list alike.

**Status:** Automated: T533-1 to -4 · QE: T533-5 · Open for the owner: T533-6

- [x] **T533-1** Detail page, "Verwalten" → "Tags": the info box reads „Weise deinen Spielen eigene Tags zu, um besser
      nach ihnen filtern zu können. Du kannst deine Tags auch unter Einstellungen → Datenquellen verwalten.“ · E2E: yes
- [x] **T533-2** Below it the info box lists the game's own tags alphabetically under "EIGENE TAGS", following a change in
      the tag window at once; a game without own tags shows „Dieses Spiel hat noch keine eigenen Tags.“; other rows list
      no tags; while `user-data.json` is unreadable the greyed row lists neither chips nor that line. · E2E: yes
- [x] **T533-3** "Titel ändern" and "Titel zurücksetzen" speak of the original title: info box „… Ein leerer Titel stellt
      den ursprünglichen wieder her.“, subtitle „Zurück zum ursprünglichen Titel“, dialog note „Leer lassen oder „<Titel>“
      eingeben stellt den ursprünglichen Titel wieder her.“, and the note of an unreadable `user-data.json` says
      „ursprünglichen Titel“. · E2E: yes
- [x] **T533-4** "Neuer Tag": at 39 characters no notice; the 40th (on-screen keyboard or real keyboard) shows
      „Maximale Länge erreicht: höchstens 40 Zeichen“ below the field; more is refused and the notice stays; erasing one
      hides it; the full name can still be taken. · E2E: yes
- [ ] **T533-5** The look: the notice below the field in the tag dialog with the keyboard greyed, the info box with the
      tag chips (a 40-character tag cut with "…") and with the line for none. · E2E: no (window)
- [ ] **T533-6** **Owner:** read the new texts in place (info box of "Tags", "Titel ändern", the notice) and judge the
      wording. · E2E: no (wording is the owner's call)

Automated tests:

| Test cases | Tests |
|---|---|
| T533-1, -2 | `Arcadia.App.Tests/OwnTagsAndTitleTextsTests.The_info_box_of_Tags_explains_them_and_lists_the_games_own_tags`; the unreadable file in `OwnTagsTests.An_unreadable_file_greys_Tags_and_Eigene_Tags_with_the_note_and_changes_nothing` |
| T533-3 | `Arcadia.App.Tests/OwnTagsAndTitleTextsTests.Titel_aendern_speaks_of_the_original_title` |
| T533-4 | `Arcadia.App.Tests/OwnTagsAndTitleTextsTests.A_new_tags_name_at_its_most_characters_says_so_and_can_still_be_taken` |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| All automated tests (`dotnet test`) | 2026-11-13 | implementer | Debug, Linux cloud container | ✅ | – |
| T533-5 | 2026-11-13 | implementer | Debug, Linux cloud container (Xvfb 1920 × 1080, `--demo`, keys) | ✅ notice in warning colour below the field, keys greyed at 40; info box with "Keine"-line, then two chips after adding tags | – |
| T533-2 (unreadable file) | 2026-11-13 | implementer | Debug, Linux cloud container | ✅ after the fix | QE finding 1: the greyed row said „keine eigenen Tags“ → lists nothing now |
