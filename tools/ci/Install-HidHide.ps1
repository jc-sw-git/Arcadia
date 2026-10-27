<#
.SYNOPSIS
    CI only: installs HidHide 1.5.230 on the Windows runner, so Pan's hiding runs against the real driver (#243
    part B, tests marked HidHideFact and the service test). Sets ARCADIA_HIDHIDE_TESTS=1 for the next steps once
    HidHide's control device answers; without it those tests skip and say why.
    Never run this on a PC of your own: it installs a filter driver for every HID device.
#>
$ErrorActionPreference = 'Stop'

$headers = @{ 'User-Agent' = 'arcadia-ci' }
if ($env:GH_TOKEN) { $headers.Authorization = "Bearer $env:GH_TOKEN" }
# Pinned to the owner's version: a new HidHide (e.g. 2.x, which Pan refuses) must not turn unrelated PRs red.
$pinned = '1.5.230'
$releases = Invoke-RestMethod -Headers $headers -Uri 'https://api.github.com/repos/nefarius/HidHide/releases?per_page=50'
$release = $releases | Where-Object { $_.tag_name -match [regex]::Escape($pinned) } | Select-Object -First 1
if (-not $release) { throw "HidHide $pinned not found among its releases" }
$asset = $release.assets | Where-Object { $_.name -match 'x64.*\.(exe|msi)$' } | Select-Object -First 1
if (-not $asset) { throw "No x64 installer in HidHide release $($release.tag_name)" }

$installer = Join-Path $env:RUNNER_TEMP $asset.name
Invoke-WebRequest -Headers $headers -Uri $asset.browser_download_url -OutFile $installer
$signature = Get-AuthenticodeSignature $installer
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Nefarius') {
    throw "HidHide installer not signed by Nefarius: $($signature.Status) $($signature.SignerCertificate.Subject)"
}

Write-Host "Installing HidHide $($release.tag_name) ($($asset.name))"
$arguments = if ($installer.EndsWith('.msi')) { @('/i', "`"$installer`"", '/qn', '/norestart') } else { @('/install', '/quiet', '/norestart') }
$exe = if ($installer.EndsWith('.msi')) { 'msiexec.exe' } else { $installer }
$setup = Start-Process -FilePath $exe -ArgumentList $arguments -Wait -PassThru
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
        & pnputil /restart-device "$($_.InstanceId)" | Out-Null
    }
    Start-Sleep -Seconds 2
}

if (Test-ControlDevice) {
    Write-Host 'HidHide answers: its tests run'
    'ARCADIA_HIDHIDE_TESTS=1' | Out-File -FilePath $env:GITHUB_ENV -Append -Encoding utf8
} else {
    Write-Host '::warning::HidHide installed, but its control device does not answer on this runner: its tests skip'
}
