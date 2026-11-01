# Steam — account (#83, #280)

`Steam/Account/`: sign-in on Steam's own store login page in Arcadia's browser (`IWebPages`, a system web view:
WebView2 / WebKitGTK, profile in `%APPDATA%\Arcadia\WebView`), the way comparable products do it. The browser opens and runs its script only on https pages under `SteamStorePage.Domains` (`steampowered.com`,
`steamcommunity.com`, subdomains included); a link anywhere else stays unopened, a link that asks for a new window opens
in the same page if it leads to those sites, otherwise nowhere (#372). WebKitGTK (Linux) asks for subframes too, so a
foreign iframe on a Steam page stays empty there and logs one line. Every store page then holds
`#application_config` with `data-userinfo` (`logged_in`, `steamid`) and `data-store_user_config` (`webapi_token`, a JWT:
`sub` = SteamID64, `exp`, about a day); `SteamStorePage` reads both. A new token: reload `store.steampowered.com/explore/`
unseen (Steam renews its own cookies there); a page signed out or in with another account means the sign-in is gone.
Sign-out deletes the cookies of `steampowered.com`/`steamcommunity.com` and the avatar (`GetPlayerLinkDetails`
`sha_digest_avatar`, Base64 SHA-1 → `avatars.steamstatic.com/<hex>_full.jpg`, kept as `steam-avatar.jpg`). **Never pose
as another app:** a build that signed in by QR as Steam's phone app (`MobileApp`, refresh token, token as
`steamLoginSecure` cookie) got the owner's account restricted as possibly stolen the same day; such a stored sign-in is
deleted at start.
Owned games: `IPlayerService/GetOwnedGames` with `access_token`, `include_appinfo`, `include_played_free_games`,
`include_free_sub`. That lists games and software only, no soundtracks or tools, and fewer games than Steam's "Spiele"
count. So Arcadia also reads the store's `dynamicstore/userdata` (`rgOwnedApps`, every owned app incl. DLC; read unseen
in the browser, so the store's own cookies count) and names and types those apps from `appinfo.vdf`; one `appinfo.vdf`
doesn't know is left out. Free tools and soundtracks only a package holds come from the store's `rgOwnedPackages` plus
package 0, resolved to apps via `appcache/packageinfo.vdf` (`SteamPackageInfo`, v39/v40, no entry size); of package 0
only tools count (Steam hides its applications, e.g. Source Filmmaker). `ISteamUser/GetPlayerSummaries`
answers HTTP 400 to an access token (it wants a web API key); the profile name comes from
`IPlayerService/GetPlayerLinkDetails` (`public_data.persona_name`).
Games hidden in Steam: `userdata/<id>/config/cloudstorage/cloud-storage-namespace-1.json`, a JSON list of
`[key, {key, value, …}]`; the entry `user-collections.hidden` holds the collection as a JSON string, app ids in
`added` (plain text). Another account in the running Steam (`ActiveUser`, read every 2 s) keeps the
games, but blocks installing (`steam://install` installs into Steam's account) and notes it on every Steam game.
