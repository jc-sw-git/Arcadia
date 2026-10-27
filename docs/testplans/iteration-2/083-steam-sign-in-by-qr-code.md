# #83 Steam sign-in by QR code for owned games

Steam's page in the launcher settings has a group "Konto" with the row "Steam-Konto" while Steam is active and found.
A shows a QR code; approved in the Steam app on the phone, Arcadia keeps the sign-in (DPAPI) and fetches the owned
games (`IPlayerService/GetOwnedGames`), which the Steam tab and the search show as not installed. The list is cached
(`%APPDATA%\Arcadia\steam-owned-games.json`) and fetched again in the background at every scan, at most every 30 s.

- [ ] **T83-1** Sign in with the controller and the phone only: ≡ → Launcher → Steam → "Steam-Konto" → A shows the QR
  code in the info panel; scanned and approved in the Steam app the row shows the Steam profile name and "Abmelden",
  and the Steam tab gets the uninstalled games within seconds; B while the code shows drops it and stays on the page;
  the group is gone while Steam is switched off · E2E: no (real Steam and phone; the flow against a faked Steam is
  automated)
- [ ] **T83-2** No approval for 2 minutes: the code disappears, "Abgelaufen – [A] für neuen Code", A shows a new code;
  rejected on the phone: the same with "Abgelehnt" · E2E: yes (faked Steam); rejecting on a real phone: by hand
- [ ] **T83-3** Signed in: the Steam tab shows all owned games, refunded ones (007 First Light, Alchemy Factory) don't
  appear; games hidden in Steam don't appear; compare the count with Steam's "Alle" under games and note any
  difference here; uninstalled games show playtime and last played from Steam · E2E: no (real account)
- [ ] **T83-4** A on an uninstalled Steam game: Steam's install dialog opens; once confirmed, the cover shows the
  install progress and ends installed without a restart · E2E: no (real Steam install)
- [ ] **T83-5** Steam signed in with another account than Arcadia: no uninstalled games; the row has a blue i and says
  "Steam ist mit einem anderen Konto angemeldet", the info panel explains why; no toast · E2E: yes
- [ ] **T83-6** Offline (network cable out) Arcadia starts with the last cached list, within the start budget; the row
  shows the warning "Steam ist nicht erreichbar", no toast; back online, "Neu einlesen" clears it · E2E: yes (faked
  Steam); the budget on the owner's PC by hand
- [ ] **T83-7** "Abmelden" asks first (focus on "Abbrechen", B keeps the sign-in); confirmed, the uninstalled games
  leave tab and search at once and `steam-account.dat` and `steam-owned-games.json` are gone · E2E: yes
- [ ] **T83-8** After T83-1 to T83-7: `arcadia.log` holds no token, no SteamID or account id and no account or profile
  name (search for the name and the ids); `steam-account.dat` is unreadable as text · E2E: no (real log; a unit test
  checks the log lines against a faked Steam)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T83-1, T83-2, T83-5, T83-6, T83-7 | 2026-10-27 | Claude | PR branch, acceptance tests against a faked Steam web API (Linux, headless) | ✅ | – |
| T83-8 | 2026-10-27 | Claude | PR branch, unit test `Log_holds_no_token_no_account_id_and_no_name` | ✅ | – |
| T83-1 | 2026-10-27 | Claude | PR branch, real app on Linux/Xvfb, Steam folder picked by hand, Steam's API unreachable from the cloud | ✅ "Anmelden", A → "Steam nicht erreichbar – [A] erneut versuchen"; QR code and sign-out question only looked at in a headless capture | Real sign-in needs the owner's test |
