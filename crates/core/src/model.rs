//! The domain model shared across scanners, writers and the wrapper.

use serde::{Deserialize, Serialize};

/// Which launcher a game originates from. Drives the default emission mode and
/// the "shelf"/collection a shortcut is filed under.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
pub enum LauncherSource {
    Epic,
    Gog,
    Ubisoft,
    Ea,
    GamePass,
    /// Heroic / Lutris / Legendary on Linux, or anything not otherwise classified.
    Other,
}

impl LauncherSource {
    /// Human-readable label, also used as the default collection name.
    pub fn label(self) -> &'static str {
        match self {
            LauncherSource::Epic => "Epic Games",
            LauncherSource::Gog => "GOG Galaxy",
            LauncherSource::Ubisoft => "Ubisoft Connect",
            LauncherSource::Ea => "EA",
            LauncherSource::GamePass => "Xbox / Game Pass",
            LauncherSource::Other => "Other",
        }
    }

    /// Default emission mode per source.
    ///
    /// Only Game Pass / UWP titles reliably lose the Steam Input hook and need
    /// the SISR wrapper; everything else launches directly. This is a *default*
    /// — the UI must allow a per-game override, because whether an Ubisoft/EA
    /// title keeps the hook depends on the individual game (see docs/architecture.md).
    pub fn default_emission(self) -> EmissionMode {
        match self {
            LauncherSource::GamePass => EmissionMode::SisrWrapper,
            _ => EmissionMode::Direct,
        }
    }
}

/// How a shortcut is written into Steam.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum EmissionMode {
    /// Point the shortcut straight at the game's launch target. Steam Input
    /// hooks the process normally.
    Direct,
    /// Route through the SteamButler wrapper: it activates the game's per-game
    /// SISR config via `steam://forceinputappid`, launches the game, waits, and
    /// restores the marker config on exit. For titles that don't accept the
    /// Steam overlay/Input directly (Windows Store / Game Pass).
    SisrWrapper,
}

/// Where and how a game is actually started.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub enum LaunchTarget {
    /// A concrete executable with optional arguments and working directory.
    Executable {
        path: String,
        args: Vec<String>,
        start_dir: Option<String>,
    },
    /// A launcher protocol URI, e.g.
    /// `com.epicgames.launcher://apps/<ns>%3A<id>%3A<app>?action=launch&silent=true`,
    /// `uplay://launch/<id>/0`, `goggalaxy://openGameView/<id>`.
    Uri(String),
    /// A UWP app path, launched via `explorer.exe shell:appsFolder\<PFN>!<AppId>`.
    ShellAppsFolder(String),
}

/// A single game discovered by a scanner.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Game {
    /// Launcher-native identifier (Epic AppName, GOG product id, uPlay id, PFN…).
    pub id: String,
    pub name: String,
    pub source: LauncherSource,
    pub launch: LaunchTarget,
    /// Filesystem install directory, if known — used for the "still installed?"
    /// cleanup check.
    pub install_dir: Option<String>,
    /// Whether the game currently appears installed. Scanners set this; the
    /// sync step removes shortcuts for games that flipped to `false`.
    pub installed: bool,
}

impl Game {
    /// Stable key for deduplicating across scans (source + native id).
    pub fn key(&self) -> String {
        format!("{:?}:{}", self.source, self.id)
    }
}
