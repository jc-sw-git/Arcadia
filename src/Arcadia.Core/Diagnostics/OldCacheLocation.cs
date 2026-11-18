namespace Arcadia.Core.Diagnostics;

/// <summary>
/// Moves the artwork and metadata caches that versions up to #568 kept in <c>%LOCALAPPDATA%\Arcadia</c>, Velopack's
/// install folder, to the data folder. The setup deletes that folder on "overwrite" or repair, and the artwork cache holds
/// images the player chose by hand, which are not downloaded again. Best effort: what cannot be moved stays and is tried
/// at the next start. Run before <see cref="LogLocation.MoveOldLogs"/>, which removes the install folder once it is empty.
/// </summary>
public static class OldCacheLocation
{
    private static readonly string[] CacheFolders = ["artwork", "metadata"];

    public static void Move(string localAppData, string dataFolder)
    {
        var installFolder = Path.Combine(localAppData, "Arcadia");
        foreach (var name in CacheFolders)
        {
            try
            {
                MoveFolder(Path.Combine(installFolder, name), Path.Combine(dataFolder, name));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Nothing is lost: the old cache stays where it was until the next start.
            }
        }
    }

    private static void MoveFolder(string from, string to)
    {
        if (!Directory.Exists(from))
        {
            return;
        }

        if (!Directory.Exists(to))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            try
            {
                Directory.Move(from, to);
                return;
            }
            catch (IOException)
            {
                // Another drive (redirected AppData folders): the folder can't be renamed, its files can be moved one by one.
            }
        }

        // Both exist (a new cache was started before the move, or the folder could not be renamed): keep what the new one has, add what only the old one has.
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            if (File.Exists(target))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Move(file, target);
        }

        Directory.Delete(from, recursive: true);
    }
}
