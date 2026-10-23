namespace Arcadia.Launchers.Steam;

/// <summary>Steam's folder picked by hand (#138), e.g. a Steam copied to another drive that has no registry entry.</summary>
public sealed class SteamFolderChoice : Abstractions.ILauncherFolderChoice
{
    public string Expected => "steam.exe und den Ordner steamapps";

    public string? Find(string folder) => SteamInstallation.RootIn(folder);

    public void Use(string? folder) => SteamInstallation.ChosenRoot = folder;
}
