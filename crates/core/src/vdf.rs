//! Steam's binary `shortcuts.vdf`.
//!
//! The file is a single nested binary-VDF map `shortcuts` whose children are
//! `"0"`, `"1"`, … each describing one non-Steam shortcut. Binary VDF tokens:
//!
//! ```text
//! 0x00 <key\0> ...children... 0x08   nested map
//! 0x01 <key\0> <value\0>              UTF-8 string
//! 0x02 <key\0> <i32 little-endian>    32-bit int
//! 0x08                                end-of-map
//! ```
//!
//! Per-shortcut fields Steam writes today: `appid` (i32), `AppName`, `Exe`,
//! `StartDir`, `icon`, `ShortcutPath`, `LaunchOptions`, `IsHidden`,
//! `AllowDesktopConfig`, `AllowOverlay`, `OpenVR`, `Devkit`, `DevkitGameID`,
//! `DevkitOverrideAppID`, `LastPlayTime`, `FlatpakAppID`, and a nested `tags`
//! map (`"0"` -> tag, …).
//!
//! Reading and writing are implemented against captured real-world samples, not
//! by guessing the field set — that's tracked as the first core task. Until
//! then these entry points return [`VdfError::NotImplemented`] so callers wire
//! up against a stable API.

use serde::{Deserialize, Serialize};
use std::path::Path;

/// One non-Steam shortcut, mirroring the fields Steam persists.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, Default)]
pub struct Shortcut {
    pub app_id: i32,
    pub app_name: String,
    /// Executable, wrapped in double quotes exactly as Steam stores it.
    pub exe: String,
    pub start_dir: String,
    pub icon: String,
    pub shortcut_path: String,
    pub launch_options: String,
    pub is_hidden: bool,
    pub allow_desktop_config: bool,
    pub allow_overlay: bool,
    pub open_vr: bool,
    pub last_play_time: i32,
    /// Legacy collection hints. The current Steam library ignores these for
    /// shelving (collections live in the CEF LevelDB), but Steam still round-trips them.
    pub tags: Vec<String>,
}

#[derive(Debug, thiserror::Error)]
pub enum VdfError {
    #[error("shortcuts.vdf I/O error")]
    Io(#[from] std::io::Error),
    #[error("malformed binary VDF: {0}")]
    Malformed(String),
    #[error("binary VDF {0} not implemented yet")]
    NotImplemented(&'static str),
}

/// Parse a `shortcuts.vdf` byte buffer into shortcuts.
pub fn parse(_bytes: &[u8]) -> Result<Vec<Shortcut>, VdfError> {
    Err(VdfError::NotImplemented("parse"))
}

/// Serialize shortcuts back into `shortcuts.vdf` bytes.
pub fn serialize(_shortcuts: &[Shortcut]) -> Result<Vec<u8>, VdfError> {
    Err(VdfError::NotImplemented("serialize"))
}

/// Read and parse `shortcuts.vdf` from disk. A missing file yields an empty list.
pub fn read(path: &Path) -> Result<Vec<Shortcut>, VdfError> {
    match std::fs::read(path) {
        Ok(bytes) => parse(&bytes),
        Err(e) if e.kind() == std::io::ErrorKind::NotFound => Ok(Vec::new()),
        Err(e) => Err(VdfError::Io(e)),
    }
}
