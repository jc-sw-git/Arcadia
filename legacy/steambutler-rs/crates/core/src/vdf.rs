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
//! Per-shortcut fields Steam writes today, in order: `appid` (i32), `AppName`,
//! `Exe` (quoted), `StartDir`, `icon`, `ShortcutPath`, `LaunchOptions`,
//! `IsHidden`, `AllowDesktopConfig`, `AllowOverlay`, `openvr`, `Devkit`,
//! `DevkitGameID`, `DevkitOverrideAppID`, `LastPlayTime`, `FlatpakAppID`,
//! `sortas`, and a nested `tags` map (`"0"` -> tag, …).
//!
//! Two layers live here:
//! * [`VdfValue`] + [`parse_binary`]/[`write_binary`] — an order-preserving
//!   generic codec. Round-trips any binary VDF byte-for-byte, so a future
//!   "apply" step can edit a shortcut while preserving fields we don't model.
//! * [`Shortcut`] + [`parse`]/[`serialize`] — the typed view used by the rest
//!   of the app.

use serde::{Deserialize, Serialize};
use std::path::Path;

const TYPE_MAP: u8 = 0x00;
const TYPE_STR: u8 = 0x01;
const TYPE_INT: u8 = 0x02;
const TYPE_END: u8 = 0x08;

#[derive(Debug, thiserror::Error)]
pub enum VdfError {
    #[error("shortcuts.vdf I/O error")]
    Io(#[from] std::io::Error),
    #[error("malformed binary VDF: {0}")]
    Malformed(String),
}

// ---------------------------------------------------------------------------
// Generic order-preserving binary VDF
// ---------------------------------------------------------------------------

/// A binary-VDF value. Maps preserve key order, which matters for byte-exact
/// round-trips against Steam's own output.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum VdfValue {
    Map(Vec<(String, VdfValue)>),
    Str(String),
    Int(i32),
}

impl VdfValue {
    /// Borrow the entries if this is a map.
    pub fn as_map(&self) -> Option<&[(String, VdfValue)]> {
        match self {
            VdfValue::Map(m) => Some(m),
            _ => None,
        }
    }

    /// Look up a child by key (maps only).
    pub fn get(&self, key: &str) -> Option<&VdfValue> {
        self.as_map()?
            .iter()
            .find(|(k, _)| k == key)
            .map(|(_, v)| v)
    }

    /// Borrow the string, if this is a string.
    pub fn as_str(&self) -> Option<&str> {
        match self {
            VdfValue::Str(s) => Some(s),
            _ => None,
        }
    }

    /// Copy the int, if this is an int.
    pub fn as_int(&self) -> Option<i32> {
        match self {
            VdfValue::Int(i) => Some(*i),
            _ => None,
        }
    }
}

struct Reader<'a> {
    buf: &'a [u8],
    pos: usize,
}

impl<'a> Reader<'a> {
    fn byte(&mut self) -> Result<u8, VdfError> {
        let b = *self
            .buf
            .get(self.pos)
            .ok_or_else(|| VdfError::Malformed(format!("unexpected EOF at {}", self.pos)))?;
        self.pos += 1;
        Ok(b)
    }

    fn cstr(&mut self) -> Result<String, VdfError> {
        let start = self.pos;
        let end = self.buf[start..]
            .iter()
            .position(|&b| b == 0)
            .map(|off| start + off)
            .ok_or_else(|| VdfError::Malformed(format!("unterminated string at {start}")))?;
        let s = std::str::from_utf8(&self.buf[start..end])
            .map_err(|e| VdfError::Malformed(format!("invalid UTF-8 at {start}: {e}")))?
            .to_owned();
        self.pos = end + 1; // skip the NUL
        Ok(s)
    }

    fn i32(&mut self) -> Result<i32, VdfError> {
        let bytes = self
            .buf
            .get(self.pos..self.pos + 4)
            .ok_or_else(|| VdfError::Malformed(format!("truncated i32 at {}", self.pos)))?;
        self.pos += 4;
        Ok(i32::from_le_bytes(bytes.try_into().unwrap()))
    }

    fn map(&mut self) -> Result<VdfValue, VdfError> {
        let mut pairs = Vec::new();
        loop {
            let ty = self.byte()?;
            if ty == TYPE_END {
                break;
            }
            let key = self.cstr()?;
            let value = match ty {
                TYPE_MAP => self.map()?,
                TYPE_STR => VdfValue::Str(self.cstr()?),
                TYPE_INT => VdfValue::Int(self.i32()?),
                other => {
                    return Err(VdfError::Malformed(format!(
                        "unsupported type byte 0x{other:02x} at {}",
                        self.pos - 1
                    )))
                }
            };
            pairs.push((key, value));
        }
        Ok(VdfValue::Map(pairs))
    }
}

/// Parse a binary-VDF buffer into a value tree. The top level is a map.
pub fn parse_binary(bytes: &[u8]) -> Result<VdfValue, VdfError> {
    Reader { buf: bytes, pos: 0 }.map()
}

/// Serialize a binary-VDF value tree. The root must be a map.
pub fn write_binary(root: &VdfValue) -> Vec<u8> {
    let mut out = Vec::new();
    match root {
        VdfValue::Map(pairs) => write_map(pairs, &mut out),
        _ => out.push(TYPE_END),
    }
    out
}

fn write_map(pairs: &[(String, VdfValue)], out: &mut Vec<u8>) {
    for (key, value) in pairs {
        match value {
            VdfValue::Map(children) => {
                out.push(TYPE_MAP);
                write_cstr(key, out);
                write_map(children, out);
            }
            VdfValue::Str(s) => {
                out.push(TYPE_STR);
                write_cstr(key, out);
                write_cstr(s, out);
            }
            VdfValue::Int(i) => {
                out.push(TYPE_INT);
                write_cstr(key, out);
                out.extend_from_slice(&i.to_le_bytes());
            }
        }
    }
    out.push(TYPE_END);
}

fn write_cstr(s: &str, out: &mut Vec<u8>) {
    out.extend_from_slice(s.as_bytes());
    out.push(0);
}

// ---------------------------------------------------------------------------
// Typed shortcut view
// ---------------------------------------------------------------------------

/// One non-Steam shortcut, mirroring the fields Steam persists (in write order).
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
    pub devkit: bool,
    pub devkit_game_id: String,
    pub devkit_override_app_id: i32,
    pub last_play_time: i32,
    pub flatpak_app_id: String,
    /// Optional "sort as" name Steam sorts the entry under.
    pub sort_as: String,
    /// Legacy collection hints. The current Steam library ignores these for
    /// shelving (collections live in the CEF LevelDB), but Steam round-trips them.
    pub tags: Vec<String>,
}

impl Shortcut {
    fn to_vdf(&self) -> VdfValue {
        let b = |v: bool| VdfValue::Int(i32::from(v));
        let s = |v: &str| VdfValue::Str(v.to_owned());
        let tags = self
            .tags
            .iter()
            .enumerate()
            .map(|(i, t)| (i.to_string(), VdfValue::Str(t.clone())))
            .collect();

        VdfValue::Map(vec![
            ("appid".into(), VdfValue::Int(self.app_id)),
            ("AppName".into(), s(&self.app_name)),
            ("Exe".into(), s(&self.exe)),
            ("StartDir".into(), s(&self.start_dir)),
            ("icon".into(), s(&self.icon)),
            ("ShortcutPath".into(), s(&self.shortcut_path)),
            ("LaunchOptions".into(), s(&self.launch_options)),
            ("IsHidden".into(), b(self.is_hidden)),
            ("AllowDesktopConfig".into(), b(self.allow_desktop_config)),
            ("AllowOverlay".into(), b(self.allow_overlay)),
            ("openvr".into(), b(self.open_vr)),
            ("Devkit".into(), b(self.devkit)),
            ("DevkitGameID".into(), s(&self.devkit_game_id)),
            (
                "DevkitOverrideAppID".into(),
                VdfValue::Int(self.devkit_override_app_id),
            ),
            ("LastPlayTime".into(), VdfValue::Int(self.last_play_time)),
            ("FlatpakAppID".into(), s(&self.flatpak_app_id)),
            ("sortas".into(), s(&self.sort_as)),
            ("tags".into(), VdfValue::Map(tags)),
        ])
    }

    fn from_vdf(v: &VdfValue) -> Shortcut {
        let tags = v
            .get("tags")
            .and_then(VdfValue::as_map)
            .map(|m| {
                m.iter()
                    .filter_map(|(_, val)| val.as_str().map(str::to_owned))
                    .collect()
            })
            .unwrap_or_default();

        Shortcut {
            app_id: get_int(v, "appid"),
            app_name: get_str(v, "AppName"),
            exe: get_str(v, "Exe"),
            start_dir: get_str(v, "StartDir"),
            icon: get_str(v, "icon"),
            shortcut_path: get_str(v, "ShortcutPath"),
            launch_options: get_str(v, "LaunchOptions"),
            is_hidden: get_bool(v, "IsHidden"),
            allow_desktop_config: get_bool(v, "AllowDesktopConfig"),
            allow_overlay: get_bool(v, "AllowOverlay"),
            open_vr: get_bool(v, "openvr"),
            devkit: get_bool(v, "Devkit"),
            devkit_game_id: get_str(v, "DevkitGameID"),
            devkit_override_app_id: get_int(v, "DevkitOverrideAppID"),
            last_play_time: get_int(v, "LastPlayTime"),
            flatpak_app_id: get_str(v, "FlatpakAppID"),
            sort_as: get_str(v, "sortas"),
            tags,
        }
    }
}

fn get_str(v: &VdfValue, key: &str) -> String {
    v.get(key)
        .and_then(VdfValue::as_str)
        .unwrap_or_default()
        .to_owned()
}

fn get_int(v: &VdfValue, key: &str) -> i32 {
    v.get(key).and_then(VdfValue::as_int).unwrap_or_default()
}

fn get_bool(v: &VdfValue, key: &str) -> bool {
    get_int(v, key) != 0
}

/// Parse a `shortcuts.vdf` byte buffer into shortcuts.
pub fn parse(bytes: &[u8]) -> Result<Vec<Shortcut>, VdfError> {
    let root = parse_binary(bytes)?;
    let shortcuts = root
        .get("shortcuts")
        .and_then(VdfValue::as_map)
        .ok_or_else(|| VdfError::Malformed("missing top-level 'shortcuts' map".into()))?;
    Ok(shortcuts
        .iter()
        .map(|(_, entry)| Shortcut::from_vdf(entry))
        .collect())
}

/// Serialize shortcuts into `shortcuts.vdf` bytes (indices assigned in order).
pub fn serialize(shortcuts: &[Shortcut]) -> Vec<u8> {
    let entries = shortcuts
        .iter()
        .enumerate()
        .map(|(i, sc)| (i.to_string(), sc.to_vdf()))
        .collect();
    let root = VdfValue::Map(vec![("shortcuts".into(), VdfValue::Map(entries))]);
    write_binary(&root)
}

/// Read and parse `shortcuts.vdf` from disk. A missing file yields an empty list.
pub fn read(path: &Path) -> Result<Vec<Shortcut>, VdfError> {
    match std::fs::read(path) {
        Ok(bytes) => parse(&bytes),
        Err(e) if e.kind() == std::io::ErrorKind::NotFound => Ok(Vec::new()),
        Err(e) => Err(VdfError::Io(e)),
    }
}

/// Write shortcuts to `shortcuts.vdf` on disk, creating parent dirs as needed.
pub fn write(path: &Path, shortcuts: &[Shortcut]) -> Result<(), VdfError> {
    if let Some(parent) = path.parent() {
        std::fs::create_dir_all(parent)?;
    }
    std::fs::write(path, serialize(shortcuts))?;
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    fn sample() -> Shortcut {
        Shortcut {
            app_id: -1207728364, // 0xB8057314 as i32
            app_name: "SISR Marker".into(),
            exe: "\"C:/Users/x/AppData/Local/SISR/SISR.exe\"".into(),
            start_dir: "C:\\Users\\x\\AppData\\Local\\SISR\\".into(),
            launch_options: "--marker".into(),
            allow_desktop_config: true,
            allow_overlay: true,
            last_play_time: 0x6A6B_2A54,
            tags: vec!["Epic Games".into()],
            ..Default::default()
        }
    }

    #[test]
    fn typed_roundtrip() {
        let shortcuts = vec![sample()];
        let bytes = serialize(&shortcuts);
        assert_eq!(parse(&bytes).unwrap(), shortcuts);
    }

    #[test]
    fn generic_roundtrip_is_byte_exact() {
        let bytes = serialize(&[sample()]);
        let tree = parse_binary(&bytes).unwrap();
        assert_eq!(write_binary(&tree), bytes);
    }

    #[test]
    fn empty_has_shortcuts_map() {
        let bytes = serialize(&[]);
        assert_eq!(bytes, b"\x00shortcuts\x00\x08\x08");
        assert!(parse(&bytes).unwrap().is_empty());
    }

    #[test]
    fn parses_hand_built_bytes() {
        // shortcuts -> "0" -> { appid=2, AppName="A", tags={} }
        let bytes = b"\x00shortcuts\x00\x000\x00\x02appid\x00\x02\x00\x00\x00\x01AppName\x00A\x00\x00tags\x00\x08\x08\x08\x08";
        let sc = parse(bytes).unwrap();
        assert_eq!(sc.len(), 1);
        assert_eq!(sc[0].app_id, 2);
        assert_eq!(sc[0].app_name, "A");
    }

    #[test]
    fn tags_are_indexed_map() {
        let mut sc = sample();
        sc.tags = vec!["A".into(), "B".into()];
        let round = parse(&serialize(&[sc.clone()])).unwrap();
        assert_eq!(round[0].tags, vec!["A".to_string(), "B".to_string()]);
    }

    /// Validates our typed model reproduces Steam's own bytes exactly. Run with
    /// `STEAMBUTLER_VDF_SAMPLE=<path> cargo test -- --ignored real_file`.
    #[test]
    #[ignore = "needs a real shortcuts.vdf via STEAMBUTLER_VDF_SAMPLE"]
    fn real_file_byte_exact() {
        let path = std::env::var("STEAMBUTLER_VDF_SAMPLE")
            .expect("set STEAMBUTLER_VDF_SAMPLE to a shortcuts.vdf path");
        let bytes = std::fs::read(&path).unwrap();
        let parsed = parse(&bytes).unwrap();
        assert_eq!(
            serialize(&parsed),
            bytes,
            "round-trip of {path} was not byte-identical"
        );
    }
}
