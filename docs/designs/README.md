# Designs

Static exports of the design canvas — the source of truth for Arcadia's UI:
https://claude.ai/artifact/8A9qxc67RcsuFmavaqCAuN (private, owner only). Many artboards are
interactive there (tabs, sorting, page sizes, view dialog, search with on-screen keyboard,
toggles); the PNGs show their default state.

`source/` holds the artboard sources (`*.dc.html`) with exact texts, colours, sizes and the
demo logic — prefer them over the PNGs when implementing. `source/canvas.json` lists all
artboards with their titles and sizes.

| Image | Artboard | Content |
|---|---|---|
| [01-library.png](01-library.png) | `Main.dc.html` | Big Picture library: favourites tab + launcher tabs (scrollable, edge arrows), preview panel, 8-column grid with update/favourite badges, pagination, status bar, footer hints. Interactive: tabs, view modal (Y), context menu (≡ / right click), favourites. |
| [02-game-detail.png](02-game-detail.png) | `GameDetail.dc.html` | Detail page: hero, cover, launcher + update chips, primary action, "play without update" hint, stats (last played, playtime, installed, input). |
| [03-search.png](03-search.png) | `Search.dc.html` | Search across launchers, `/steam-epic` prefix, launcher chips, results, on-screen keyboard with layout switcher (R3). |
| [04-artwork-modal.png](04-artwork-modal.png) | `ArtworkModal.dc.html` | Change cover/background: SteamGridDB suggestions, own file, preview. |
| [05-spotify-connect.png](05-spotify-connect.png) | `SpotifyConnect.dc.html` | Connect Spotify (browser login, three steps). |
| [06-spotify-picker.png](06-spotify-picker.png) | `SpotifyPicker.dc.html` | Pick background music: playlists, saved songs, search; shuffle/repeat/device. |
| [07-settings-launchers.png](07-settings-launchers.png) | `Launchers.dc.html` | Settings › Launcher: connected launchers, status, keyword, tab toggle, add own launcher. |
| [08-settings-launcher-edit.png](08-settings-launcher-edit.png) | `LauncherEdit.dc.html` | Edit a launcher: logo (24×24 in tabs), tab name, search keyword, refresh game list, auto refresh. |
| [09-settings-localization.png](09-settings-localization.png) | `Language.dc.html` | Settings › Lokalisierung: UI language, on-screen keyboard default + quick selection, 12/24 h. |
| [10-settings-appearance.png](10-settings-appearance.png) | `Appearance.dc.html` | Settings › Darstellung: status bar items, artwork (missing, overwrite, multiple hits), sounds & music (file/Spotify, volume). |
| [11-settings-big-picture.png](11-settings-big-picture.png) | `BigPicture.dc.html` | Settings › Big Picture: autostart (+ only with controller), display and audio device on enter/leave. |
| [12-desktop-mode.png](12-desktop-mode.png) | `Desktop.dc.html` | Desktop mode (mouse/keyboard) with the same functionality as Big Picture. |

## Re-exporting

The artboards need the canvas runtime (`artifact-type/dc-runtime.js` of the artifact, not
committed). Download it and the current `project/*.dc.html` with the Artifact tool, put them in
one folder with the runtime saved as `support.js`, and render each artboard with headless Chrome:

```powershell
& 'C:\Program Files\Google\Chrome\Application\chrome.exe' --headless=new --disable-gpu --hide-scrollbars `
  --window-size=1920,1080 --virtual-time-budget=10000 --allow-file-access-from-files `
  --user-data-dir=$env:TEMP\arcadia-render-profile --screenshot=out.png file:///C:/path/Main.dc.html
```
