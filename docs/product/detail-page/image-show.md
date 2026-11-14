# Product decisions: "Bild ansehen"

Index: [README.md](README.md). The background's place on the page: [page.md](page.md).

- After 10 s without any input on the detail page, the page fades out (about 1 s) and the shown game's background shows
  whole and centred, its edges fading into the blurred, darkened copy around it (#565). Only the shown game's title
  stays, small, bottom left: no button hints, no clock. No button or setting starts it or changes the 10 s.
- After another 10 s a random background of another game blends in (about 1.5 s), then every 10 s the next. The pool is
  the games of the tab the page turns through, with its filter (from the search: its hits), that have a background,
  without the page's own game; none comes twice before every other one was shown. With no other game the own image
  stays; without an own background the show begins with another game's, without any it doesn't start. An image that
  can't be loaded is skipped silently.
- Any input ends it: a button, a stick, the mouse moving or clicking, the wheel, a key. The page of the original game
  fades back in with the focus as before; the input itself does nothing else (an A never starts a game), nor do its
  repeats and its release.
- It starts only while nothing is open over the page (dialog, on-screen keyboard, settings, search) and Arcadia is in
  front. In the background (a game runs) no timer runs and nothing moves; leaving the front ends it at once, and back
  in front the 10 s start anew. Nothing of it is saved.
