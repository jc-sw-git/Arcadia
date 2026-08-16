//! Ubisoft Connect scanner.
//!
//! Registry `HKLM\SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs\<uplayId>`
//! -> `InstallDir`. Launch via `uplay://launch/<uplayId>/0` (a URI, not the exe:
//! a direct exe start re-spawns through `upc.exe`).
//!
//! TODO: enumerate the `Installs` subkeys and pair the ids with titles.

use crate::Scanner;
use anyhow::Result;
use steambutler_core::model::{Game, LauncherSource};

pub struct UbisoftScanner;

impl Scanner for UbisoftScanner {
    fn source(&self) -> LauncherSource {
        LauncherSource::Ubisoft
    }

    fn scan(&self) -> Result<Vec<Game>> {
        // TODO: read HKLM\SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs\*.
        Ok(Vec::new())
    }
}
