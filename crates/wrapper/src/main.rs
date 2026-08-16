//! SteamButler launch wrapper.
//!
//! Shipped as a shortcut's target for games that lose the Steam Input hook
//! (Windows Store / Game Pass). It does **not** replace the game process — it
//! only steers which Steam Input configuration is active around the launch:
//!
//! 1. `steam://forceinputappid/<config-appid>` — switch to the game's own
//!    config-carrier shortcut. This holds regardless of foreground window, so
//!    no overlay hook on the game itself is required.
//! 2. Launch the game (URI, executable, or `shell:appsFolder\…` for UWP).
//! 3. Wait for the game to exit.
//! 4. `steam://forceinputappid/<marker-appid>` — restore the global marker
//!    config so subsequent launches aren't stuck on this game's mapping.
//!
//! A permanent SISR tray instance is assumed to be running; the wrapper never
//! starts SISR itself. See docs/architecture.md.

use anyhow::{Context, Result};
use clap::Parser;
use std::process::Command;

#[derive(Parser, Debug)]
#[command(name = "steambutler-wrapper", version, about)]
struct Args {
    /// AppID of the per-game SISR config-carrier shortcut to activate.
    #[arg(long)]
    config_appid: u32,

    /// AppID of the global marker shortcut to restore on exit.
    #[arg(long)]
    marker_appid: Option<u32>,

    /// How to launch the game: a `steam://`/`uplay://`/`com.epicgames…` URI,
    /// a `shell:appsFolder\…` string, or a plain executable path.
    #[arg(long)]
    launch: String,

    /// Arguments passed to an executable launch target.
    #[arg(long = "arg", value_name = "ARG")]
    args: Vec<String>,

    /// For UWP/Game Pass: executable name to poll for lifecycle, since the game
    /// is not our child process. Without it we return as soon as launch is issued.
    #[arg(long)]
    wait_exe: Option<String>,
}

fn main() -> Result<()> {
    let args = Args::parse();

    force_input_appid(args.config_appid).context("activating per-game input config")?;

    let restore = || {
        if let Some(marker) = args.marker_appid {
            let _ = force_input_appid(marker);
        }
    };

    // Ensure we always attempt to restore the marker, even on launch failure.
    let launch_result = launch_and_wait(&args);
    restore();
    launch_result
}

/// Invoke the (undocumented) `steam://forceinputappid/<id>` protocol handler.
fn force_input_appid(app_id: u32) -> Result<()> {
    open_url(&format!("steam://forceinputappid/{app_id}"))
}

fn launch_and_wait(args: &Args) -> Result<()> {
    let target = &args.launch;

    if is_uri(target) {
        open_url(target).with_context(|| format!("launching {target}"))?;
        wait_for_exit(args)?;
    } else {
        // Plain executable path.
        let mut cmd = Command::new(target);
        cmd.args(&args.args);
        let mut child = cmd.spawn().with_context(|| format!("spawning {target}"))?;
        if args.wait_exe.is_some() {
            wait_for_exit(args)?;
        } else {
            child.wait().context("waiting for game process")?;
        }
    }
    Ok(())
}

/// Wait for the game to end. For URI/UWP launches the game isn't our child, so
/// we poll for `--wait-exe` by name.
fn wait_for_exit(args: &Args) -> Result<()> {
    match &args.wait_exe {
        Some(_exe) => {
            // TODO: poll the process list (sysinfo / tasklist on Windows) until
            // `_exe` disappears, then return. Until implemented, don't hang.
            Ok(())
        }
        None => Ok(()),
    }
}

fn is_uri(s: &str) -> bool {
    // scheme://…  or  shell:appsFolder\…
    s.contains("://") || s.starts_with("shell:")
}

/// Open a URL/URI with the OS default handler.
fn open_url(url: &str) -> Result<()> {
    #[cfg(target_os = "windows")]
    {
        // `cmd /C start "" "<url>"` so the shell resolves the protocol handler.
        Command::new("cmd")
            .args(["/C", "start", "", url])
            .status()
            .with_context(|| format!("opening {url}"))?;
    }
    #[cfg(target_os = "macos")]
    {
        Command::new("open")
            .arg(url)
            .status()
            .with_context(|| format!("opening {url}"))?;
    }
    #[cfg(target_os = "linux")]
    {
        Command::new("xdg-open")
            .arg(url)
            .status()
            .with_context(|| format!("opening {url}"))?;
    }
    Ok(())
}
