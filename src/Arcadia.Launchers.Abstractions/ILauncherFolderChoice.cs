namespace Arcadia.Launchers.Abstractions;

/// <summary>
/// Lets the user show Arcadia the launcher's folder by hand, for when <see cref="ILauncherIntegration.Locate"/> doesn't
/// find it, e.g. a Steam copied to another drive without its registry entry (#138). Optional: a launcher whose data
/// always lies in the same place needs none.
/// </summary>
public interface ILauncherFolderChoice
{
    /// <summary>What the folder must hold, for the message when it doesn't, e.g. <c>steam.exe und den Ordner steamapps</c>.</summary>
    string Expected { get; }

    /// <summary>Checks a folder the user picked; touches the disk, so call it off the UI thread.</summary>
    /// <returns>
    /// The launcher's folder, which may be the picked one or one next to it (say, the user picked <c>steamapps</c> inside
    /// it); null if the launcher isn't there.
    /// </returns>
    string? Find(string folder);

    /// <summary>
    /// From now on the launcher is only looked for in <paramref name="folder"/>, one <see cref="Find"/> returned; null looks
    /// for it as usual again. Called before every <see cref="ILauncherIntegration.Locate"/>, from any thread.
    /// </summary>
    void Use(string? folder);
}
