//! Mapping a scanned [`Game`] to a Steam [`Shortcut`].
//!
//! Every shortcut SteamButler creates carries [`MANAGED_TAG`] so the sync step
//! can tell its own entries apart from ones the user (or another tool) added,
//! and only ever removes its own.

use crate::appid;
use crate::model::{EmissionMode, Game, LaunchTarget};
use crate::vdf::Shortcut;

/// Tag stamped on every SteamButler-managed shortcut.
pub const MANAGED_TAG: &str = "SteamButler";

#[derive(Debug, thiserror::Error)]
pub enum BuildError {
    #[error("emission mode {0:?} is not supported yet (needs the SISR wrapper)")]
    UnsupportedEmission(EmissionMode),
}

/// Build a Steam shortcut for a game, using the source's default emission mode.
pub fn build_shortcut(game: &Game) -> Result<Shortcut, BuildError> {
    match game.source.default_emission() {
        EmissionMode::Direct => Ok(build_direct(game)),
        mode => Err(BuildError::UnsupportedEmission(mode)),
    }
}

fn build_direct(game: &Game) -> Shortcut {
    let (exe, launch_options, start_dir) = match &game.launch {
        LaunchTarget::Executable {
            path,
            args,
            start_dir,
        } => (
            quote(path),
            args.join(" "),
            start_dir.clone().unwrap_or_else(|| parent_dir(path)),
        ),
        LaunchTarget::Uri(uri) => (
            quote(uri),
            String::new(),
            game.install_dir.clone().unwrap_or_default(),
        ),
        LaunchTarget::ShellAppsFolder(app) => (
            quote("explorer.exe"),
            format!("shell:appsFolder\\{app}"),
            String::new(),
        ),
    };

    Shortcut {
        // Steam recomputes this on save, but we set it so artwork can be named
        // and the entry matched deterministically before Steam ever sees it.
        // Stored as i32: the top bit is always set, so the value is negative.
        app_id: appid::shortcut_app_id(&exe, &game.name) as i32,
        app_name: game.name.clone(),
        exe,
        start_dir,
        launch_options,
        allow_desktop_config: true,
        allow_overlay: true,
        tags: vec![MANAGED_TAG.to_owned(), game.source.label().to_owned()],
        ..Default::default()
    }
}

/// Wrap in double quotes unless already quoted (Steam stores `Exe` quoted).
fn quote(s: &str) -> String {
    if s.starts_with('"') {
        s.to_owned()
    } else {
        format!("\"{s}\"")
    }
}

fn parent_dir(path: &str) -> String {
    std::path::Path::new(path)
        .parent()
        .map(|p| p.display().to_string())
        .unwrap_or_default()
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::model::LauncherSource;

    fn epic_game() -> Game {
        Game {
            id: "Fake".into(),
            name: "Loop Hero".into(),
            source: LauncherSource::Epic,
            launch: LaunchTarget::Uri("com.epicgames.launcher://apps/x?action=launch".into()),
            install_dir: Some("C:\\Games\\LoopHero".into()),
            installed: true,
        }
    }

    #[test]
    fn direct_uri_shortcut_is_tagged_and_quoted() {
        let sc = build_shortcut(&epic_game()).unwrap();
        assert_eq!(sc.app_name, "Loop Hero");
        assert!(sc.exe.starts_with('"') && sc.exe.ends_with('"'));
        assert!(sc.tags.contains(&MANAGED_TAG.to_string()));
        assert!(sc.tags.contains(&"Epic Games".to_string()));
        assert_eq!(sc.start_dir, "C:\\Games\\LoopHero");
        assert_ne!(sc.app_id, 0);
    }

    #[test]
    fn gamepass_emission_unsupported_for_now() {
        let mut g = epic_game();
        g.source = LauncherSource::GamePass;
        g.launch = LaunchTarget::ShellAppsFolder("Pub.Game_abc!App".into());
        assert!(matches!(
            build_shortcut(&g),
            Err(BuildError::UnsupportedEmission(EmissionMode::SisrWrapper))
        ));
    }
}
