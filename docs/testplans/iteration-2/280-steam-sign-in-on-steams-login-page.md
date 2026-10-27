# #280 Steam sign-in on Steam's own login page

#83's sign-in posed as Steam's phone app, and Steam restricted the owner's account right after the merge (2026-10-27).
Rebuilt the way Playnite does it: A on "Anmelden" opens Steam's own store login page in Arcadia's browser (WebView2,
profile in `%APPDATA%\Arcadia\WebView`), the user signs in there (QR code in the Steam app, or name and password), and
Arcadia reads the web API token from the store page. A new token comes from reloading the store page unseen; signing
out deletes Steam's cookies. #83's test cases T83-1 to T83-8 still apply; T83-1 and T83-2 are covered by the new
acceptance tests below instead of the QR code ones.

Use another Steam account than the restricted one if you can, and give Steam a day before T280-7.

- [ ] **T280-1** Sign in with the Steam app: ≡ → Launcher → Steam → "Steam-Konto" → A opens Steam's login page over the
  whole window, headed "Bei Steam anmelden" with the address "https://store.steampowered.com"; scan its QR code in the
  Steam app and approve: the page closes by itself, the row shows the profile name and "Abmelden", the Steam tab gets
  the uninstalled games within seconds · E2E: yes (faked Steam pages); the real page by hand
- [ ] **T280-2** B on the gamepad while the page shows closes it and stays on Steam's launcher page ("Anmelden"); A and
  the D-pad meanwhile do nothing in Arcadia; the ✕ at the top right closes it too (mouse); signing in on the phone after
  closing doesn't sign Arcadia in · E2E: yes (faked Steam pages); ✕ and the real page by hand
- [ ] **T280-3** Sign in with name, password and Steam Guard code typed on the keyboard in the page: same result as
  T280-1; links to other sites on the page don't open · E2E: no (real Steam page and keyboard input into the web view)
- [ ] **T280-4** Network cable out, A on "Anmelden": the row says "Steam nicht erreichbar – [A] erneut versuchen", no
  page stays open; cable in, A opens the page · E2E: yes (faked Steam pages); the real load failure by hand
- [ ] **T280-5** Close Arcadia, start it again: still signed in, games fetched without the page showing; a day later
  (token expired) too, `arcadia.log` shows "Spiele … geladen" and no sign-in page · E2E: no (real token expiry; unit
  tests cover renewal against a faked store page)
- [ ] **T280-6** "Abmelden" and confirm: uninstalled games leave at once; A on "Anmelden" then shows Steam's login page
  again instead of signing in at once (Steam's cookies are gone) · E2E: yes
- [ ] **T280-7** The day after the sign-in: Steam shows no restriction and no "unexpected device" mail; in Steam's
  account settings the sign-in appears as a browser sign-in, not as a phone · E2E: no (real Steam account)
- [ ] **T280-8** Starting this build after #83's build (signed in there): the row shows "Anmelden", `arcadia.log` says
  "Anmeldung der ersten Version (als Handy-App) gelöscht"; after T280-1 to T280-6 the log holds no token, no SteamID
  or account id and no profile name · E2E: no (real update; unit tests cover both)

Risks only the owner's PC can show (the web view can't run in the cloud): the hidden web view loading the store page
for a new token, WebView2's script results, the native page inside Arcadia's scaled 1080p layout.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T280-1, T280-2, T280-4, T280-6 | 2026-10-27 | Claude | PR branch, acceptance tests against faked Steam pages through the real `WebPages` service and window view model (Linux, headless; the web view itself replaced by `TestBrowser`); each new test made red once | ✅ | – |
| T280-5, T280-8 | 2026-10-27 | Claude | PR branch, unit tests (`Expired_token_is_read_anew_from_the_store_page_without_showing_the_login_page`, `Sign_in_of_the_first_version_posing_as_the_phone_app_is_deleted_at_the_start`, `Log_holds_no_token_no_account_id_and_no_name`) | ✅ | – |
