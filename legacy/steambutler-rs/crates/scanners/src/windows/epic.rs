//! Epic Games Launcher scanner — the simplest, fully data-driven case.
//!
//! Epic writes one JSON manifest per installed game under
//! `%PROGRAMDATA%\Epic\EpicGamesLauncher\Data\Manifests\*.item`. We read the
//! `DisplayName`, `InstallLocation`, `LaunchExecutable` and catalog identifiers
//! and emit a launch URI so SISR (which only redirects input, never launches)
//! can stay out of the way:
//!
//! `com.epicgames.launcher://apps/<ns>%3A<catalogId>%3A<appName>?action=launch&silent=true`

use crate::Scanner;
use anyhow::{Context, Result};
use std::path::PathBuf;
use steambutler_core::model::{Game, LaunchTarget, LauncherSource};

pub struct EpicScanner;

impl EpicScanner {
    fn manifest_dir() -> Option<PathBuf> {
        let program_data = std::env::var_os("PROGRAMDATA")?;
        Some(
            PathBuf::from(program_data)
                .join("Epic")
                .join("EpicGamesLauncher")
                .join("Data")
                .join("Manifests"),
        )
    }

    fn parse_manifest(bytes: &[u8]) -> Option<Game> {
        let v: serde_json::Value = serde_json::from_slice(bytes).ok()?;

        let name = v.get("DisplayName")?.as_str()?.to_owned();
        let app_name = v.get("AppName")?.as_str()?.to_owned();
        let namespace = v
            .get("CatalogNamespace")
            .and_then(|x| x.as_str())
            .unwrap_or_default();
        let catalog_id = v
            .get("CatalogItemId")
            .and_then(|x| x.as_str())
            .unwrap_or_default();
        let install_location = v
            .get("InstallLocation")
            .and_then(|x| x.as_str())
            .map(str::to_owned);

        let uri = format!(
            "com.epicgames.launcher://apps/{namespace}%3A{catalog_id}%3A{app_name}?action=launch&silent=true"
        );

        let installed = install_location
            .as_deref()
            .map(|p| std::path::Path::new(p).is_dir())
            .unwrap_or(false);

        Some(Game {
            id: app_name,
            name,
            source: LauncherSource::Epic,
            launch: LaunchTarget::Uri(uri),
            install_dir: install_location,
            installed,
        })
    }
}

impl Scanner for EpicScanner {
    fn source(&self) -> LauncherSource {
        LauncherSource::Epic
    }

    fn scan(&self) -> Result<Vec<Game>> {
        let Some(dir) = Self::manifest_dir() else {
            return Ok(Vec::new());
        };
        let entries = match std::fs::read_dir(&dir) {
            Ok(e) => e,
            // Launcher not installed — not an error.
            Err(e) if e.kind() == std::io::ErrorKind::NotFound => return Ok(Vec::new()),
            Err(e) => return Err(e).with_context(|| format!("reading {}", dir.display())),
        };

        let mut games = Vec::new();
        for entry in entries.filter_map(std::result::Result::ok) {
            let path = entry.path();
            if path.extension().is_some_and(|e| e == "item") {
                if let Ok(bytes) = std::fs::read(&path) {
                    if let Some(game) = Self::parse_manifest(&bytes) {
                        games.push(game);
                    }
                }
            }
        }
        Ok(games)
    }
}
