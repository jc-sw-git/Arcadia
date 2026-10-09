# SteamButler — Architektur & Entscheidungen

Festgehalten aus der Konzeptphase, damit die Begründungen nicht verloren gehen.

## Zielbild

Ein Tool, das Non-Steam-Games aus mehreren Launchern als Steam-fremde Spiele
einträgt: **Scanner → Shortcut-Writer (zwei Emissionsmodi) → Artwork → Collections
→ Cleanup**. Dazu ein separates Wrapper-Binary für die SISR-Zwischenschicht.

## Stack: Rust

- BoilR (Rust) ist der Präzedenzfall für genau diesen Job, plattformübergreifend
  inkl. Steam Deck.
- Das Wrapper-Binary wird pro Hook-verlierendem Spiel mitgeliefert und muss klein
  und runtime-frei sein → statisches Rust-Binary (~hunderte KB) statt .NET-Runtime.
- SteamOS/Deck-Ökosystem ist Rust/Python-zentriert; statische musl-Binaries laufen
  dort friktionsfrei.
- GUI später via `eframe`/`egui` (pure Rust, ein Binary, kein Webview — läuft auch
  im Deck-Gaming-Mode).

## Steam: drei getrennte Datenquellen

1. `userdata/<id>/config/shortcuts.vdf` — die Shortcuts (Binär-VDF, `appid` =
   CRC32 über `exe + appname`). Unkritisch.
2. `userdata/<id>/config/grid/` — Artwork, benannt nach `<appid>p.png`,
   `<appid>_hero.png`, `<appid>_logo.png`, `<appid>.ico`. Unkritisch.
3. **Collections** liegen **nicht** in einer VDF, sondern in der Chromium-LevelDB
   unter `%LOCALAPPDATA%\Steam\htmlcache\Local Storage\leveldb`
   (Key `_https://steamloopback.host…-cloud-storage-namespace-1`). Undokumentiert,
   Steam muss dafür geschlossen sein, Valve ändert das Format ohne Vorwarnung.
   Das `tags`-Feld in `shortcuts.vdf` ist der Legacy-Weg und wird von der aktuellen
   Bibliothek nicht mehr verlässlich als Collection interpretiert.

**Konsequenz:** Shortcuts + Artwork + Sync/Cleanup sind der gut beherrschbare Teil.
Die Regale sind der dauerhafte Wartungsaufwand. Optionen: eigener LevelDB-Writer
(fragil) **oder** Live über Steams CEF-Debugging (`-cef-enable-debugging`,
WebSocket auf `localhost:8080`, `SteamClient.*`-APIs zur Laufzeit) — sauberer,
aber ebenfalls undokumentiert. Entscheidung offen; erst Shortcuts/Artwork
liefern.

## Emissionsmodi

Der Kern der Zweiteilung (`EmissionMode`):

- **Direct** — Shortcut zeigt direkt auf das Launch-Ziel (URI bevorzugt).
  Steam Input hookt den Prozess normal. Default für Epic/GOG/Ubisoft/EA.
- **SisrWrapper** — Shortcut zeigt auf das SteamButler-Wrapper-Binary. Default
  nur für Game Pass / UWP, weil diese den Steam-Overlay/Input-Hook nicht annehmen.

Die Zuordnung muss **hart** durchgezogen werden: Für ein Spiel, das Steam Input
akzeptiert, wäre der Wrapper schädlich (zwei Mapping-Ebenen von SISR und Steam
Input übereinander). Default nach Quelle **plus manueller Per-Game-Override** —
bei Ubisoft/EA hängt es am Einzelspiel, ob der Launcher-Handoff das Overlay
abreißen lässt. Keine Auto-Detection (Prozessbeobachtung zur Laufzeit, fehleranfällig).

## SISR-Integration — die korrigierte Kausalkette

Wichtig, weil es den Wrapper stark vereinfacht:

- SISR **startet Spiele nicht** (anders als GloSC/GlosSI). Input-Umleitung und
  Spielstart sind entkoppelt → Shortcuts müssen nur starten, nicht hooken.
- Die aktive Config hängt **nicht** am Overlay-Hook, sondern an einem
  Protokoll-Aufruf: SISR ruft `steam://forceinputappid/<AppID>` auf und hält die
  Steam-Input-Config dieses Shortcuts — unabhängig von Vordergrundfenster oder
  gestartetem Prozess.
- Der **Marker**-Shortcut ist genau **einer**, global (erster Non-Steam-Eintrag
  mit dem Marker-Argument). Für Per-Game-Configs **nicht** wiederverwenden.

**Daraus folgt der Wrapper-Ablauf** (kein Kindprozess-Trick, keine
Env-Vererbung, kein Nachstarten von SISR nötig):

1. Dauerhafte SISR-Tray-Instanz läuft im Hintergrund.
2. SteamButler legt pro Spiel einen SISR-Config-Träger-Shortcut **ohne** `--marker`
   an — er muss nur existieren und eine AppID haben, nicht gestartet werden.
3. Der Wrapper berechnet dessen AppID (CRC32 über `exe + appname`, deterministisch),
   ruft `steam://forceinputappid/<id>`, startet das Spiel, wartet auf dessen Ende.
4. Danach zurück auf die Marker-AppID.

Ein Bibliothekseintrag pro Spiel, eigene Config ohne zweiten sichtbaren Shortcut
(Config-Träger optional in eine ausgeblendete Collection schieben).

### Lifecycle-Sonderfall UWP/Game Pass

Der Spielprozess ist **kein Kind** des Wrappers (Start über `explorer.exe
shell:appsFolder\…`). Der Wrapper muss daher auf den Executable-Namen pollen —
den liefert `MicrosoftGame.config`. Genau deshalb funktioniert es bei UWP: kein
Kindprozess ⇒ keine geerbten `SteamAppId`/`SteamGameId`-Env-Variablen ⇒ Steam
versucht gar nicht erst zu hooken.

### Laufzeit-Mapping wechseln

- **Welche Config aktiv ist** → `forceinputappid`, ohne Overlay.
- **Zwischen Mappings im Spiel wechseln** → Action Sets / Action Set Layers
  (Chord/Modeshift), vorab modelliert, keine UI nötig.
- **Bindings tatsächlich bearbeiten** → braucht die Steam-Overlay-UI.

## Watcher-Architektur

Steam liest `shortcuts.vdf` nur beim Start und schreibt beim Beenden seinen
eigenen Stand zurück; ein Dienst, der live hineinschreibt, verliert seine
Änderungen. Zwei Wege:

- **A) Offline-Apply** (Default): Watcher überwacht Manifest-Ordner/Registry-Keys,
  hält ein Delta vor und schreibt erst, wenn Steam nicht läuft (bzw. beim
  Beobachten des Steam-Exits). Robust; sichtbar wird alles beim nächsten Start.
- **B) Live via CEF**: `-cef-enable-debugging`, WebSocket an `localhost:8080`,
  `SteamClient.*` zur Laufzeit. Sauberer für die „dynamische" Anmutung und für
  Collections, aber undokumentiert und ein Sicherheits-Tradeoff (offener Debug-Port).

## Launcher-Datenquellen (Windows)

| Launcher | Quelle | Start |
|----------|--------|-------|
| Epic | `%PROGRAMDATA%\Epic\EpicGamesLauncher\Data\Manifests\*.item` (JSON) | `com.epicgames.launcher://apps/<ns>%3A<catId>%3A<app>?action=launch&silent=true` |
| GOG | Registry `HKLM\…\WOW6432Node\GOG.com\Games\<id>`; alt. `galaxy-2.0.db` | exe direkt oder `goggalaxy://openGameView/<id>` |
| Ubisoft | Registry `HKLM\…\WOW6432Node\Ubisoft\Launcher\Installs\<id>` → `InstallDir` | `uplay://launch/<id>/0` |
| EA | Registry `HKLM\…\WOW6432Node\EA Games\<title>` + Ordner-Scan (unzuverlässig) | `link2ea://launchgame/<offerId>?platform=pc` |
| Game Pass | `C:\XboxGames\<Title>\Content\MicrosoftGame.config` (PFN + exe) | `explorer.exe shell:appsFolder\<PFN>!<AppId>` |

## Offene Vorbehalte

- `forceinputappid` ist ein undokumentierter Protokoll-Handler (Valve kann ändern).
- SISR ist selbst hochexperimentell (VIIPER/USBIP statt ViGEm) — bewegliche Basis.
- CEF-Debug-Port teilt sich SISR ggf. mit uns — fragile geteilte Ressource.
- Kern-Annahme (Wrapper-AppID-Config greift via `forceinputappid`) vor dem Ausbau
  praktisch verifizieren: Wrapper mit fixem Namen als Non-Steam-Game, auffällige
  Test-Config (z. B. A/B vertauscht), prüfen ob sie greift.

## Nächste Schritte

1. **`vdf::serialize`/`parse`** gegen echte `shortcuts.vdf`-Samples (Round-Trip-Tests).
2. **GOG-Scanner** (Registry) — zweiter realer Scanner, bestätigt das Muster.
3. **Artwork-Client** (SteamGridDB) + Grid-Datei-Benennung.
4. **Sync/Cleanup** (Offline-Apply gegen Steam-Exit).
5. **Wrapper praktisch verifizieren**, dann Game-Pass-Scanner + `wait_exe`-Polling.
6. **Collections** (Weg A LevelDB vs. Weg B CEF entscheiden).
7. **GUI** (`eframe`/`egui`).
