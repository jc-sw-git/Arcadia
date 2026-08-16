//! EA (Origin / EA app) scanner — the least reliable source.
//!
//! Origin exposed readable `.mfst` files under
//! `%PROGRAMDATA%\Origin\LocalContent`; the EA app keeps its catalog encrypted
//! under `%PROGRAMDATA%\EA Desktop`. The practical path is registry
//! `HKLM\SOFTWARE\WOW6432Node\EA Games\<title>` plus an install-folder scan.
//! Launch via `link2ea://launchgame/<offerId>?platform=pc`.
//!
//! TODO: expect per-game special cases; start from the registry titles.

use crate::Scanner;
use anyhow::Result;
use steambutler_core::model::{Game, LauncherSource};

pub struct EaScanner;

impl Scanner for EaScanner {
    fn source(&self) -> LauncherSource {
        LauncherSource::Ea
    }

    fn scan(&self) -> Result<Vec<Game>> {
        // TODO: read HKLM\SOFTWARE\WOW6432Node\EA Games\* and resolve offer ids.
        Ok(Vec::new())
    }
}
