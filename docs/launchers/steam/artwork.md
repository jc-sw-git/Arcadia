# Steam — covers and backgrounds (#13, #487)

`SteamArtwork` offers a game's images in this order; Arcadia's cache copies the one it picks (`docs/architecture/core.md`).

1. **Own images** of the account whose library Arcadia shows (`SteamInstallation.UserDataDirectory`):
   `userdata/<account>/config/grid/<appid>p.<ext>` (cover) and `<appid>_hero.<ext>` (background), `.png`, `.jpg`,
   `.jpeg` or `.webp`. Set in Steam with "Verwalten → Eigenes Artwork festlegen". They don't count as set by hand.
   None on the owner's PC: tested with made-up files only.
2. **Steam's library cache** `appcache/librarycache/<appid>/library_600x900.jpg` and `library_hero.jpg`, directly in the
   game's folder or in a hash-named subfolder. Verified on the owner's PC: it holds only images Steam has shown (382
   covers and 459 backgrounds in 1,050 folders).
3. **Steam's image server** by AppID, no key, no cookie: `https://shared.steamstatic.com/store_item_assets/steam/apps/<appid>/…`
   and, as a second try, `https://cdn.cloudflare.steamstatic.com/steam/apps/<appid>/…`, same file names. Not reachable
   from the cloud containers; the real answers are checked by the owner's test (T487-23).

An AppID that isn't a plain number (a manipulated manifest) names no file and no address.
