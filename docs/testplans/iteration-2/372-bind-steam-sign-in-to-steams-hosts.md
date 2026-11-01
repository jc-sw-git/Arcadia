# #372 Bind the Steam sign-in web view to Steam's hosts

Arcadia's browser opens and reads only https pages on Steam's own sites (`steampowered.com`, `steamcommunity.com` and
their subdomains). Unit tests cover which addresses open and are read (`WebPagesTests`); the web view itself doesn't run
headless, so the real page is checked by hand.

- [ ] **T372-1** ≡ → Launcher → Steam → "Steam-Konto" → A opens Steam's login page; follow a link on it that leads off
  Steam (e.g. the privacy policy's link to another site, or a social media link in the footer): the page stays where it
  was, the address above it still shows a Steam address, `arcadia.log` says "Browser: <host> gehört nicht zu
  steampowered.com, steamcommunity.com, nicht geöffnet."; links to other Steam pages (e.g. "Hilfe") open; B goes back;
  signing in afterwards works as T280-1 · E2E: no (real web view and Steam's real page)
- [ ] **T372-2** On Steam's login page, follow links that would open a new window (e.g. with the middle mouse button or
  a link that opens in a new tab): one to another Steam page opens in the same page, one off Steam opens nothing and
  the page stays as it was; no second window appears in either case and the page never turns white · E2E: no (real web
  view and Steam's real page)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| unit tests | 2026-11-01 | Claude | PR branch, Linux container (`WebPagesTests`, faked Steam pages) | ✅ | – |
