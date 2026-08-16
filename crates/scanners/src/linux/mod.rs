//! Linux / SteamOS scanners.
//!
//! On the Deck and desktop Linux the non-Steam launcher world is different:
//! Heroic (Epic/GOG/Amazon), Legendary, Lutris, Bottles. This module is a
//! placeholder so the trait wiring and `cargo check` cover Linux from day one;
//! backends land once the Windows path is proven.

use crate::Scanner;

/// Every Linux scanner instance (none yet).
pub fn scanners() -> Vec<Box<dyn Scanner>> {
    // TODO: HeroicScanner (config under ~/.config/heroic), LutrisScanner, …
    Vec::new()
}
