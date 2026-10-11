<#
.SYNOPSIS
    Builds the Windows installer and update packages for Arcadia with Velopack.

.DESCRIPTION
    Publishes Arcadia self-contained for win-x64 and packs it with vpk. Output in artifacts\:
      publish\                     the published program
      releases\Arcadia-win-Setup.exe        installer (no admin rights, installs to %LOCALAPPDATA%\Arcadia)
      releases\Arcadia-win-Portable.zip     portable build
      releases\Arcadia-<version>-full.nupkg, releases.win.json, …  the update feed

    For delta packages, put the previous release's files into artifacts\releases first
    (the release workflow does this with "vpk download github").

.EXAMPLE
    .\tools\release\Pack.ps1 -Version 0.1.0
#>
param(
    # SemVer without a leading "v". Defaults to <Version> in Directory.Build.props.
    [string] $Version
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$artifacts = Join-Path $root 'artifacts'
$publish = Join-Path $artifacts 'publish'
$releases = Join-Path $artifacts 'releases'

if (-not $Version) {
    $Version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props'))).Project.PropertyGroup.Version
}
$Version = $Version.TrimStart('v')

if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

dotnet publish (Join-Path $root 'src\Arcadia.App\Arcadia.App.csproj') `
    --configuration Release --runtime win-x64 --self-contained `
    -p:Version=$Version --output $publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

Push-Location $root
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed' }

    dotnet vpk pack `
        --packId Arcadia `
        --packVersion $Version `
        --packTitle Arcadia `
        --packAuthors 'Juri Himmelreich' `
        --packDir $publish `
        --mainExe Arcadia.exe `
        --icon (Join-Path $root 'src\Arcadia.App\Assets\arcadia.ico') `
        --outputDir $releases
    if ($LASTEXITCODE -ne 0) { throw 'vpk pack failed' }
}
finally {
    Pop-Location
}

Write-Host "Arcadia $Version packed to $releases"
