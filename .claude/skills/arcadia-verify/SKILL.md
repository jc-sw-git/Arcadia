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

## 4. Specialised checks

- **Foreground / Steam Controller Support toggle:** `./tools/dev/Trace-Foreground.ps1`
  (toggles the first settings item; expected when switching off: `Arcadia -> explorer -> Arcadia`).
- **Is the controller emulated?** `./tools/dev/Probe-XInput.ps1` (slot 0 connected while SISR runs) and
  `Invoke-RestMethod http://127.0.0.1:47640/api/v1/devices`.
- **Steam shortcuts:** `./tools/dev/Read-SteamShortcuts.ps1` (read-only).
- **Logs:** `%APPDATA%\Arcadia\logs\arcadia.log`, `sisr.log`.
- **Crash without log line:** Windows event log, provider `.NET Runtime` (see CLAUDE.md).

## 5. What only the owner can check

Physical Steam Controller behaviour (buttons in Arcadia, desktop layout after switching support
off, behaviour inside games) and real game launches. State clearly what you verified mechanically
and what still needs a hands-on test.
