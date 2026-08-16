//! GOG Galaxy scanner.
//!
//! Two data sources, cheapest first:
//! * Registry `HKLM\SOFTWARE\WOW6432Node\GOG.com\Games\<id>` -> `path`, `exe`,
//!   `gameName`. Robust and simple; GOG games launch cleanly from the exe.
//! * SQLite `C:\ProgramData\GOG.com\Galaxy\storage\galaxy-2.0.db` (joins over
//!   `LimitedDetails` / `InstalledBaseProducts`) for richer metadata.
//!
//! TODO: implement the registry path first (no extra deps), fall back to the DB.

use crate::Scanner;
use anyhow::Result;
use steambutler_core::model::{Game, LauncherSource};

pub struct GogScanner;

impl Scanner for GogScanner {
    fn source(&self) -> LauncherSource {
        LauncherSource::Gog
    }

    fn scan(&self) -> Result<Vec<Game>> {
        // TODO: read HKLM\SOFTWARE\WOW6432Node\GOG.com\Games\*.
        Ok(Vec::new())
    }
}
