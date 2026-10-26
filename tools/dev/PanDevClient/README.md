# PanDevClient

A stand-in for Arcadia towards Pan (#243 part B), until #242 gives Arcadia a caller for `Hide`/`Unhide`. It connects
like Arcadia (starts the service if needed, checks that the pipe's server is the service), says hello and then takes
one command per line on standard input:

| Command | What it does |
|---|---|
| `hide` | starts an emulation (if none runs) and hides the Steam Controller; status `hidden <n>` |
| `unhide` | shows it again; status `shown` |
| `check` | asks whether Pan could hide now; status `check <reason>` |
| `freeze` | stops the heartbeats, the pipe stays open: Pan sees a hang after 3 s; status `torn down Hang` |
| `resume` | heartbeats again, acknowledges a teardown; the next `hide` starts a new generation |
| `exit` | Arcadia's planned exit: unhide, stop, goodbye |
| `quit` | goodbye without unhiding: Pan's teardown shows the controller |

Killing the process is Arcadia crashing. Options: `--log <file>` (every exchange), `--status <file>` (the last state,
for scripts), `--quiet` (nothing on the console). Close Arcadia first: Pan serves one client at a time.

```powershell
dotnet run --project tools/dev/PanDevClient -- --log pan-dev.log
```

The check script for R12 (Steam and a controller Pan shows again from session 0) uses it: project files `spikes/243/`.
