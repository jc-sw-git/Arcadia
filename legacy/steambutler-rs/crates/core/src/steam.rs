//! Locating the Steam installation and its per-user `userdata` directories.
//!
//! Path discovery is one of the few genuinely cross-platform concerns, so it
//! lives here rather than in a platform scanner.

use std::path::PathBuf;

/// Return the Steam root directory (the folder containing `steamapps`,
/// `userdata`, `config`), or `None` if Steam can't be located.
pub fn steam_root() -> Option<PathBuf> {
    steam_root_candidates().into_iter().find(|p| p.is_dir())
}

/// All plausible Steam roots for the current OS, most-likely first. Exposed so
/// the UI can offer a manual pick when auto-detection fails.
pub fn steam_root_candidates() -> Vec<PathBuf> {
    let mut out = Vec::new();

    #[cfg(target_os = "windows")]
    {
        // TODO: prefer HKCU\Software\Valve\Steam\SteamPath from the registry
        // before falling back to these defaults.
        for base in ["ProgramFiles(x86)", "ProgramFiles", "ProgramW6432"] {
            if let Ok(dir) = std::env::var(base) {
                out.push(PathBuf::from(dir).join("Steam"));
            }
        }
    }

    #[cfg(target_os = "macos")]
    {
        if let Some(home) = dirs::home_dir() {
            out.push(home.join("Library/Application Support/Steam"));
        }
    }

    #[cfg(target_os = "linux")]
    {
        if let Some(home) = dirs::home_dir() {
            // Native, plus the common SteamOS / Flatpak locations.
            out.push(home.join(".local/share/Steam"));
            out.push(home.join(".steam/steam"));
            out.push(home.join(".var/app/com.valvesoftware.Steam/data/Steam"));
        }
    }

    out
}

/// Enumerate `userdata/<id>` directories under a Steam root. Each corresponds
/// to a logged-in Steam account; `shortcuts.vdf` and `grid/` live inside.
pub fn userdata_dirs(steam_root: &std::path::Path) -> Vec<PathBuf> {
    let userdata = steam_root.join("userdata");
    let Ok(entries) = std::fs::read_dir(&userdata) else {
        return Vec::new();
    };
    entries
        .filter_map(Result::ok)
        .map(|e| e.path())
        .filter(|p| p.is_dir() && p.file_name().is_some_and(|n| n != "0"))
        .collect()
}

/// Path to a user's `config/shortcuts.vdf` (may not exist yet).
pub fn shortcuts_vdf(userdata_dir: &std::path::Path) -> PathBuf {
    userdata_dir.join("config").join("shortcuts.vdf")
}

/// Path to a user's `config/grid` artwork directory (may not exist yet).
pub fn grid_dir(userdata_dir: &std::path::Path) -> PathBuf {
    userdata_dir.join("config").join("grid")
}

/// Best-effort check whether Steam is currently running. Writing `shortcuts.vdf`
/// while Steam runs loses the change (Steam rewrites the file on exit), so the
/// apply step refuses when this returns `true`.
pub fn is_running() -> bool {
    #[cfg(target_os = "windows")]
    {
        std::process::Command::new("tasklist")
            .args(["/FI", "IMAGENAME eq steam.exe", "/NH"])
            .output()
            .map(|o| {
                String::from_utf8_lossy(&o.stdout)
                    .to_lowercase()
                    .contains("steam.exe")
            })
            .unwrap_or(false)
    }
    #[cfg(not(target_os = "windows"))]
    {
        std::process::Command::new("pgrep")
            .args(["-x", "steam"])
            .output()
            .map(|o| o.status.success())
            .unwrap_or(false)
    }
}
