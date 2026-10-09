<#
.SYNOPSIS
    Starts Arcadia, optionally sends keys, and saves a screenshot of its window.

.EXAMPLE
    ./tools/dev/Screenshot-Arcadia.ps1 -Out $env:TEMP\arcadia.png -Keys '{F2}' -AfterKeysMs 1000
    Opens the settings dialog and captures it.

.NOTES
    Build first (dotnet build). Keys use System.Windows.Forms.SendKeys syntax: "e", "{F2}",
    "{ENTER}", "{DOWN}". The window is captured from the screen, so it must be visible.
#>
param(
    [Parameter(Mandatory)] [string] $Out,
    [string] $Arguments = '--demo',
    [int] $WaitMs = 7000,
    [string[]] $Keys = @(),
    [int] $AfterKeysMs = 500,
    [switch] $KeepRunning
)

$exe = Join-Path $PSScriptRoot '..\..\src\Arcadia.App\bin\Debug\net10.0\Arcadia.App.exe' | Resolve-Path
Add-Type -AssemblyName System.Drawing, System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class ArcadiaShot {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
"@
[ArcadiaShot]::SetProcessDPIAware() | Out-Null

$process = Start-Process -FilePath $exe -ArgumentList $Arguments -PassThru
Start-Sleep -Milliseconds $WaitMs
$process.Refresh()
$handle = $process.MainWindowHandle
if ($handle -eq [IntPtr]::Zero) { throw 'Arcadia has no window yet — raise -WaitMs.' }

[ArcadiaShot]::SetForegroundWindow($handle) | Out-Null
Start-Sleep -Milliseconds 400
foreach ($key in $Keys) {
    [System.Windows.Forms.SendKeys]::SendWait($key)
    Start-Sleep -Milliseconds 350
}
Start-Sleep -Milliseconds $AfterKeysMs

$rect = New-Object ArcadiaShot+RECT
[ArcadiaShot]::GetWindowRect($handle, [ref]$rect) | Out-Null
$bitmap = New-Object System.Drawing.Bitmap ($rect.R - $rect.L), ($rect.B - $rect.T)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($rect.L, $rect.T, 0, 0, $bitmap.Size)
$bitmap.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose(); $bitmap.Dispose()

if (-not $KeepRunning) { $process.CloseMainWindow() | Out-Null }
"Saved $Out"
