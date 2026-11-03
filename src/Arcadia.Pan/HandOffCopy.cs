using System.ComponentModel;
using System.Diagnostics;

namespace Arcadia.Pan;

/// <summary>Finds a running hand-off copy of Pan, the one in Pan-Uninstall that does an uninstall started from Arcadia's folder (#270).</summary>
internal static class HandOffCopy
{
    public static string Executable => Path.Combine(PanPaths.HandOffDirectory, PanPaths.ExecutableName);

    /// <summary>
    /// The id of a process other than <paramref name="self"/> running the hand-off copy, or null. Checked by its image path:
    /// only administrators can start a program from Program Files, so nobody else can fake one (a named mutex could be
    /// squatted by any user).
    /// </summary>
    public static int? RunningId(int self)
    {
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(PanPaths.ExecutableName)))
        {
            using (process)
            {
                if (process.Id != self && Runs(process, Executable))
                {
                    return process.Id;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Whether <paramref name="process"/> runs <paramref name="image"/>. The copy is meant to end, so a process found in
    /// the list may have exited before its image is read: it doesn't run anything any more.
    /// </summary>
    internal static bool Runs(Process process, string image)
    {
        try
        {
            return string.Equals(process.MainModule?.FileName, image, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            // Exited meanwhile, or a process this one may not inspect: not the copy, which runs elevated like Pan.
            return false;
        }
    }
}
