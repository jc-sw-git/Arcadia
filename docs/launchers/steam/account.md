# Steam — account (#83, #280, 2026-10-27, partly checked against the real Steam)

Index: `README.md`.

`Steam/Account/`: sign-in on Steam's own store login page in Arcadia's browser (`IWebPages`, a system web view:
WebView2 / WebKitGTK, profile in `%APPDATA%\Arcadia\WebView`), the way Playnite does it. Every store page then holds
`#application_config` with `data-userinfo` (`logged_in`, `steamid`) and `data-store_user_config` (`webapi_token`, a JWT:
`sub` = SteamID64, `exp`, about a day); `SteamStorePage` reads both. A new token: reload `store.steampowered.com/explore/`
unseen (Steam renews its own cookies there); a page signed out or in with another account means the sign-in is gone.
Sign-out deletes the cookies of `steampowered.com`/`steamcommunity.com` and the avatar (`GetPlayerLinkDetails`
`sha_digest_avatar`, Base64 SHA-1 → `avatars.steamstatic.com/<hex>_full.jpg`, kept as `steam-avatar.jpg`). **Never pose as another app:** the first #83
build signed in by QR as Steam's phone app (`MobileApp`, refresh token, token as `steamLoginSecure` cookie), and Steam
restricted the owner's account as possibly stolen the same day (2026-10-27); such a stored sign-in is deleted at start.
Owned games: `IPlayerService/GetOwnedGames` with `access_token`, `include_appinfo`, `include_played_free_games`,
`include_free_sub`. Owner's account (T83-3, 2026-10-27): 413 apps, games and software only, no soundtracks or tools,
and fewer games than Steam's "Spiele" count. So Arcadia also reads the store's `dynamicstore/userdata` (`rgOwnedApps`,
every owned app incl. DLC; read unseen in the browser, so the store's own cookies count) and
names and types those apps from `appinfo.vdf`; one `appinfo.vdf` doesn't know is left out. Still missing (2026-10-27):
free tools and soundtracks only a package holds (169 tools in Steam, 120 from package 0). So the store's
`rgOwnedPackages` plus package 0 are resolved to apps via `appcache/packageinfo.vdf` (`SteamPackageInfo`, v39/v40, no
entry size); of package 0 only tools count (Steam hides its applications, e.g. Source Filmmaker). `ISteamUser/GetPlayerSummaries`
answers HTTP 400 to an access token (it wants a web API key); the profile name comes from
`IPlayerService/GetPlayerLinkDetails` (`public_data.persona_name`).
Games hidden in Steam: `userdata/<id>/config/cloudstorage/cloud-storage-namespace-1.json`, a JSON list of
`[key, {key, value, …}]`; the entry `user-collections.hidden` holds the collection as a JSON string, app ids in
`added` (owner's PC: 7 ids, plain text). Another account in the running Steam (`ActiveUser`, read every 2 s) keeps the
games, but blocks installing (`steam://install` installs into Steam's account) and notes it on every Steam game.
