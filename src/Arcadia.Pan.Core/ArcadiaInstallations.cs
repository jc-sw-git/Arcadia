namespace Arcadia.Pan;

/// <summary>Whether an Arcadia installation is on the PC, as far as Pan can tell (#422).</summary>
public enum ArcadiaPresence
{
    Found,
    Absent,

    /// <summary>A place Arcadia may be could not be read: counts as "Arcadia may be there".</summary>
    Unknown,
}

/// <summary>
/// Where Pan looks for Arcadia at its start (#422): Velopack's install in every local user profile, and every
/// <c>Arcadia.exe</c> whose bundled Pan ran <c>--install</c> (a build without the installer). Pan only checks that
/// these files exist; it never runs, changes or deletes anything there, as the folders are the users'. A user who fakes
/// one only keeps Pan installed.
/// </summary>
public static class ArcadiaInstallations
{
    public const string ExecutableName = "Arcadia.exe";

    /// <summary>The installs <see cref="RecordedInstallsFile"/> keeps at most; the oldest goes first.</summary>
    public const int MaxRecorded = 8;

    /// <summary>Velopack installs per user into <c>%LocalAppData%\Arcadia\current</c>.</summary>
    public static string VelopackExecutable(string profile) => Path.Combine(profile, "AppData", "Local", "Arcadia", "current", ExecutableName);

    /// <summary>The <c>Arcadia.exe</c> beside the Arcadia folder a bundled <c>Pan\Pan.exe</c> runs from, or null.</summary>
    public static string? BesideBundledPan(string panPath) =>
        Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(panPath))) is { } arcadia ? Path.Combine(arcadia, ExecutableName) : null;

    /// <param name="profiles">The local user profiles' folders; null when Windows' list of them could not be read.</param>
    /// <param name="probe">Whether one file exists, or <see cref="ArcadiaPresence.Unknown"/> when that can't be told.</param>
    public static ArcadiaPresence Look(IEnumerable<string>? profiles, IEnumerable<string> recorded, Func<string, ArcadiaPresence> probe)
    {
        if (profiles is null)
        {
            return ArcadiaPresence.Unknown;
        }

        var result = ArcadiaPresence.Absent;
        foreach (var file in profiles.Select(VelopackExecutable).Concat(recorded))
        {
            switch (probe(file))
            {
                case ArcadiaPresence.Found:
                    return ArcadiaPresence.Found;
                case ArcadiaPresence.Unknown:
                    result = ArcadiaPresence.Unknown;
                    break;
            }
        }

        return result;
    }
}

/// <summary>The <c>Arcadia.exe</c> paths <c>--install</c> was run for, one per line, in Pan's data folder.</summary>
public sealed class RecordedInstallsFile(string path)
{
    public const string FileName = "arcadia-installs.txt";

    /// <summary>A file this large is not one Pan wrote (8 short lines): it is not read.</summary>
    public const long MaxBytes = 64 * 1024;

    /// <summary>
    /// Only fully qualified paths; a file that can't be read, or is too large, gives none (then only Velopack's places
    /// count). Also read from a data folder Pan is about to set aside, so its content is never trusted further.
    /// </summary>
    public IReadOnlyList<string> Load()
    {
        try
        {
            return File.Exists(path) && new FileInfo(path).Length <= MaxBytes
                ? File.ReadAllLines(path).Select(line => line.Trim()).Where(IsUsable).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>Adds <paramref name="executable"/> as the newest; keeps at most <see cref="ArcadiaInstallations.MaxRecorded"/>.</summary>
    public void Add(string executable)
    {
        if (!IsUsable(executable))
        {
            return;
        }

        var lines = Load().Where(line => !string.Equals(line, executable, StringComparison.OrdinalIgnoreCase)).Append(executable)
            .TakeLast(ArcadiaInstallations.MaxRecorded);
        File.WriteAllLines(path, lines);
    }

    private static bool IsUsable(string line) =>
        line.Length > 0 && line.IndexOfAny(Path.GetInvalidPathChars()) < 0 && Path.IsPathFullyQualified(line)
        && string.Equals(Path.GetFileName(line), ArcadiaInstallations.ExecutableName, StringComparison.OrdinalIgnoreCase);
}
