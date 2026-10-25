<#
.SYNOPSIS
    Spike #112: runs the Steam Input probe steps on the owner's PC and collects their logs in one folder.

.DESCRIPTION
    Before: Steam runs and is logged in, no Steam game runs, the Steam Controller is on, and SISR does not run
    (Arcadia closed, or its Steam Controller support switched off). The probe poses as Steam's "SISR Marker"
    shortcut, exactly like SISR, so it uses that shortcut's controller layout; nothing in Steam is changed.

    Each step says what to do with the controller and waits for Enter. The probe window prints "PROMPT >>>" lines
    with what to do next while it runs. All logs land in logs\<time>\ next to this script and are zipped at the end.

    -Build publishes the probe from tools\dev\SteamInputProbe and downloads SDL 3.4.18 (SHA-256 checked).

.EXAMPLE
    ./tools/dev/Run-SteamInputProbe.ps1 -Build
    ./Run-SteamInputProbe.ps1 -Steps 2,3
#>
param(
    # The download package has the probe in .\probe; in the repo it is published to SteamInputProbe\bin\probe.
    [string] $ProbeDir = $(if (Test-Path (Join-Path $PSScriptRoot 'probe')) { Join-Path $PSScriptRoot 'probe' } else { Join-Path $PSScriptRoot 'SteamInputProbe\bin\probe' }),
    [int[]] $Steps = @(1, 2, 3, 4, 5, 6),
    [switch] $Build
)
$ErrorActionPreference = 'Stop'

$sdlUrl = 'https://github.com/libsdl-org/SDL/releases/download/release-3.4.18/SDL3-3.4.18-win32-x64.zip'
$sdlZipSha256 = '75c2c0fc74e7d1206aaedc22893a35887332f4f85f5c8b74fddc10d177985f2c'

if ($Build) {
    dotnet publish (Join-Path $PSScriptRoot 'SteamInputProbe') -c Release -o $ProbeDir -v q
    if (-not (Test-Path (Join-Path $ProbeDir 'SDL3.dll'))) {
        $zip = Join-Path $env:TEMP 'SDL3-3.4.18-win32-x64.zip'
        Invoke-WebRequest $sdlUrl -OutFile $zip
        if ((Get-FileHash $zip -Algorithm SHA256).Hash -ne $sdlZipSha256) { throw "SDL download does not match its SHA-256" }
        Expand-Archive $zip -DestinationPath $ProbeDir -Force
    }
}
$probe = Join-Path $ProbeDir 'SteamInputProbe.exe'
if (-not (Test-Path $probe)) { throw "$probe not found (run with -Build)" }

if (Get-Process SISR -ErrorAction SilentlyContinue) { throw 'SISR runs. Close Arcadia or switch its Steam Controller support off first.' }
if (-not (Get-Process steam -ErrorAction SilentlyContinue)) { throw 'Steam does not run.' }

$marker = & (Join-Path $PSScriptRoot 'Read-SteamShortcuts.ps1') | Where-Object { $_.LaunchOptions -match '--marker' } | Select-Object -First 1
if (-not $marker) { throw 'No "SISR Marker" shortcut found in shortcuts.vdf.' }
$markerId = $marker.AppId
Write-Host "SISR Marker: app id $markerId"

$logs = Join-Path $PSScriptRoot ("logs\" + (Get-Date -Format 'yyyy-MM-dd_HHmmss'))
New-Item -ItemType Directory -Force $logs | Out-Null

function Step([int] $number, [string] $title, [string] $todo) {
    if ($Steps -notcontains $number) { return $false }
    Write-Host ''
    Write-Host "=== Step $number`: $title" -ForegroundColor Cyan
    Write-Host $todo
    Read-Host 'Press Enter to start' | Out-Null
    return $true
}

function Probe([string] $name, [string[]] $arguments) {
    # Windows PowerShell 5.1 passes a double quote inside an argument to a program unescaped, which splits it.
    if ($arguments -match '"') { throw "Probe argument with a double quote: $($arguments -match '"')" }
    & $probe @arguments --log (Join-Path $logs "$name.log")
}

function ProbeInBackground([string] $name, [string[]] $arguments) {
    Start-Process $probe -ArgumentList ($arguments + @('--log', "`"$(Join-Path $logs "$name.log")`"")) -PassThru
}

if (Step 1 'Control: no overlay (what Steam gives a normal program)' @'
Keep this window in front. Press A, B, X, Y, move the left stick, and move the RIGHT TRACKPAD (it should move
the mouse, as usual on the desktop). 20 seconds.
'@) {
    Probe '1-control' @('steam', '--marker', $markerId, '--no-overlay', '--no-force', '--seconds', '20',
        '--say', '1=Press A, B, X, Y, move the left stick, then move the mouse with the right trackpad')
}

if (Step 2 'Steam pad: overlay as "SISR Marker", layout forced' @'
45 seconds. Follow the PROMPT lines. Have a second window ready to click (e.g. Explorer).
Near the end, look at Steam's friends list: does it show you "in game" / "SISR Marker"?
'@) {
    $arguments = @('steam', '--marker', $markerId, '--seconds', '45',
        '--say', '2=Keep this window in front. Press A, B, X, Y, Back+Start, move both sticks, pull both triggers',
        '--say', '15=Now click ANOTHER window (Explorer) and keep pressing buttons and moving the sticks',
        '--say', '30=Move the right trackpad and tilt the controller. Check Steam friends list: in game?')
    Probe '2-steam-pad' $arguments
    $log2 = Join-Path $logs '2-steam-pad.log'
    if ((Test-Path $log2) -and -not (Select-String -Path $log2 -Pattern 'CONNECTED' -Quiet)) {
        Write-Host 'No XInput pad in step 2. Same again, with a 1x1 window of the probe (does Steam need one?).' -ForegroundColor Yellow
        Read-Host 'Press Enter to start' | Out-Null
        Probe '2b-steam-pad-window' ($arguments + @('--window'))
    }
}

if (Step 3 'Switch to Steam Input (#93): release the layout, then force it again' @'
40 seconds. The probe releases the forced layout after 12 s and forces it again after 26 s.
Keep moving the left stick and the right trackpad all the time; follow the PROMPT lines.
'@) {
    Probe '3-switch' @('steam', '--marker', $markerId, '--seconds', '40', '--release-at', '12', '--reforce-at', '26',
        '--say', '2=Keep this window in front. Keep moving the left stick and the right trackpad, press A now and then',
        '--say', '12=Layout released: keep moving. Does the trackpad move the mouse again?',
        '--say', '18=Now click ANOTHER window, keep moving the stick and trackpad',
        '--say', '26=Layout forced again: keep moving, press A')
}

if (Step 4 'Raw SDL3 beside the Steam pad (gyro and trackpads for Arcadia)' @'
A second probe window (Steam pad) opens for 55 s; this window reads the controller with SDL3 for 45 s.
Press buttons, touch BOTH trackpads, tilt and turn the controller. Afterwards check the Steam pad window:
did it log buttons the whole time?
'@) {
    $background = ProbeInBackground '4-steam-pad-beside-raw' @('steam', '--marker', $markerId, '--seconds', '55')
    Start-Sleep -Seconds 4
    Probe '4-raw' @('raw', '--seconds', '45',
        '--say', '1=Press A, B, X, Y, touch the LEFT trackpad, then the RIGHT one, then tilt and turn the controller',
        '--say', '25=Press A, B a few times again (the other window must log them too)')
    $background.WaitForExit()
}

if (Step 5 'Raw SDL3 alone (Steam desktop layout active): double input?' @'
30 seconds, no Steam pad. Move the right trackpad (mouse), press A and B, tilt the controller.
After it ends: does the trackpad still move the mouse as before?
'@) {
    Probe '5-raw-alone' @('raw', '--seconds', '30',
        '--say', '1=Move the mouse with the right trackpad, press A and B, tilt the controller')
}

if (Step 6 'VIIPER: forward the Steam pad as Xbox 360 controller, without SISR.exe' @'
40 seconds. The probe creates a virtual Xbox 360 controller and forwards the Steam pad to it.
A second window lists what a normal program sees through XInput (Probe-XInput).
Press A, B, X, Y and move the sticks. Optional: open joy.cpl ("Set up USB game controllers") and look.
'@) {
    $viiperView = Join-Path $logs '6-xinput-view.log'
    $xinput = Start-Process powershell -PassThru -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command',
        "& '$(Join-Path $PSScriptRoot 'Probe-XInput.ps1')' -Seconds 40 *> '$viiperView'")
    Probe '6-viiper' @('steam', '--marker', $markerId, '--seconds', '40', '--viiper',
        '--say', '6=Press A, B, X, Y and move both sticks (now through VIIPER)')
    $xinput.WaitForExit()
}

$zip = "$logs.zip"
Compress-Archive -Path "$logs\*" -DestinationPath $zip -Force
Write-Host ''
Write-Host "Done. Logs: $zip" -ForegroundColor Green
