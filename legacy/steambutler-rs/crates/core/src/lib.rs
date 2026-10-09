//! # steambutler-core
//!
//! Platform-agnostic building blocks shared by every SteamButler frontend
//! (CLI, GUI) and by the wrapper binary.
//!
//! Nothing in this crate touches a specific launcher — that lives in
//! `steambutler-scanners`. Here we keep the pieces that are the same on
//! Windows, macOS and SteamOS:
//!
//! * [`appid`]    – deterministic Steam non-Steam shortcut AppIDs.
//! * [`model`]    – the domain model (`Game`, `LaunchTarget`, emission modes).
//! * [`vdf`]      – reading/writing Steam's binary `shortcuts.vdf`.
//! * [`steam`]    – locating the Steam install and per-user `userdata` dirs.
//! * [`shortcut`] – turning a `Game` into a Steam `Shortcut`.
//! * [`sync`]     – reconciling desired shortcuts with what's already there.

pub mod appid;
pub mod model;
pub mod shortcut;
pub mod steam;
pub mod sync;
pub mod vdf;
