//! Launcher scanners.
//!
//! Every launcher backend implements [`Scanner`]. Backends are compiled per
//! target OS: the Windows launchers (Epic, GOG, Ubisoft, EA, Game Pass) only
//! exist on Windows; Linux/SteamOS exposes the Heroic/Lutris/Legendary world
//! instead. The shared frontends only ever see the trait.

use anyhow::Result;
use steambutler_core::model::{Game, LauncherSource};

/// A source of installed non-Steam games.
pub trait Scanner {
    /// Which launcher this scanner covers.
    fn source(&self) -> LauncherSource;
    /// Discover currently-known games. Returns an empty vec (not an error) when
    /// the launcher simply isn't installed.
    fn scan(&self) -> Result<Vec<Game>>;
}

/// All scanners available on the current platform.
pub fn all_scanners() -> Vec<Box<dyn Scanner>> {
    #[allow(unused_mut)]
    let mut scanners: Vec<Box<dyn Scanner>> = Vec::new();

    #[cfg(target_os = "windows")]
    scanners.extend(windows::scanners());

    #[cfg(target_os = "linux")]
    scanners.extend(linux::scanners());

    scanners
}

/// Run every available scanner and collect the games, skipping (but logging)
/// individual scanner failures so one broken launcher doesn't abort the sweep.
pub fn scan_all() -> Vec<Game> {
    let mut games = Vec::new();
    for scanner in all_scanners() {
        match scanner.scan() {
            Ok(mut found) => games.append(&mut found),
            Err(err) => eprintln!("[scanners] {:?} failed: {err:#}", scanner.source()),
        }
    }
    games
}

#[cfg(target_os = "windows")]
pub mod windows;

#[cfg(target_os = "linux")]
pub mod linux;
