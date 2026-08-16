//! Deterministic AppIDs for non-Steam shortcuts.
//!
//! Steam derives the identity of a non-Steam shortcut from a CRC32 over the
//! executable string concatenated with the shortcut's display name. The same
//! value names the artwork files under `userdata/<id>/config/grid/` and drives
//! `steam://rungameid/<id>`.
//!
//! The `exe` argument must be **byte-identical** to what ends up in
//! `shortcuts.vdf` (Steam stores the exe wrapped in double quotes), otherwise
//! the computed id won't match Steam's own and artwork lookups miss.

/// 32-bit shortcut AppID, as stored in `shortcuts.vdf` and used to name grid
/// artwork (`<appid>p.png`, `<appid>_hero.png`, `<appid>_logo.png`, `<appid>.ico`).
pub fn shortcut_app_id(exe: &str, app_name: &str) -> u32 {
    let mut hasher = crc32fast::Hasher::new();
    hasher.update(exe.as_bytes());
    hasher.update(app_name.as_bytes());
    hasher.finalize() | 0x8000_0000
}

/// 64-bit "gameid" used by the `steam://rungameid/<id>` protocol handler for
/// non-Steam shortcuts.
pub fn rungameid(app_id: u32) -> u64 {
    ((app_id as u64) << 32) | 0x0200_0000
}

/// Legacy signed 32-bit id (`appid - 2^32`) that some older Steam data files
/// and third-party tools expect. Provided for interop when reading foreign data.
pub fn legacy_signed_id(app_id: u32) -> i32 {
    app_id as i32
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn top_bit_is_always_set() {
        let id = shortcut_app_id("\"C:\\Games\\game.exe\"", "My Game");
        assert_eq!(id & 0x8000_0000, 0x8000_0000);
    }

    #[test]
    fn is_deterministic() {
        let a = shortcut_app_id("\"game.exe\"", "Title");
        let b = shortcut_app_id("\"game.exe\"", "Title");
        assert_eq!(a, b);
    }

    #[test]
    fn rungameid_layout() {
        let app = shortcut_app_id("\"game.exe\"", "Title");
        let rgid = rungameid(app);
        assert_eq!(rgid & 0xFFFF_FFFF, 0x0200_0000);
        assert_eq!((rgid >> 32) as u32, app);
    }
}
