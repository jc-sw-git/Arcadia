# Releases and updates

- Windows x64 only for now. Installer and self-update with **Velopack**: `tools/release/Pack.ps1 -Version x.y.z`
  publishes self-contained and runs `vpk pack` → `artifacts/releases/Arcadia-win-Setup.exe` + update feed.
  Installs without admin rights to `%LOCALAPPDATA%\Arcadia\current\Arcadia.exe`. Nothing else may go into
  `%LOCALAPPDATA%\Arcadia`: the setup takes any file there for an installation and deletes the folder on
  "overwrite" or repair. Arcadia's own files (settings, logos, Epic play times, logs) are all in `%APPDATA%\Arcadia`
  (`LogLocation`; logs of 0.1.0-rc.1 in `%LOCALAPPDATA%\Arcadia\logs` move there at the next start).
  The `Velopack` package and the `vpk` tool (`.config/dotnet-tools.json`) share one version.
- `VelopackApp.Build()…Run()` must stay the first thing in `Program.Main`. Uninstalling asks whether SISR, VIIPER and
  usbip-win2 go too (`ControllerSupportRemoval`) and whether `%APPDATA%\Arcadia` goes too (owner, 2026-10-24); both
  default to no. Velopack kills the uninstall hook after 30 s, so the questions share 20 s and say how long they wait;
  the removal runs the important steps first. The "SISR Marker" goes from every account's `shortcuts.vdf`, with Steam
  closed (up to 8 s) and started again, because Steam rewrites that file from memory on exit (owner, 2026-10-24).
- `ArcadiaUpdateService` checks GitHub Releases (stable only) every 6 h and on "Nach Updates suchen" in
  Einstellungen → "Über Arcadia". "Jetzt aktualisieren" shows only with an update; it downloads, Arcadia closes
  and Velopack installs silently (no input needed) and restarts it. Updating while a game runs is fine: SISR
  keeps running across the restart (the new Arcadia adopts it), pauses for running games are handed over with
  `--sisr-paused-for <dir>`, games started from Arcadia with `--started-game <key>` (their end brings Arcadia back, #94),
  and `--background` brings the window back behind the game instead of in front.
  "Was ist neu" lists the bullets of every release body between the running and the newest version
  (`Changelog`). Push a tag `vX.Y.Z` to release (`release.yml`).
- Every CI run on Windows packs the installer; only pushes to `main` upload it as an artifact (1 day). The account's
  artifact storage is small and counted per hour over the billing month, so deleting doesn't free it at once:
  `prune-artifacts.yml` keeps only the newest 5 (after each CI run, daily, or by hand), and a failed upload only
  warns, it doesn't fail the CI (#115).
- Versioning (owner's rule): SemVer, 0.x until the first full release 1.0. Major may break; minor adds only
  non-breaking features; patch only fixes bugs — no features, no intended behaviour change (security aside).
  This matters because third-party plugins are planned.
