<#
.SYNOPSIS
    Lists the non-Steam games in Steam's shortcuts.vdf (read-only) with their app ids.

.DESCRIPTION
    Steam keeps new entries in memory and writes shortcuts.vdf only when it exits, so an entry
    just added through Steam's UI or SISR's CEF API may not show up yet.
    Modern Steam does NOT derive the stored app id from CRC32(exe + name) anymore — always use
    the stored "appid". rungameid uses (appid << 32) | 0x02000000; forceinputappid uses appid.
#>
param([string] $Path)

if (-not $Path) {
    $steam = (Get-ItemProperty 'HKCU:\Software\Valve\Steam').SteamPath
    $user = (Get-ItemProperty 'HKCU:\Software\Valve\Steam\ActiveProcess').ActiveUser
    $Path = Join-Path $steam "userdata\$user\config\shortcuts.vdf"
}

$data = [IO.File]::ReadAllBytes($Path)
$script:pos = 0
function Read-CString {
    $start = $script:pos
    while ($data[$script:pos] -ne 0) { $script:pos++ }
    $text = [Text.Encoding]::UTF8.GetString($data, $start, $script:pos - $start)
    $script:pos++
    $text
}
function Read-Map {
    $map = [ordered]@{}
    while ($script:pos -lt $data.Length) {
        $type = $data[$script:pos]; $script:pos++
        if ($type -eq 8) { return $map }
        $key = Read-CString
        switch ($type) {
            0 { $map[$key] = Read-Map }
            1 { $map[$key] = Read-CString }
            2 { $map[$key] = [BitConverter]::ToInt32($data, $script:pos); $script:pos += 4 }
            default { throw "Unknown VDF type $type at $($script:pos)" }
        }
    }
    $map
}

$root = Read-Map
foreach ($entry in $root['shortcuts'].Values) {
    $appId = [uint32]([BitConverter]::ToUInt32([BitConverter]::GetBytes([int]$entry['appid']), 0))
    [pscustomobject]@{
        Name          = $entry['AppName']
        AppId         = $appId
        RunGameId     = ([uint64]$appId -shl 32) -bor 0x02000000
        Exe           = $entry['Exe']
        LaunchOptions = $entry['LaunchOptions']
    }
}
