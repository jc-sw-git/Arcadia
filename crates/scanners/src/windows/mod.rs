//! Windows launcher scanners.

use crate::Scanner;

mod ea;
mod epic;
mod gamepass;
mod gog;
mod ubisoft;

pub use ea::EaScanner;
pub use epic::EpicScanner;
pub use gamepass::GamePassScanner;
pub use gog::GogScanner;
pub use ubisoft::UbisoftScanner;

/// Every Windows scanner instance.
pub fn scanners() -> Vec<Box<dyn Scanner>> {
    vec![
        Box::new(EpicScanner),
        Box::new(GogScanner),
        Box::new(UbisoftScanner),
        Box::new(EaScanner),
        Box::new(GamePassScanner),
    ]
}
