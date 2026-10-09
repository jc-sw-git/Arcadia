//! SteamButler CLI — the frontend used to drive and test the core before the
//! GUI exists.

use anyhow::{bail, Context, Result};
use clap::{Parser, Subcommand};
use std::path::{Path, PathBuf};
use steambutler_core::{appid, shortcut, steam, sync, vdf};
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
    /// Reconcile scanned games into shortcuts.vdf. Dry-run unless `--write`.
    Apply {
        /// Actually write shortcuts.vdf (Steam must be closed). Otherwise dry-run.
        #[arg(long)]
        write: bool,
        /// Target a specific Steam user id under `userdata/`. Required if several exist.
        #[arg(long)]
        user: Option<String>,
    },
}

fn main() -> Result<()> {
    match Cli::parse().command {
        Commands::Scan => cmd_scan(),
        Commands::Steam => cmd_steam(),
        Commands::Apply { write, user } => cmd_apply(write, user.as_deref()),
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

fn cmd_apply(write: bool, user: Option<&str>) -> Result<()> {
    let root = steam::steam_root().context("Steam installation not found")?;
    let userdata = pick_userdata(&root, user)?;
    let vdf_path = steam::shortcuts_vdf(&userdata);
    let existing =
        vdf::read(&vdf_path).with_context(|| format!("reading {}", vdf_path.display()))?;

    // Build desired shortcuts from scanned games; skip modes we can't emit yet.
    let mut desired = Vec::new();
    let mut skipped = Vec::new();
    for game in scan_all() {
        match shortcut::build_shortcut(&game) {
            Ok(sc) => desired.push(sc),
            Err(_) => skipped.push(game),
        }
    }

    let plan = sync::plan(&existing, &desired);

    println!(
        "Steam user {}  ({} existing shortcut(s))",
        userdata.file_name().unwrap_or_default().to_string_lossy(),
        existing.len()
    );
    println!("  {}\n", vdf_path.display());

    for sc in &plan.added {
        println!("  + {}", sc.app_name);
    }
    for sc in &plan.removed {
        println!("  - {}", sc.app_name);
    }
    if plan.added.is_empty() && plan.removed.is_empty() {
        println!("  (no changes)");
    }
    println!(
        "\n{} to add, {} to remove, {} unchanged (managed), {} foreign (untouched)",
        plan.added.len(),
        plan.removed.len(),
        plan.kept.len(),
        plan.foreign.len()
    );
    if !skipped.is_empty() {
        println!(
            "{} game(s) skipped — wrapper emission not implemented yet",
            skipped.len()
        );
    }

    if !write {
        println!("\nDry-run — nothing written. Re-run with --write (Steam must be closed).");
        return Ok(());
    }

    if plan.is_noop() {
        println!("\nNothing to write.");
        return Ok(());
    }

    if steam::is_running() {
        bail!("Steam is running — close it before writing (it would overwrite the file on exit).");
    }

    if vdf_path.exists() {
        let backup = vdf_path.with_extension("vdf.bak");
        std::fs::copy(&vdf_path, &backup)
            .with_context(|| format!("backing up to {}", backup.display()))?;
        println!("\nBacked up existing file to {}", backup.display());
    }

    let final_list = sync::apply(&plan);
    vdf::write(&vdf_path, &final_list)
        .with_context(|| format!("writing {}", vdf_path.display()))?;
    println!(
        "Wrote {} shortcut(s) to {}",
        final_list.len(),
        vdf_path.display()
    );
    Ok(())
}

fn pick_userdata(root: &Path, user: Option<&str>) -> Result<PathBuf> {
    if let Some(id) = user {
        let dir = root.join("userdata").join(id);
        if dir.is_dir() {
            return Ok(dir);
        }
        bail!("userdata/{id} not found under {}", root.display());
    }

    let dirs = steam::userdata_dirs(root);
    match dirs.as_slice() {
        [] => bail!("no userdata directories found under {}", root.display()),
        [only] => Ok(only.clone()),
        many => {
            let ids: Vec<String> = many
                .iter()
                .filter_map(|p| p.file_name().map(|n| n.to_string_lossy().into_owned()))
                .collect();
            bail!(
                "multiple Steam users found ({}); pass --user <id>",
                ids.join(", ")
            )
        }
    }
}
