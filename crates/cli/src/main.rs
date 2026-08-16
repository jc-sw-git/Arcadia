//! SteamButler CLI — the frontend used to drive and test the core before the
//! GUI exists.

use anyhow::Result;
use clap::{Parser, Subcommand};
use steambutler_core::{appid, steam};
use steambutler_scanners::scan_all;

#[derive(Parser)]
#[command(
    name = "steambutler",
    version,
    about = "Add non-Steam launcher games to Steam"
)]
struct Cli {
    #[command(subcommand)]
    command: Commands,
}

#[derive(Subcommand)]
enum Commands {
    /// Scan installed launchers and list the games found.
    Scan,
    /// Show the detected Steam install and per-user config locations.
    Steam,
}

fn main() -> Result<()> {
    match Cli::parse().command {
        Commands::Scan => cmd_scan(),
        Commands::Steam => cmd_steam(),
    }
}

fn cmd_scan() -> Result<()> {
    let games = scan_all();
    if games.is_empty() {
        println!("No games found (no supported launcher installed, or scanners still stubbed).");
        return Ok(());
    }

    println!("Found {} game(s):\n", games.len());
    for game in &games {
        let emission = game.source.default_emission();
        let app_id = appid::shortcut_app_id(&format!("{:?}", game.launch), &game.name);
        println!(
            "  [{:<16}] {:<40} {:?}  appid={}  {}",
            game.source.label(),
            game.name,
            emission,
            app_id,
            if game.installed {
                "installed"
            } else {
                "MISSING"
            }
        );
    }
    Ok(())
}

fn cmd_steam() -> Result<()> {
    match steam::steam_root() {
        Some(root) => {
            println!("Steam root: {}", root.display());
            let users = steam::userdata_dirs(&root);
            if users.is_empty() {
                println!("  (no userdata directories found)");
            }
            for user in users {
                println!(
                    "  user: {}",
                    user.file_name().unwrap_or_default().to_string_lossy()
                );
                println!(
                    "    shortcuts.vdf: {}",
                    steam::shortcuts_vdf(&user).display()
                );
                println!("    grid/:         {}", steam::grid_dir(&user).display());
            }
        }
        None => {
            println!("Steam not found. Candidates checked:");
            for c in steam::steam_root_candidates() {
                println!("  {}", c.display());
            }
        }
    }
    Ok(())
}
