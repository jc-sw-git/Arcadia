<#
.SYNOPSIS
    CI only: installs HidHide 1.5.230 on the Windows runner, so Pan's hiding runs against the real driver (#243
    part B, tests marked HidHideFact and the service test). Sets ARCADIA_HIDHIDE_TESTS=1 for the next steps once
    HidHide's control device answers; without it those tests skip and say why.
    Never run this on a PC of your own: it installs a filter driver for every HID device.
#>
$ErrorActionPreference = 'Stop'

# Pinned to the owner's version: a new HidHide (e.g. 2.x, which Pan refuses) must not turn unrelated PRs red.
# Hash from winget's manifest of Nefarius.HidHide 1.5.230.
$url = 'https://github.com/nefarius/HidHide/releases/download/v1.5.230.0/HidHide_1.5.230_x64.exe'
$sha256 = 'F4BBBCB82E6258641B887C74BC81C4C5F66E4AA811808DFC304347687B7605F6'

$installer = Join-Path $env:RUNNER_TEMP 'HidHide_1.5.230_x64.exe'
Invoke-WebRequest -Uri $url -OutFile $installer -TimeoutSec 120
$actual = (Get-FileHash $installer -Algorithm SHA256).Hash
if ($actual -ne $sha256) { throw "HidHide installer hash $actual, expected $sha256" }
$signature = Get-AuthenticodeSignature $installer
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Nefarius') {
    throw "HidHide installer not signed by Nefarius: $($signature.Status) $($signature.SignerCertificate.Subject)"
}

Write-Host 'Installing HidHide 1.5.230'
# An Advanced Installer bootstrapper: /exenoui /qn is its silent mode. Burn's "/install /quiet" opened its UI and
# waited on the runner for six hours, so the setup gets a deadline of its own as well.
$setup = Start-Process -FilePath $installer -ArgumentList @('/exenoui', '/qn', '/norestart') -PassThru
if (-not $setup.WaitForExit(300000)) {
    Stop-Process -Id $setup.Id -Force -ErrorAction SilentlyContinue
    throw 'HidHide setup did not finish within 5 minutes'
}
if ($setup.ExitCode -notin 0, 3010) { throw "HidHide setup exited with $($setup.ExitCode)" }

function Test-ControlDevice {
    try {
        $stream = [System.IO.File]::Open('\\.\HidHide', 'Open', 'Read', 'ReadWrite')
        $stream.Dispose()
        return $true
    } catch {
        Write-Host "Control device: $($_.Exception.Message)"
        return $false
    }
}

if (-not (Test-ControlDevice)) {
    # The filter attaches when a HID stack starts; restart the runner's HID devices instead of rebooting.
    Get-PnpDevice -Class HIDClass -PresentOnly -ErrorAction SilentlyContinue | ForEach-Object {
        $restart = Start-Process pnputil -ArgumentList @('/restart-device', "`"$($_.InstanceId)`"") -WindowStyle Hidden -PassThru
        if (-not $restart.WaitForExit(30000)) { Stop-Process -Id $restart.Id -Force -ErrorAction SilentlyContinue }
    }
    Start-Sleep -Seconds 2
}

if (Test-ControlDevice) {
    Write-Host 'HidHide answers: its tests run'
    'ARCADIA_HIDHIDE_TESTS=1' | Out-File -FilePath $env:GITHUB_ENV -Append -Encoding utf8
} else {
    Write-Host '::warning::HidHide installed, but its control device does not answer on this runner: its tests skip'
}
