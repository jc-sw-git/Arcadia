# SteamButler

Fügt Non-Steam-Games aus **Epic, GOG, Ubisoft, EA und Xbox/Game Pass** als
Steam-fremde Spiele hinzu — inklusive Artwork, Launcher-„Regalen" (Collections)
und automatischem Aufräumen deinstallierter Titel. Für Spiele, die den
Steam-Input-Hook verlieren (v. a. Windows-Store/Game-Pass), erzeugt SteamButler
einen kleinen Wrapper, der über [SISR](https://github.com/Alia5/SteamInputSteamRedirect)
die passende Controller-Konfiguration aktiviert.

> Status: **frühe Phase / Scaffold.** Der Epic-Scanner läuft, der Rest ist als
> dokumentierte Stubs angelegt. Siehe [docs/architecture.md](docs/architecture.md)
> für Design-Entscheidungen und offene Punkte.

## Warum

BoilR deckt Scannen + `shortcuts.vdf` + Artwork bereits ab, wird aber kaum noch
gepflegt und kann keine Collections. SteamButler zielt auf drei Dinge, die dort
fehlen: verlässliche Launcher-Regale, dynamisches Cleanup, und die
SISR-Zwischenschicht für Hook-verlierende Titel.

## Plattformen

Primär **Windows** und **macOS**, mit dem Ziel, auch auf **SteamOS/Steam Deck**
lauffähig zu bleiben. Der plattformspezifische Teil (Registry, UWP-Start,
Wrapper) ist Windows-first; der geteilte Kern ist OS-agnostisch.

## Aufbau

Cargo-Workspace:

| Crate | Zweck |
|-------|-------|
| `crates/core` | OS-agnostisch: AppID-CRC32, `shortcuts.vdf`, Domain-Modell, Steam-Pfade |
| `crates/scanners` | Scanner-Trait + per-OS-Backends (Epic real, restliche Stubs) |
| `crates/wrapper` | winziges SISR-Wrapper-Binary (`forceinputappid` + Lifecycle) |
| `crates/cli` | Frontend zum Scannen/Testen (`steambutler`) |

Eine GUI (`eframe`/`egui`) folgt, sobald der CLI-getriebene Kern steht.

## Build & Ausführen

```bash
cargo build
cargo run -p steambutler-cli -- scan
cargo run -p steambutler-cli -- steam
cargo test
```

### Windows (GNU-Toolchain)

Lokal entwickelt wird mit der GNU-Toolchain (`stable-x86_64-pc-windows-gnu`).
Deren „self-contained"-mingw ist unvollständig (kein `as`), daher braucht der
lokale Build ein vollständiges mingw-w64 auf dem PATH — z. B.
[WinLibs](https://winlibs.com/) (MSVCRT-Runtime, passend zum `-gnu`-Target):

```powershell
winget install BrechtSanders.WinLibs.POSIX.MSVCRT
rustup set default-host x86_64-pc-windows-gnu
```

CI baut zusätzlich nativ auf `windows-latest` (MSVC), `macos-latest` und
`ubuntu-latest` — die committete `rust-toolchain.toml` pinnt nur den Channel,
nicht den Host.

## Lizenz

MIT — siehe [LICENSE](LICENSE).
