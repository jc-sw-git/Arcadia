# Owner-in-the-loop reproduction for bugs that only show on the owner's PC.
# Copy this file, edit the steps between the markers, and have the owner run it in PowerShell.
# The captured answers are printed as KEY=VALUE at the end; the owner pastes them back.
# Capture observations only — never ask for passwords or tokens here.

$ErrorActionPreference = 'Stop'
$captured = [ordered]@{}

function Step([string]$Instruction) {
    Write-Host "`n>>> $Instruction" -ForegroundColor Cyan
    [void](Read-Host '    [Enter when done]')
}

function Capture([string]$Key, [string]$Question) {
    Write-Host "`n>>> $Question" -ForegroundColor Cyan
    $captured[$Key] = Read-Host '    >'
}

# --- edit below ---------------------------------------------------------

Step 'Start Arcadia and open the Epic tab.'
Capture 'PROGRESS_STUCK' 'Pause the running install in Epic, then cancel it. Does Arcadia still show progress? (j/n)'
Capture 'SHOWN_TEXT' 'What does the tile say?'

# --- edit above ---------------------------------------------------------

Write-Host "`n--- Captured ---"
$captured.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }
