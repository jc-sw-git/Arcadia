using Arcadia.Launchers.Steam;

// One command per call, one result line per account on standard output, for Run-Check-214.ps1:
//   list     <account id> <markers>   how many shortcuts start %LOCALAPPDATA%\SISR\SISR.exe
//   remove   <account id> <removed>   removes them (Steam must be closed; a .bak is kept)
// Exit code 2: no Steam found, 3: Steam is running (remove only), 1: wrong usage.
var command = args.FirstOrDefault();
if (command is not ("list" or "remove"))
{
    Console.Error.WriteLine("Usage: SisrMarkerCheck list|remove");
    return 1;
}

if (SteamInstallation.Find() is not { } steam)
{
    Console.Error.WriteLine("Steam not found");
    return 2;
}

if (command == "remove" && SteamInstallation.IsRunning)
{
    Console.Error.WriteLine("Steam is running");
    return 3;
}

var sisrExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SISR", "SISR.exe");
foreach (var shortcuts in steam.AllShortcutsPaths())
{
    var account = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(shortcuts)));
    var count = command == "remove"
        ? SteamShortcuts.RemoveByExecutable(shortcuts, sisrExe)
        : SteamShortcuts.Read(shortcuts).Count(s => SteamShortcuts.FindByExecutable([s], sisrExe) is not null);
    Console.WriteLine($"{account} {count}");
}

return 0;
