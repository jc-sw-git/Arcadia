//! Xbox / Game Pass (UWP) scanner.
//!
//! Installed titles live under `C:\XboxGames\<Title>\Content\MicrosoftGame.config`,
//! which yields the `PackageFamilyName` and the primary `Executable`/AppId.
//! Launch is only possible via `explorer.exe shell:appsFolder\<PFN>!<AppId>`.
//!
//! These titles are exactly the ones that lose the Steam Input hook, so their
//! default emission is [`EmissionMode::SisrWrapper`]. The wrapper watches the
//! executable name (from `MicrosoftGame.config`) for lifecycle, since the game
//! runs in a protected process that is not our child.
//!
//! [`EmissionMode::SisrWrapper`]: steambutler_core::model::EmissionMode::SisrWrapper
//!
//! TODO: parse MicrosoftGame.config (XML) for PFN + executable, also honour the
//! per-user install root beyond the default `C:\XboxGames`.

use crate::Scanner;
use anyhow::Result;
use steambutler_core::model::{Game, LauncherSource};

pub struct GamePassScanner;

impl Scanner for GamePassScanner {
    fn source(&self) -> LauncherSource {
        LauncherSource::GamePass
    }

    fn scan(&self) -> Result<Vec<Game>> {
        // TODO: walk C:\XboxGames\*\Content\MicrosoftGame.config.
        Ok(Vec::new())
    }
}
