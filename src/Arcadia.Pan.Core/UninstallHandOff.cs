using System.Security.Cryptography;

namespace Arcadia.Pan;

/// <summary>What <see cref="UninstallHandOff.TryHandOff"/> did.</summary>
public enum HandOffResult
{
    /// <summary>Pan runs from <c>%ProgramFiles%\Arcadia</c> already (installed copy or the hand-off copy): it uninstalls itself.</summary>
    NotNeeded,

    /// <summary>The verified copy runs the uninstall; this process only exits.</summary>
    HandedOff,

    /// <summary>The copy failed (the log says why): this process uninstalls in place, as before #270.</summary>
    RunInPlace,
}

/// <summary>
/// <c>--uninstall</c> started from Arcadia's folder hands off to a copy in <c>%ProgramFiles%\Arcadia\Pan-Uninstall</c>
/// (#270): with the drivers it runs longer than Velopack's uninstall hook, and the bundled exe it ran from would stay
/// behind in <c>%LocalAppData%\Arcadia</c>. The copy sits in an admin-only folder, so it may schedule itself for deletion
/// at the next boot; a user-writable path must never be (a planted junction would make that an arbitrary deletion).
/// The hand-off never fails the uninstall: anything going wrong falls back to running in place.
/// </summary>
public static class UninstallHandOff
{
    public const string DirectoryName = "Pan-Uninstall";

    /// <summary>True when <paramref name="path"/> lies inside <paramref name="directory"/> (Windows paths: case-insensitive).</summary>
    public static bool IsInside(string path, string directory)
    {
        var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(folder, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Copies <paramref name="ownPath"/> into <paramref name="handOffDirectory"/>, checks the copy's SHA-256 against the
    /// original's and starts it with <paramref name="start"/> (the same arguments; it must not be waited for). Not done
    /// when Pan already runs from inside <paramref name="arcadiaProgramFiles"/>.
    /// </summary>
    public static HandOffResult TryHandOff(string ownPath, string arcadiaProgramFiles, string handOffDirectory, Action<string> start, Action<string> log)
    {
        if (IsInside(ownPath, arcadiaProgramFiles))
        {
            return HandOffResult.NotNeeded;
        }

        if (File.Exists(Path.ChangeExtension(ownPath, ".dll")))
        {
            // A plain build's Pan.exe is an apphost that needs Pan.dll beside it: a lone copy would start and die at once.
            log("Uninstalling in place: this Pan.exe is not a single file and can't be handed off");
            return HandOffResult.RunInPlace;
        }

        var copy = Path.Combine(handOffDirectory, Path.GetFileName(ownPath));
        try
        {
            CopyVerified(ownPath, copy);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            log($"Uninstalling in place, the hand-off copy failed: {ex.Message}");
            TryDelete(copy, log);
            return HandOffResult.RunInPlace;
        }

        // Logged before the start: once the copy runs it writes the same log, and this process writes nothing more.
        log($"Handing the uninstall off to {copy}");
        try
        {
            start(copy);
            return HandOffResult.HandedOff;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            log($"Uninstalling in place, the hand-off copy could not be started: {ex.Message}");
            TryDelete(copy, log);
            return HandOffResult.RunInPlace;
        }
    }

    /// <summary>
    /// A <c>Pan-Uninstall</c> folder left by an uninstall that crashed or wasn't followed by a reboot goes at the next
    /// <c>--install</c> or <c>--uninstall</c>. Not from inside it: the copy can't delete itself and schedules that instead.
    /// </summary>
    public static void RemoveLeftover(string ownPath, string handOffDirectory, Action<string> log)
    {
        if (IsInside(ownPath, handOffDirectory) || !Directory.Exists(handOffDirectory))
        {
            return;
        }

        try
        {
            Directory.Delete(handOffDirectory, recursive: true);
            log($"Deleted the leftover {handOffDirectory}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log($"The leftover {handOffDirectory} could not be deleted: {ex.Message}");
        }
    }

    /// <summary>The source stays open without write access from the hash to the end of the copy, so it can't be swapped in between.</summary>
    private static void CopyVerified(string source, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        string sourceHash;
        using (var from = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            sourceHash = Convert.ToHexString(SHA256.HashData(from));
            from.Position = 0;
            using var to = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None);
            from.CopyTo(to);
        }

        string copyHash;
        using (var copied = File.OpenRead(target))
        {
            copyHash = Convert.ToHexString(SHA256.HashData(copied));
        }

        if (!string.Equals(sourceHash, copyHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"the copy doesn't match (expected SHA-256 {sourceHash}, got {copyHash})");
        }
    }

    private static void TryDelete(string file, Action<string> log)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log($"{file} could not be deleted ({ex.Message}); the next --install or --uninstall removes it");
        }
    }
}
