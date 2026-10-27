# #83 Steam sign-in by QR code for owned games

Steam's page in the launcher settings starts with the group "Konto" and the row "Steam-Konto" (disabled while Steam
is inactive or missing). A shows a QR code; approved in the Steam app on the phone, Arcadia keeps the sign-in (DPAPI)
and fetches the owned apps (`IPlayerService/GetOwnedGames`, plus soundtracks and tools from Steam's store list), which
the Steam tab and the search show as not installed. The list is cached (`%APPDATA%\Arcadia\steam-owned-games.json`)
and fetched again in the background at every scan, at most every 30 s.

Changed after the owner's first test (2026-10-27): the code has no time limit, the group comes first and stays
(disabled) while Steam is off, another account keeps the games, uninstalled soundtracks and tools appear, no exceptions
in the debugger's output for expected cases. Texts reworded by the owner the same day (signed out, signed in, sign-out
question, other account).

- [ ] **T83-1** Sign in with the controller and the phone only: ≡ → Launcher → Steam → "Steam-Konto" (first row) → A
  shows the QR code in the info panel; scanned and approved in the Steam app the row shows the Steam profile name (not
  the login name) and "Abmelden", and the Steam tab gets the uninstalled games within seconds; B while the code shows
  drops it and stays on the page; with Steam switched off the row stays, disabled, its info says why, A does nothing ·
  E2E: no (real Steam and phone; the flow against a faked Steam is automated)
- [ ] **T83-2** Leave the code unapproved for 3 minutes: it stays, Steam renews it now and then (the new one shows at
  once) and the row counts "Code erneuert vor … s" up from each renewal; scanning the latest code still works;
  rejected on the phone: "Abgelehnt – [A] für neuen Code", A shows a new code · E2E: yes (faked Steam); a real renewal
  and a rejection on a real phone: by hand
- [ ] **T83-3** Signed in: the Steam tab shows all owned games, refunded ones (007 First Light, Alchemy Factory) don't
  appear; games hidden in Steam don't appear; with "Soundtracks" and "Werkzeuge" switched on, the uninstalled ones
  appear too, no DLC; compare the counts per type with Steam's library filter (Spiele, Soundtracks, Software,
  Werkzeuge) and note any difference here; uninstalled games show playtime and last played from Steam · E2E: no (real
  account; the store list against a faked Steam is automated)
- [ ] **T83-4** A on an uninstalled Steam game: Steam's install dialog opens; once confirmed, the cover shows the
  install progress and ends installed without a restart (live progress is #278) · E2E: no (real Steam install)
- [ ] **T83-5** Switch Steam to another account than Arcadia's while Arcadia shows the Steam tab: within a few
  seconds, without a press, every Steam game's preview and detail page note "In Steam ist derzeit ein anderes Konto
  angemeldet als in Arcadia …" (uninstalled: installs not possible; installed: may not start or use wrong saves); the uninstalled games stay, "Installieren" is disabled with that reason (A on the cover: a toast,
  no install dialog); the row has a blue i; switch back or close Steam: the notes go · E2E: yes
- [ ] **T83-6** Offline (network cable out) Arcadia starts with the last cached list, within the start budget; the row
  shows the warning "Steam ist nicht erreichbar", no toast; back online, "Neu einlesen" clears it · E2E: yes (faked
  Steam); the budget on the owner's PC by hand
- [ ] **T83-7** "Abmelden" asks first (focus on "Abbrechen", B keeps the sign-in); confirmed, the uninstalled games
  leave tab and search at once and `steam-account.dat` and `steam-owned-games.json` are gone · E2E: yes
- [ ] **T83-8** After T83-1 to T83-7: `arcadia.log` holds no token, no SteamID or account id and no account or profile
  name (search for the name and the ids); `steam-account.dat` is unreadable as text; with a debugger attached, sign-in,
  B and a fetch show no exceptions in its output (only a real network failure may) · E2E: no (real log; a unit test
  checks the log lines against a faked Steam)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T83-1, T83-2, T83-5, T83-6, T83-7 | 2026-10-27 | Claude | PR branch, acceptance tests against a faked Steam web API (Linux, headless) | ✅ | – |
| T83-8 | 2026-10-27 | Claude | PR branch, unit test `Log_holds_no_token_no_account_id_and_no_name` | ✅ | – |
| T83-1 | 2026-10-27 | Claude | PR branch, real app on Linux/Xvfb, Steam folder picked by hand, Steam's API unreachable from the cloud | ✅ "Anmelden", A → "Steam nicht erreichbar – [A] erneut versuchen"; QR code and sign-out question only looked at in a headless capture | Real sign-in needs the owner's test |
| T83-1, T83-3, T83-5, T83-6, T83-7, T83-8 | 2026-10-27 | Owner | PR build on the owner's PC, real Steam and phone | ❌ 11 findings: counts (no uninstalled soundtracks/tools, fewer games), code expiry, group position, exceptions in the debug output, group hidden while Steam off, another account; profile name HTTP 400 in the log | Fixed in this PR; findings 7–9 became #277 and #278 |
| T83-1, T83-2, T83-3, T83-5, T83-6, T83-7 | 2026-10-27 | Claude | PR branch after the fixes, acceptance tests against a faked Steam web API (Linux, headless); each new test made red once | ✅ | – |
| T83-3 | 2026-10-27 | Claude | Owner's `appinfo.vdf`, `packageinfo.vdf` and cached list, counted offline with Arcadia's readers (owned packages approximated: packages holding an owned app) | ✅ software 2, soundtracks 7 as in Steam; tools 148 (Steam 169: Arcadia hides Proton and runtimes); games 413 owned, the installed extras unverified | Owner's count after the next build |
