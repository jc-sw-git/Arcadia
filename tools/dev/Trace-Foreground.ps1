<#
.SYNOPSIS
    Starts Arcadia, opens the settings (F2), presses Enter on the first item (toggles Steam
    Controller Support) and records which process owns the foreground, every 10 ms.

.DESCRIPTION
    Used to verify foreground behaviour around SISR: when it starts, Arcadia must keep the
    focus; when it stops, Arcadia bounces the focus to the desktop and back (Steam only switches
    the Steam Controller back to its desktop layout on a foreground change).

.EXAMPLE
    ./tools/dev/Trace-Foreground.ps1
    Expected after switching off: "Arcadia -> explorer -> Arcadia".
#>
param([int] $TraceMs = 4000)

$exe = Join-Path $PSScriptRoot '..\..\src\Arcadia.App\bin\Debug\net10.0\Arcadia.exe' | Resolve-Path
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class ArcadiaFg {
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
}
"@
function Get-ForegroundProcess {
    $id = 0
    [ArcadiaFg]::GetWindowThreadProcessId([ArcadiaFg]::GetForegroundWindow(), [ref]$id) | Out-Null
    (Get-Process -Id $id).ProcessName
}

$process = Start-Process $exe -ArgumentList '--demo' -PassThru
Start-Sleep -Seconds 7
$process.Refresh()
[ArcadiaFg]::SetForegroundWindow($process.MainWindowHandle) | Out-Null
Start-Sleep -Seconds 5
"before: $(Get-ForegroundProcess); SISR running: $([bool](Get-Process SISR -ErrorAction SilentlyContinue))"

[System.Windows.Forms.SendKeys]::SendWait('{F2}'); Start-Sleep -Milliseconds 800
[System.Windows.Forms.SendKeys]::SendWait('{ENTER}')

$seen = New-Object System.Collections.Generic.List[string]
$clock = [Diagnostics.Stopwatch]::StartNew()
while ($clock.ElapsedMilliseconds -lt $TraceMs) {
    $current = Get-ForegroundProcess
    if ($seen.Count -eq 0 -or $seen[$seen.Count - 1] -ne $current) { $seen.Add($current) }
    Start-Sleep -Milliseconds 10
}
"foreground: $($seen -join ' -> ')"
"SISR running: $([bool](Get-Process SISR -ErrorAction SilentlyContinue))"
Get-Content (Join-Path $env:APPDATA 'Arcadia\logs\arcadia.log') -Tail 6
