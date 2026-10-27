# Steam — account (#83, 2026-10-27, partly checked against the real Steam)

Index: `README.md`.

`Steam/Account/`: a QR sign-in over Steam's web API, as Steam's phone app and `node-steam-session` (MIT, reference only)
do it. `IAuthenticationService/BeginAuthSessionViaQR`, `PollAuthSessionStatus` and `GenerateAccessTokenForApp` take
`input_protobuf_encoded` (base64 protobuf, POST) and answer protobuf; the result is the `x-eresult` header (1 = OK).
Arcadia signs in as platform `MobileApp` (device name "Arcadia"): only such a refresh token may get new access tokens
over the web API (WebBrowser gets AccessDenied since 2025-04-30). The code renews itself (`new_challenge_url`,
`new_client_id`; the owner saw it change about every 30 s); the old one is then invalid. A poll error after
`had_remote_interaction` counts as rejected on the phone; without a scan, as a session Steam ended, and a new one starts.
Tokens are JWTs (`sub` = SteamID64, `exp`); the access token lives about a day, the refresh token about 200 days.
Owned games: `IPlayerService/GetOwnedGames` with `access_token`, `include_appinfo`, `include_played_free_games`,
`include_free_sub`. Owner's account (T83-3, 2026-10-27): 413 apps, games and software only, no soundtracks or tools,
and fewer games than Steam's "Spiele" count. So Arcadia also reads the store's `dynamicstore/userdata` (`rgOwnedApps`,
every owned app incl. DLC; the phone app's access token is the store cookie `steamLoginSecure=<id>||<token>`) and
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

