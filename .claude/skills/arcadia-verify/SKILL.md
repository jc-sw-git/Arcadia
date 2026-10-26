---
name: arcadia-verify
description: Run Arcadia and check a change for real — build, start with demo data, drive it with keys, take screenshots, trace the foreground window, probe XInput, read logs. Use after UI, input, launcher or Steam Controller Support changes, before reporting them as done.
---

# Verify Arcadia changes in the running app

Unit tests don't cover the UI, input or SISR. Check those in the running app.

## 1. Build cleanly

```powershell
Get-Process Arcadia -ErrorAction SilentlyContinue | ForEach-Object { $_.CloseMainWindow() | Out-Null }
Start-Sleep -Seconds 8   # let Arcadia stop SISR
dotnet build Arcadia.slnx -v q -nologo
dotnet test Arcadia.slnx --no-build -nologo
```

In a fresh PowerShell after installing the SDK, reload PATH first:
`$env:Path = [Environment]::GetEnvironmentVariable('Path','Machine') + ';' + [Environment]::GetEnvironmentVariable('Path','User')`.

Start options: `--demo` (demo libraries for all launchers, skips the first start's page), `--first-run` (shows the
first start's launcher question again; with `--demo` the answer holds for the session only), `--demo-games 5000` (with
`--demo`: that many demo games in all, to measure a large library). After the start, `arcadia.log` names the time from
the process start to the shown library and the working set (line `Bibliothek: …`), the values of the budgets in `CLAUDE.md`.

## 2. Tell the owner before driving the app

The owner frequently tests by hand at the same time; key presses from both collide (this happened).
Say "I'm driving Arcadia for ~1 min, please don't touch it" before running the scripts below.

## 3. Look at it

```powershell
./tools/dev/Screenshot-Arcadia.ps1 -Out $env:TEMP\a.png                      # library
./tools/dev/Screenshot-Arcadia.ps1 -Out $env:TEMP\a.png -Keys '{F2}' -AfterKeysMs 1000   # settings
./tools/dev/Screenshot-Arcadia.ps1 -Out $env:TEMP\a.png -Keys 'e','e','v'      # next tab twice, view dialog
```

Then read the PNG. Keys: arrows, Enter, Esc, Q/E (tabs), PageUp/PageDown (pages), V (view), F2 (settings), F11 (fullscreen).
`-WaitMs` must cover startup (window handle exists after ~4–7 s).

### Linux (cloud threads)

No Windows, no gamepad: the real app runs under Xvfb with `--demo`, keys stand in for the controller.

```bash
apt-get update && apt-get install -y dotnet-sdk-10.0 xvfb xdotool imagemagick   # what's missing; ~1 min
dotnet build Arcadia.slnx -v q -clp:ErrorsOnly
tools/dev/screenshot-arcadia.sh --out "$SCRATCH/a.png"                          # library
tools/dev/screenshot-arcadia.sh --out "$SCRATCH/a.png" --keys Return --after-keys-ms 1000   # settings
tools/dev/screenshot-arcadia.sh --out "$SCRATCH/a.png" --keys "d d r s"         # third cover, game menu, 2nd item
```

Same parameters as the PowerShell script (`--arguments`, `--wait-ms`, `--keys`, `--after-keys-ms`, `--keep-running`);
keys are xdotool names (`space`, `Escape`, `shift+e`, `F2`; the full map is in the script's head). It gives the window the
whole 1920 × 1080 screen, parks the pointer and keeps settings and logs in a fresh folder per run (printed at the end).
Look at a detail at 200 %: `convert a.png -crop 600x300+800+200 -scale 200% detail.png`, then read that.
Headless captures (`Frame.Save` in `Arcadia.App.Tests`) are for a failing test only, not for looking at a change.

## 4. Before pushing a UI change

Each look is one screenshot read at 200 % (crop as above), on every page state the change touches:

- Focus on the **first**, the **last** and one **edge** element (a corner, the end of a row) of each changed list or bar.
- The **lifted** (focused cover, hover) and the **pressed** state of changed buttons.
- In each image: a focus ring cut off on any side, text running into an edge or cut, things out of line with their
  neighbours (left edges, baselines, gaps).
- `dotnet test --filter "FullyQualifiedName~Ring"` green: `RingGeometryTests` checks every visible button's ring
  against its clipping parents, the pixel tests (`AssertRingAround`) the main lists. A new list, bar or dialog gets
  its page state in `RingGeometryTests` and a pixel check at its edges.

Say in the PR which states you looked at and which you only checked by test.

## 5. Specialised checks

- **Foreground / Steam Controller Support toggle:** `./tools/dev/Trace-Foreground.ps1`
  (toggles the first settings item; expected when switching off: `Arcadia -> explorer -> Arcadia`).
- **Is the controller emulated?** `./tools/dev/Probe-XInput.ps1` (slot 0 connected while SISR runs) and
  `Invoke-RestMethod http://127.0.0.1:47640/api/v1/devices`.
- **Steam shortcuts:** `./tools/dev/Read-SteamShortcuts.ps1` (read-only).
- **Logs:** `%APPDATA%\Arcadia\logs\arcadia.log`, `sisr.log`.
- **Crash without log line:** Windows event log, provider `.NET Runtime` (see CLAUDE.md).

## 6. What only the owner can check

Physical Steam Controller behaviour (buttons in Arcadia, desktop layout after switching support
off, behaviour inside games) and real game launches. State clearly what you verified mechanically
and what still needs a hands-on test.
