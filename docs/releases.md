# Releases and updates

- Windows x64 only for now. Installer and self-update with **Velopack**: `tools/release/Pack.ps1 -Version x.y.z`
  publishes self-contained, publishes Pan as Native AOT into `Pan\Pan.exe` beside `Arcadia.exe` (needs Visual Studio's
  C++ build tools; the CI's Windows runner has them) and runs `vpk pack` → `artifacts/releases/Arcadia-win-Setup.exe`
  + update feed. Installs without admin rights to `%LOCALAPPDATA%\Arcadia\current\Arcadia.exe`. Nothing else may go
  into `%LOCALAPPDATA%\Arcadia`: the setup takes any file there for an installation and deletes the folder on
  "overwrite" or repair. Arcadia's own files (settings, logos, Epic play times, logs) are all in `%APPDATA%\Arcadia`.
  The `Velopack` package and the `vpk` tool (`.config/dotnet-tools.json`) share one version.
- `VelopackApp.Build()…Run()` must stay the first thing in `Program.Main`.
- Pan installs itself to `%ProgramFiles%\Arcadia\Pan` with one UAC prompt; an installed Pan that isn't exactly the
  bundled one is updated at start once the player confirms; declined, Pan is switched off
  (`docs/controller-support/pan-update.md`). `Pan.exe --restore`
  (settings: "Controller wiederherstellen") gives the controllers back if the service itself hangs; if Pan can't give
  a controller back, HidHide's Configuration Client (Start menu, admin) is the user's emergency exit: untick the puck
  and the other controllers and turn hiding off (`docs/controller-support/pan-hiding.md`, `pan-hiding-controllers.md`).
- Uninstalling (only from Windows; Arcadia offers no "Deinstallieren", and these questions need no controller) asks
  "Treiber für die Controller-Emulation auch entfernen?" (usbip-win2, HidHide, SISR/VIIPER; `ControllerSupportRemoval`)
  and whether `%APPDATA%\Arcadia` goes too; both default to no. Pan goes without a question, with one UAC prompt
  (`docs/controller-support/pan-setup.md`). Velopack's Update.exe kills the uninstall hook after 60 s
  (`commands/uninstall.rs` in 1.2.161; its C# docs say 30 s, recheck on updates), so the questions share 40 s and say
  how long they wait; the removal runs the important steps first. Asking without a time limit: #164.
- `ArcadiaUpdateService` checks GitHub Releases (stable only) every 6 h and on "Nach Updates suchen" in Einstellungen →
  "Über Arcadia". "Jetzt aktualisieren" shows only with an update; it downloads, Arcadia closes and Velopack installs
  silently and restarts it. Updating while a game runs is fine: games started from Arcadia are handed over with
  `--started-game <key>` (their end brings Arcadia back, #94), and `--background` brings the window back behind the
  game instead of in front. "Was ist neu" lists the bullets of every release body between the running and the newest
  version (`Changelog`).
- To release, run the workflow "Tag release" on `main` with the version: `tag-release.yml` sets the tag `vX.Y.Z` and
  starts `release.yml`. Claude can start it; its cloud sessions can't push tags (the git proxy answers 403).
- Every CI run on Windows packs the installer; only pushes to `main` upload it as an artifact (1 day). The account's
  artifact storage is small and counted per hour over the billing month, so `prune-artifacts.yml` keeps only the
  newest 5 of each artifact name, and a failed upload only warns (#115). Every CI run measures code coverage on
  Windows and uploads it as `coverage-report` (HTML, 7 days); locally `dotnet test Arcadia.slnx --collect:"XPlat Code
  Coverage"` writes Cobertura XML under each test project's `TestResults/`.
- Versioning: SemVer, 0.x until the first full release 1.0. Major may break; minor adds only non-breaking features;
  patch only fixes bugs — no features, no intended behaviour change (security aside). This matters because
  third-party plugins are planned. Code signing of `Pan.exe` and the installer: #271.
